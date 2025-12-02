using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace WallHitSound.Services
{
    /// <summary>
    /// 音声クリップの読み込み、再生、音量・ピッチ制御を管理するサービス。
    /// ビープ音またはカスタムオーディオファイル（WAV/OGG/MP3）を再生できる。
    /// </summary>
    public class WallHitSoundService : System.IDisposable
    {
        private GameObject _go;
        private AudioSource _audioSource;
        private AudioClip _cachedClip;
        private bool _suppressLogs = false;

        public void SetLogSuppressed(bool suppress)
        {
            _suppressLogs = suppress;
        }

        private void LogDebug(string msg)
        {
            if (!_suppressLogs) Plugin.Log?.Debug(msg);
        }
        private void LogInfo(string msg)
        {
            if (!_suppressLogs) Plugin.Log?.Info(msg);
        }
        private void LogWarn(string msg)
        {
            if (!_suppressLogs) Plugin.Log?.Warn(msg);
        }
        private void LogError(string msg)
        {
            if (!_suppressLogs) Plugin.Log?.Error(msg);
        }

        /// <summary>
        /// AudioSourceを作成して初期化し、設定に応じて音声を読み込む。
        /// </summary>
        public void Initialize()
        {
            LogDebug("WallHitSound: Initializing WallHitSoundService");

            _go = new GameObject("WallHitSound_Audio");
            UnityEngine.Object.DontDestroyOnLoad(_go);

            _audioSource = _go.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;

            // Menu スコープで確実に再生できるように設定
            _audioSource.spatialBlend = 0.0f;  // 2D audio
            _audioSource.bypassEffects = false;
            _audioSource.bypassListenerEffects = false;
            _audioSource.bypassReverbZones = false;

            LogDebug($"WallHitSound: AudioSource created - Enabled={_audioSource.enabled}, Spatial={_audioSource.spatialBlend}");

            // 設定に基づいて音声を読み込む
            LoadSound();

            // 初期音量とピッチを設定
            try
            {
                float vol = PluginConfig.Instance.Volume;
                SetVolume(vol);
                LogDebug($"WallHitSound: Set volume to {vol}");
            }
            catch (Exception ex)
            {
                Plugin.Log?.Warn($"WallHitSound: Failed to set initial volume: {ex.Message}");
            }

            try
            {
                float pitch = PluginConfig.Instance.AudioPitch;
                SetPitch(pitch);
                LogDebug($"WallHitSound: Set pitch to {pitch}");
            }
            catch (Exception ex)
            {
                Plugin.Log?.Warn($"WallHitSound: Failed to set initial pitch: {ex.Message}");
            }

            Plugin.Log?.Info("WallHitSound: WallHitSoundService initialized successfully");
            LogInfo("WallHitSound: WallHitSoundService initialized successfully");
        }

        /// <summary>
        /// 設定に基づいて音声を読み込む。ビープ音またはカスタムファイルから選択。
        /// </summary>
        private void LoadSound()
        {
            try
            {
                string selectedSound = PluginConfig.Instance.SelectedClipName ?? "beep";
                LogInfo($"WallHitSound: LoadSound called, selectedSound = '{selectedSound}'");

                if (selectedSound == "beep")
                {
                    // 埋め込まれたビープ音、またはフォールバックビープを使用
                    _cachedClip = Resources.Load<AudioClip>("Audio/beep");
                    if (_cachedClip == null)
                    {
                        _cachedClip = CreateFallbackBeep();
                    }
                    Plugin.Log?.Info($"WallHitSound: Using beep sound");
                    LogInfo($"WallHitSound: Using beep sound");
                }
                else
                {
                    // UserDataからカスタムオーディオファイルを読み込む
                    string userDataPath = Utilities.BeatSaberPathHelper.GetBeatSaberUserDataPath();
                    Plugin.Log?.Info($"WallHitSound: userDataPath = '{userDataPath}'");
                    LogInfo($"WallHitSound: userDataPath = '{userDataPath}'");

                    if (userDataPath == null)
                    {
                        Plugin.Log?.Warn($"WallHitSound: Could not determine UserData path, using fallback beep");
                        LogWarn($"WallHitSound: Could not determine UserData path, using fallback beep");
                        _cachedClip = CreateFallbackBeep();
                        return;
                    }

                    // 複数の拡張子でファイル検索
                    string[] extensions = { ".wav", ".ogg", ".mp3" };
                    AudioClip loadedClip = null;

                    foreach (var ext in extensions)
                    {
                        string filePath = Path.Combine(userDataPath, selectedSound + ext);
                        Plugin.Log?.Debug($"WallHitSound: Checking for file: {filePath}");
                        LogDebug($"WallHitSound: Checking for file: {filePath}");
                        if (File.Exists(filePath))
                        {
                            Plugin.Log?.Info($"WallHitSound: Found file: {filePath}");
                            LogInfo($"WallHitSound: Found file: {filePath}");
                            loadedClip = LoadAudioClip(filePath);
                            if (loadedClip != null)
                            {
                                _cachedClip = loadedClip;
                                Plugin.Log?.Info($"WallHitSound: Successfully loaded custom audio: {selectedSound}{ext}");
                                LogInfo($"WallHitSound: Successfully loaded custom audio: {selectedSound}{ext}");
                                return;
                            }
                        }
                    }

                    // カスタムファイルが見つからない場合はビープ音にフォールバック
                    Plugin.Log?.Warn($"WallHitSound: Custom audio file '{selectedSound}' not found in {userDataPath}, using fallback beep");
                    LogWarn($"WallHitSound: Custom audio file '{selectedSound}' not found in {userDataPath}, using fallback beep");
                    _cachedClip = CreateFallbackBeep();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"WallHitSound: Error loading sound: {ex.Message}\n{ex.StackTrace}");
                LogError($"WallHitSound: Error loading sound: {ex.Message}\n{ex.StackTrace}");

                // エラー時はビープ音にフォールバック
                _cachedClip = CreateFallbackBeep();
            }
        }

        /// <summary>
        /// 指定されたファイルパスからオーディオクリップを読み込む。
        /// </summary>
        private AudioClip LoadAudioClip(string filePath)
        {
            try
            {
                string extension = Path.GetExtension(filePath).ToLower();
                Plugin.Log?.Info($"WallHitSound: LoadAudioClip called for: {filePath} (extension: {extension})");
                LogInfo($"WallHitSound: LoadAudioClip called for: {filePath} (extension: {extension})");

                // サポートされている形式をチェック
                if (extension == ".wav" || extension == ".ogg" || extension == ".mp3")
                {
                    return LoadAudioClipViaWeb(filePath);
                }
                else
                {
                    Plugin.Log?.Warn($"WallHitSound: Format {extension} is not supported. Supported formats: WAV, OGG, MP3");
                    LogWarn($"WallHitSound: Format {extension} is not supported. Supported formats: WAV, OGG, MP3");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"WallHitSound: Failed to load audio clip {filePath}: {ex.Message}\n{ex.StackTrace}");
                LogError($"WallHitSound: Failed to load audio clip {filePath}: {ex.Message}\n{ex.StackTrace}");
            }
            return null;
        }

        /// <summary>
        /// UnityWebRequestを使用してファイルからオーディオクリップを読み込む。
        /// </summary>
        private AudioClip LoadAudioClipViaWeb(string filePath)
        {
            try
            {
                Plugin.Log?.Info($"WallHitSound: Loading audio via UnityWebRequest from: {filePath}");
                LogInfo($"WallHitSound: Loading audio via UnityWebRequest from: {filePath}");

                // ファイルパスをURI形式に変換
                string uriPath = GetEscapedURLForFilePath(filePath);
                AudioType audioType = GetAudioTypeFromPath(filePath);

                using (var request = UnityWebRequestMultimedia.GetAudioClip(uriPath, audioType))
                {
                    var task = request.SendWebRequest();

                    // 読み込み完了まで待機（タイムアウト約5秒）
                    int timeoutCounter = 0;
                    while (!task.isDone && timeoutCounter < 500)
                    {
                        System.Threading.Thread.Sleep(10);
                        timeoutCounter++;
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                        if (clip != null)
                        {
                            Plugin.Log?.Info($"WallHitSound: Successfully loaded audio clip: {Path.GetFileName(filePath)}");
                            LogInfo($"WallHitSound: Successfully loaded audio clip: {Path.GetFileName(filePath)}");
                            return clip;
                        }
                    }
                    else
                    {
                        Plugin.Log?.Error($"WallHitSound: Failed to load audio via web request: {request.error}");
                        LogError($"WallHitSound: Failed to load audio via web request: {request.error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"WallHitSound: Error loading audio via web: {ex.Message}\n{ex.StackTrace}");
                LogError($"WallHitSound: Error loading audio via web: {ex.Message}\n{ex.StackTrace}");
            }
            return null;
        }

        /// <summary>
        /// Windowsファイルパスをファイル URI形式に変換する。
        /// </summary>
        private string GetEscapedURLForFilePath(string filePath)
        {
            // Windowsパスをファイル URI形式に変換
            return "file:///" + filePath.Replace("\\", "/");
        }

        /// <summary>
        /// ファイル拡張子からAudioTypeを決定する。
        /// </summary>
        private AudioType GetAudioTypeFromPath(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".wav" => AudioType.WAV,
                ".ogg" => AudioType.OGGVORBIS,
                ".mp3" => AudioType.MPEG,
                _ => AudioType.UNKNOWN
            };
        }

        /// <summary>
        /// リソースを解放する。
        /// </summary>
        public void Dispose()
        {
            if (_go != null) UnityEngine.Object.Destroy(_go);
        }

        /// <summary>
        /// キャッシュされた音声クリップを再生する。
        /// </summary>
        public void PlaySound()
        {
            if (_audioSource == null)
            {
                Plugin.Log?.Warn("WallHitSound: AudioSource is null");
                LogWarn("WallHitSound: AudioSource is null");
                return;
            }

            try
            {
                // AudioSource が無効な場合は有効にする
                if (!_audioSource.enabled) _audioSource.enabled = true;

                // 設定から音量とピッチを取得
                float volume = Mathf.Clamp01(PluginConfig.Instance.Volume);
                float pitch = Mathf.Clamp(PluginConfig.Instance.AudioPitch, 0.5f, 2.0f);

                // AudioSource のプロパティを設定
                _audioSource.volume = volume;
                _audioSource.pitch = pitch;

                Plugin.Log?.Debug($"WallHitSound: AudioSource settings - volume={volume}, pitch={pitch}");
                LogDebug($"WallHitSound: AudioSource settings - volume={volume}, pitch={pitch}");

                if (_cachedClip != null)
                {
                    Plugin.Log?.Debug($"WallHitSound: Playing cached clip: {_cachedClip.name}");
                    LogDebug($"WallHitSound: Playing cached clip: {_cachedClip.name}");
                    _audioSource.PlayOneShot(_cachedClip, 1.0f);  // PlayOneShot は volume を無視するので、直接 volume を設定
                    return;
                }

                // フォールバック：ビープ音を生成して再生
                Plugin.Log?.Debug("WallHitSound: Playing fallback beep");
                LogDebug("WallHitSound: Playing fallback beep");
                AudioClip beep = CreateFallbackBeep();
                if (beep != null)
                {
                    _audioSource.PlayOneShot(beep, 1.0f);
                }
                else
                {
                    Plugin.Log?.Error("WallHitSound: Failed to create fallback beep");
                    LogError("WallHitSound: Failed to create fallback beep");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"WallHitSound: PlaySound failed: {ex.Message}\n{ex.StackTrace}");
                LogError($"WallHitSound: PlaySound failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 設定の周波数を使用してビープ音を生成する（正弦波）。
        /// </summary>
        private AudioClip CreateFallbackBeep()
        {
            // サンプル数：44.1kHzで約0.05秒
            int sampleCount = 2205;
            float[] data = new float[sampleCount];
            float freq = PluginConfig.Instance.BeepFrequency;

            // 正弦波を生成
            for (int i = 0; i < sampleCount; i++)
            {
                data[i] = Mathf.Sin((2f * Mathf.PI * freq * i) / 44100f) * 0.5f;
            }

            AudioClip clip = AudioClip.Create("wallhit_beep", sampleCount, 1, 44100, false);
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

        /// <summary>
        /// 現在の設定に基づいて音声を再読み込みする。
        /// </summary>
        public void ReloadSound()
        {
            Plugin.Log?.Info("WallHitSound: Reloading sound");
            LogInfo("WallHitSound: Reloading sound");
            LoadSound();
        }
    }
}
