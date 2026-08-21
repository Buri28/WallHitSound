using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace WallHitSound.Services
{
    /// <summary>
    /// 壁ヒット音の読み込み・再生を管理するサービス。
    /// AudioSource とクリップはアプリ全体で1つだけ持ち（static + DontDestroyOnLoad）、曲をまたいで使い回す。
    /// クリップの用意はメニュー到達時・設定変更時に済ませ、曲開始時には何もしないようにしている
    /// （曲開始時に処理を入れると VR ランタイムのフレームレート判定に引っかかるため）。
    /// カスタム音の読み込みはコルーチンで非同期に行い、フレームを止めない。
    /// LRCounter の DropSoundPlayer と同じ方式。
    /// </summary>
    public class WallHitSoundService
    {
        /// <summary>生成ビープ音を表す設定値（これ以外は UserData のファイル名として扱う）。</summary>
        public const string BeepClipName = "beep";

        // アプリ全体で常駐する AudioSource。曲ごとに作り直すと曲開始時のコストになるので static で1つだけ持つ
        private static GameObject _go;
        private static AudioSource _audioSource;

        // コルーチン（カスタム音の非同期読み込み）の実行役。常駐 GameObject に付けるので曲をまたいで生き残る
        private static SoundRunner _runner;

        // 現在のクリップと、その生成条件を表すキー（"beep:{周波数}" または "file:{名前}"）。
        // static キャッシュなので、実際に作り直すのは設定を変えたときだけ
        private static AudioClip _clip;
        private static string _clipKey = "";

        // カスタム音を非同期読み込み中のキー（""=読み込み中でない）。二重に読み込みを走らせないための印
        private static string _loadingKey = "";

        // クリップが鳴り終わる予定の時刻（Time.time 基準。0=まだ鳴らしていない）。
        // 差し替え時に「いつまで待てば破棄してよいか」を知るために再生のたびに更新する
        private static float _clipEndTime;

        /// <summary>コルーチンを回すためだけの MonoBehaviour（常駐 GameObject に付く）。</summary>
        private class SoundRunner : MonoBehaviour { }

        /// <summary>
        /// 曲開始時に呼ばれる初期化。AudioSource とクリップは既に用意できているのが通常なので、
        /// ここでは実質何もしない（メニュー到達時・設定変更時の Prewarm で済ませてある）。
        /// </summary>
        public void Initialize()
        {
            Plugin.LogDebug("WallHitSound: Initializing WallHitSoundService");

            Prewarm();

            // 初期音量とピッチを設定
            try
            {
                float vol = PluginConfig.Instance.Volume;
                SetVolume(vol);
                Plugin.LogDebug($"WallHitSound: Set volume to {vol}");
            }
            catch (Exception ex)
            {
                Plugin.LogWarn($"WallHitSound: Failed to set initial volume: {ex.Message}");
            }

            try
            {
                float pitch = PluginConfig.Instance.AudioPitch;
                SetPitch(pitch);
                Plugin.LogDebug($"WallHitSound: Set pitch to {pitch}");
            }
            catch (Exception ex)
            {
                Plugin.LogWarn($"WallHitSound: Failed to set initial pitch: {ex.Message}");
            }

            Plugin.LogInfo("WallHitSound: WallHitSoundService initialized successfully");
        }

        /// <summary>
        /// AudioSource とクリップを事前に用意する。
        /// メニュー到達時と設定変更時に呼ぶことで、曲開始時にはキャッシュ済みの状態にしておく。
        /// ビープ生成は数千サンプルのループだけで軽く、カスタム音の読み込みは非同期なので、
        /// ここでフレームが飛ぶことはない。
        /// </summary>
        public void Prewarm()
        {
            EnsureAudioSource();
            EnsureClip();
        }

        /// <summary>
        /// 現在の設定に基づいて音声を再読み込みする。設定変更時に呼ぶ。
        /// Prewarm と違い、キーが同じでも必ず作り直す。
        /// 読み込みに失敗してビープが収まっている場合や、後からファイルが用意された場合に
        /// 選び直しで復帰できるようにするため。
        /// 進行中の読み込みの印も消すので、その結果は FinishLoad で捨てられる。
        /// </summary>
        public void ReloadSound()
        {
            Plugin.LogInfo("WallHitSound: Reloading sound");
            EnsureAudioSource();
            _clipKey = "";
            _loadingKey = "";
            EnsureClip();
        }

        /// <summary>
        /// 常駐する AudioSource を作る（初回のみ）。
        /// </summary>
        private static void EnsureAudioSource()
        {
            if (_audioSource != null) return;

            // 前の GameObject が何らかの理由で失われていた場合、読み込み中の印だけが残ると
            // そのキーが二度と読み込まれなくなるので、ここで落としておく
            _loadingKey = "";

            _go = new GameObject("WallHitSound_Audio");
            UnityEngine.Object.DontDestroyOnLoad(_go);
            _runner = _go.AddComponent<SoundRunner>();

            _audioSource = _go.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;

            // Menu スコープで確実に再生できるように設定
            _audioSource.spatialBlend = 0.0f;  // 2D audio
            _audioSource.bypassEffects = false;
            _audioSource.bypassListenerEffects = false;
            _audioSource.bypassReverbZones = false;

            Plugin.LogDebug($"WallHitSound: AudioSource created - Enabled={_audioSource.enabled}, Spatial={_audioSource.spatialBlend}");
        }

        /// <summary>
        /// 現在の設定に合ったクリップを用意して返す。まだ用意できていなければ null。
        /// 設定（ビープは周波数、カスタムはファイル名）が変わっていたら作り直し、古いクリップは破棄する。
        /// ビープはその場で生成（軽い）、カスタム音はコルーチンで非同期に読み込む（重いのでフレームを止めない）。
        /// </summary>
        private static AudioClip EnsureClip()
        {
            string clipName;
            float frequency;
            try
            {
                clipName = PluginConfig.Instance.SelectedClipName;
                frequency = PluginConfig.Instance.BeepFrequency;
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"WallHitSound: Failed to read sound settings: {ex.Message}");
                return _clip;
            }

            if (string.IsNullOrEmpty(clipName)) clipName = BeepClipName;

            string key = clipName == BeepClipName ? $"beep:{frequency}" : $"file:{clipName}";
            if (_clip != null && _clipKey == key) return _clip;

            if (clipName == BeepClipName)
            {
                SetClip(key, CreateBeep(frequency));
                Plugin.LogInfo($"WallHitSound: Using beep sound ({frequency}Hz)");
                return _clip;
            }

            // カスタム音: 読み込みが終わるまで鳴らせるクリップは無い（null を返す）。
            // 同じキーの読み込みが進行中なら二重には走らせない。
            // 実行役がいないときは印を立てない（立てると二度と読み込まれなくなるため）。
            if (_loadingKey != key && _runner != null)
            {
                _loadingKey = key;
                Plugin.LogInfo($"WallHitSound: Loading custom audio '{clipName}'");
                _runner.StartCoroutine(LoadCustomClipCoroutine(key, clipName, frequency));
            }
            return null;
        }

        /// <summary>
        /// 読み込み・生成が済んだクリップを収める。古いクリップは破棄する
        /// （スクリプト生成の AudioClip は放置すると積み上がるため）。
        /// </summary>
        private static void SetClip(string key, AudioClip clip)
        {
            DestroyOldClip();
            _clip = clip;
            _clipKey = key;
        }

        /// <summary>
        /// 古いクリップを破棄する。まだ鳴っている最中なら鳴り終わるまで破棄を遅らせる
        /// （設定画面でテスト再生した直後にサウンドを変える、といった操作で踏みうるため）。
        /// 待ち時間は再生開始時に控えた「鳴り終わる予定の時刻」からの残り時間で決める。
        /// </summary>
        private static void DestroyOldClip()
        {
            AudioClip old = _clip;
            _clip = null;
            _clipKey = "";

            float remaining = _clipEndTime - Time.time;
            _clipEndTime = 0f;
            if (old == null) return;

            // PlayOneShot 中は AudioSource.isPlaying が当てにならない環境があるので、
            // 「鳴り終わる予定の時刻」だけを見て判断する
            if (remaining > 0f)
            {
                UnityEngine.Object.Destroy(old, remaining + 0.5f); // 余裕をみて少し長めに待つ
            }
            else
            {
                UnityEngine.Object.Destroy(old);
            }
        }

        /// <summary>
        /// キャッシュされた音声クリップを再生する。読み込み中でクリップが無い場合は何もしない。
        /// </summary>
        public void PlaySound()
        {
            try
            {
                EnsureAudioSource();
                if (_audioSource == null)
                {
                    Plugin.LogWarn("WallHitSound: AudioSource is null");
                    return;
                }

                // AudioSource が無効な場合は有効にする
                if (!_audioSource.enabled) _audioSource.enabled = true;

                AudioClip clip = EnsureClip();
                if (clip == null)
                {
                    Plugin.LogDebug("WallHitSound: Clip is not ready yet");
                    return;
                }

                // 設定から音量とピッチを取得
                float volume = Mathf.Clamp01(PluginConfig.Instance.Volume);
                float pitch = Mathf.Clamp(PluginConfig.Instance.AudioPitch, 0.5f, 2.0f);

                // AudioSource のプロパティを設定
                // PlayOneShot は volume を無視するので、直接 volume を設定する
                _audioSource.volume = volume;
                _audioSource.pitch = pitch;
                _audioSource.PlayOneShot(clip, 1.0f);

                // 鳴り終わる時刻を控えておく（再生時間はピッチで縮む）。
                // 差し替え時の破棄をこの時刻まで遅らせて、再生中のクリップが消えないようにする
                _clipEndTime = Time.time + clip.length / _audioSource.pitch;

                // 壁に当たるたびに通るので、詳細ログが無効なら文字列の組み立てもしない
                if (Plugin.VerboseLogs)
                {
                    Plugin.LogDebug($"WallHitSound: Playing clip {clip.name} - volume={volume}, pitch={pitch}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"WallHitSound: PlaySound failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // ─── カスタムサウンド（UserData の wav/ogg/mp3） ───────────────────

        private static readonly string[] AudioExtensions = { ".wav", ".ogg", ".mp3" };

        /// <summary>
        /// UserData から拡張子を総当たりでファイルを探す。見つからなければ null。
        /// </summary>
        private static string FindSoundFile(string clipName)
        {
            // ここで例外が漏れるとコルーチンが yield 前に死に、読み込み中の印が残って
            // そのキーが二度と読み込まれなくなるため、丸ごと保護する
            try
            {
                string userDataPath = Utilities.BeatSaberPathHelper.GetBeatSaberUserDataPath();
                if (userDataPath == null)
                {
                    Plugin.LogWarn("WallHitSound: Could not determine UserData path");
                    return null;
                }

                foreach (var ext in AudioExtensions)
                {
                    string filePath = Path.Combine(userDataPath, clipName + ext);
                    if (File.Exists(filePath)) return filePath;
                }
                Plugin.LogWarn($"WallHitSound: Custom audio file '{clipName}' not found in {userDataPath}");
                return null;
            }
            catch (Exception ex)
            {
                Plugin.LogWarn($"WallHitSound: Failed to look up custom audio '{clipName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// カスタム音を UnityWebRequest で非同期に読み込む。
        /// 以前はメインスレッドを Thread.Sleep で最大5秒止めていたが、それだと読み込みが走る
        /// フレームが確実に飛ぶため、コルーチンで待つ方式にした。読み込み中は音が鳴らない
        /// （EnsureClip が null を返す）が、フレームレートには影響しない。
        /// ファイルが無い・読み込みに失敗した場合はビープにフォールバックする。
        /// </summary>
        private static IEnumerator LoadCustomClipCoroutine(string key, string clipName, float frequency)
        {
            string filePath = FindSoundFile(clipName);
            if (filePath == null)
            {
                FinishLoad(key, CreateBeep(frequency));
                yield break;
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            AudioType audioType = ext switch
            {
                ".wav" => AudioType.WAV,
                ".ogg" => AudioType.OGGVORBIS,
                ".mp3" => AudioType.MPEG,
                _ => AudioType.UNKNOWN,
            };
            string uriPath = "file:///" + filePath.Replace("\\", "/");

            AudioClip loaded = null;
            using (var request = UnityWebRequestMultimedia.GetAudioClip(uriPath, audioType))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    // GetContent は例外を投げうるので、コルーチンで yield を挟まない範囲だけ try で囲む
                    try
                    {
                        loaded = DownloadHandlerAudioClip.GetContent(request);
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogWarn($"WallHitSound: Error loading audio '{filePath}': {ex.Message}");
                    }
                }
                else
                {
                    Plugin.LogWarn($"WallHitSound: Failed to load audio '{filePath}': {request.error}");
                }
            }

            if (loaded != null) Plugin.LogInfo($"WallHitSound: Successfully loaded custom audio: {Path.GetFileName(filePath)}");
            FinishLoad(key, loaded ?? CreateBeep(frequency));
        }

        /// <summary>
        /// 非同期読み込みの完了処理。待っている間に設定が変わっていたら結果は捨てる。
        /// </summary>
        private static void FinishLoad(string key, AudioClip clip)
        {
            if (_loadingKey != key)
            {
                // 読み込み中に別のクリップへ切り替わった。この結果はもう使わない
                if (clip != null) UnityEngine.Object.Destroy(clip);
                return;
            }
            _loadingKey = "";
            SetClip(key, clip);
        }

        /// <summary>
        /// 設定の周波数で正弦波のビープ音を生成する（約0.05秒）。
        /// </summary>
        private static AudioClip CreateBeep(float frequency)
        {
            const int sampleRate = 44100;
            int sampleCount = 2205; // 約0.05秒
            float[] data = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                data[i] = Mathf.Sin((2f * Mathf.PI * frequency * i) / sampleRate) * 0.5f;
            }

            AudioClip clip = AudioClip.Create("wallhit_beep", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// AudioSourceの音量を設定する（0～1）。
        /// </summary>
        public void SetVolume(float v)
        {
            if (_audioSource != null) _audioSource.volume = Mathf.Clamp01(v);
        }

        /// <summary>
        /// AudioSourceのピッチを設定する（0.5～2.0）。
        /// </summary>
        public void SetPitch(float p)
        {
            if (_audioSource != null) _audioSource.pitch = Mathf.Clamp(p, 0.5f, 2.0f);
        }
    }
}
