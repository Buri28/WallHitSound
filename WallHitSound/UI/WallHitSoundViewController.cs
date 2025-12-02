using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.GameplaySetup;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;
using WallHitSound.Services;
using WallHitSound.Utilities;
using ModestTree;

namespace WallHitSound.UI
{
    /// <summary>
    /// ゲームプレイ画面の設定タブを管理し、プラグインの各種設定を BSML UIで操作できる MonoBehaviour。
    /// INotifyPropertyChanged により、UI変更を設定と連動させる。
    /// </summary>
    public class WallHitSoundViewController : MonoBehaviour, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly bool _verboseLogs = false;
        private void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            if (_verboseLogs)
            {
                Plugin.Log?.Debug($"WallHitSound: NotifyPropertyChanged for {propName}");
            }
        }

        // BSML 実装差異に備え、エイリアス名の両方で通知する
        private void NotifyUI(string alias)
        {
            // NotifyPropertyChanged(propName);
            NotifyPropertyChanged(alias);
        }
        private void NotifyUIValueChangedEnabled() => NotifyUI("enabled");
        private void NotifyUIValueChangedVolume() => NotifyUI("volume");
        private void NotifyUIValueChangedSelectedSound() => NotifyUI("selected-sound");
        private void NotifyUIValueChangedBeepFrequency() => NotifyUI("beep-frequency");
        private void NotifyUIValueChangedAudioPitch() => NotifyUI("audio-pitch");
        private void NotifyUIValueChangedIsBeepSelected() => NotifyUI("is-beep-selected");
        private void NotifyUIValueChangedSoundOptions() => NotifyUI("sound-options");
        private List<object> _customSoundFiles = new List<object>();

        // UI コンポーネント参照（パース後に解決）
        [UIComponent("enabled-toggle")] private ToggleSetting _enabledToggle;
        [UIComponent("volume-slider")] private SliderSetting _volumeSlider;
        [UIComponent("sound-dropdown")] private DropDownListSetting _soundDropdown;
        [UIComponent("beep-slider")] private SliderSetting _beepSlider;
        [UIComponent("pitch-slider")] private SliderSetting _pitchSlider;

        // ローカルバインディング用の変数（UI表示用）
        private bool _enabled = true;
        private float _volume = 1.0f;
        private string _selectedSound = "beep";
        private float _beepFrequency = 1000f;
        private float _audioPitch = 1.0f;

        // Menu scope 用の AudioSource
        private AudioSource _localAudioSource;

        // Menu scope 用の WallHitSoundService（Zenject注入用）
        [Inject]
        private WallHitSoundService _menuSoundService;



        private void Awake()
        {
            Plugin.Log?.Info("WallHitSoundViewController Awake");

            // ローカル変数をPluginConfigから初期化
            _enabled = PluginConfig.Instance.Enabled;
            _volume = PluginConfig.Instance.Volume;
            _selectedSound = PluginConfig.Instance.SelectedClipName ?? "beep";
            _beepFrequency = PluginConfig.Instance.BeepFrequency;
            _audioPitch = PluginConfig.Instance.AudioPitch;
            Plugin.Log?.Info($"WallHitSound: Initialized local fields - Enabled={_enabled}, Volume={_volume}, Sound={_selectedSound}, Freq={_beepFrequency}, Pitch={_audioPitch}");

            // Menu scope 用のローカル AudioSource を作成
            GameObject audioGO = new GameObject("WallHitSound_MenuAudio");
            audioGO.transform.SetParent(gameObject.transform);
            _localAudioSource = audioGO.AddComponent<AudioSource>();
            _localAudioSource.playOnAwake = false;
            _localAudioSource.spatialBlend = 0.0f;  // 2D audio
            Plugin.Log?.Info("WallHitSound: Created local AudioSource for Menu scope");

            // カスタムサウンドファイルを読み込む
            LoadCustomSoundFiles();
        }

        private void Start()
        {
            Plugin.Log?.Info("WallHitSoundViewController started");
            AddGameplayTabIfNeeded();
        }

        // BSMLのパース完了後に確実に初期値をUIへ反映
        [UIAction("#post-parse")]
        private void OnPostParse()
        {
            Plugin.Log?.Info("WallHitSoundViewController OnPostParse - syncing UI components");
            // UI コンポーネントの初期同期
            if (_enabledToggle != null) { _enabledToggle.Value = _enabled; _enabledToggle.ReceiveValue(); }
            if (_volumeSlider != null) { _volumeSlider.Value = _volume; _volumeSlider.ReceiveValue(); }
            if (_soundDropdown != null) { _soundDropdown.Value = _selectedSound; _soundDropdown.ReceiveValue(); }
            if (_beepSlider != null) { _beepSlider.Value = _beepFrequency; _beepSlider.ReceiveValue(); }
            if (_pitchSlider != null) { _pitchSlider.Value = _audioPitch; _pitchSlider.ReceiveValue(); }
        }

        private void OnEnable()
        {
            Plugin.Log?.Info("WallHitSoundViewController OnEnable");
        }

        private void OnDestroy()
        {
            Plugin.Log?.Info("WallHitSoundViewController destroyed");
        }

        /// <summary>
        /// UserDataからカスタムオーディオファイルを読み込む。
        /// </summary>
        private void LoadCustomSoundFiles()
        {
            try
            {
                // Beat SaberのUserDataパスを取得
                string userDataPath = Utilities.BeatSaberPathHelper.GetBeatSaberUserDataPath();

                if (userDataPath == null)
                {
                    Plugin.Log?.Error("WallHitSound: Could not determine Beat Saber UserData path");
                    _customSoundFiles.Clear();
                    _customSoundFiles.Add("beep");
                    return;
                }

                // ディレクトリが存在しない場合は作成
                if (!Directory.Exists(userDataPath))
                {
                    Directory.CreateDirectory(userDataPath);
                    Plugin.Log?.Info($"WallHitSound: Created UserData/WallHitSound directory: {userDataPath}");

                    // デフォルトサウンドをセットアップ
                    Services.DefaultSoundInitializer.InitializeDefaultSounds(userDataPath);
                }

                // オーディオファイル（WAV/OGG/MP3）を検索
                var audioFiles = Directory.GetFiles(userDataPath)
                    .Where(f => f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .ToList();

                _customSoundFiles.Clear();
                _customSoundFiles.Add("beep"); // デフォルトのビープ音オプション
                _customSoundFiles.AddRange(audioFiles);

                Plugin.Log?.Info($"WallHitSound: Loaded {audioFiles.Count} custom sound files from {userDataPath}");
                foreach (var file in audioFiles)
                {
                    Plugin.Log?.Debug($"  - {file}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"WallHitSound: Error loading custom sound files: {ex.Message}");
                _customSoundFiles.Clear();
                _customSoundFiles.Add("beep");
            }
        }

        private bool _tabAdded = false;

        /// <summary>
        /// ゲームプレイ設定タブを追加する。
        /// </summary>
        private void AddGameplayTabIfNeeded()
        {
            if (_tabAdded) return;
            if (GameplaySetup.Instance == null) return;

            try
            {
                GameplaySetup.Instance.RemoveTab("WallHitSound");
                GameplaySetup.Instance.AddTab("WallHitSound", "WallHitSound.UI.Settings.bsml", this, MenuType.Solo);
                _tabAdded = true;
                Plugin.Log?.Info("WallHitSoundViewController: added GameplaySetup tab 'WallHitSound'");
            }
            catch (Exception ex)
            {
                Plugin.Log?.Warn($"WallHitSoundViewController: AddGameplayTab failed: {ex.Message}");
            }
        }

        /// <summary>
        /// LateUpdate で GameplaySetup が利用可能になるのを待つ。
        /// </summary>
        private void LateUpdate()
        {
            if (!_tabAdded && GameplaySetup.Instance != null)
            {
                AddGameplayTabIfNeeded();
            }
        }

        /// <summary>
        /// プラグイン有効/無効の設定。
        /// </summary>
        [UIValue("enabled")]
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    PluginConfig.Instance.Enabled = value;
                    Plugin.Log?.Info($"WallHitSound: Enabled changed to {value}");
                    NotifyUIValueChangedEnabled();
                }
            }
        }

        /// <summary>
        /// 音量の設定（0～1）。
        /// </summary>
        [UIValue("volume")]
        public float Volume
        {
            get => _volume;
            set
            {
                if (Math.Abs(_volume - value) > 0.001f)
                {
                    _volume = value;
                    PluginConfig.Instance.Volume = value;
                    Plugin.Log?.Info($"WallHitSound: Volume changed to {value}");
                    NotifyUIValueChangedVolume();
                }
            }
        }

        /// <summary>
        /// 利用可能なサウンドオプション（beep + カスタムファイル一覧）。
        /// </summary>
        [UIValue("sound-options")]
        public List<object> SoundOptions
        {
            get => _customSoundFiles;
        }


        /// <summary>
        /// 選択されたサウンドの名前。選択時に音声を再読み込みする。
        /// </summary>
        [UIValue("selected-sound")]
        public string SelectedSound
        {
            get => _selectedSound;
            set
            {
                if (_selectedSound != value)
                {
                    _selectedSound = value;
                    PluginConfig.Instance.SelectedClipName = value;
                    Plugin.Log?.Info($"WallHitSound: SelectedSound changed to {value}");
                    NotifyUIValueChangedSelectedSound();
                    NotifyUIValueChangedIsBeepSelected();

                    // サウンド選択変更時に音声を再読み込み
                    try
                    {
                        // Menu scope の WallHitSoundService を優先的に使用
                        if (_menuSoundService != null)
                        {
                            Plugin.Log?.Info($"WallHitSound: Calling ReloadSound for (Menu scope): {value}");
                            _menuSoundService.ReloadSound();
                            Plugin.Log?.Info($"WallHitSound: Sound reloaded successfully (Menu scope)");
                        }
                        // Menu scope で使用できない場合は Player scope の WallHitSoundManager を使用
                        else if (WallHitSoundManager.Instance != null && WallHitSoundManager.Instance.SoundService != null)
                        {
                            Plugin.Log?.Info($"WallHitSound: Calling ReloadSound for (Player scope): {value}");
                            WallHitSoundManager.Instance.SoundService.ReloadSound();
                            Plugin.Log?.Info($"WallHitSound: Sound reloaded successfully (Player scope)");
                        }
                        // メニュースコープではサービスがなくても警告しない（ログ抑制）
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.Error($"WallHitSound: Failed to reload sound: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
        }


        /// <summary>
        /// ビープ音が選択されているかどうか。
        /// </summary>
        [UIValue("is-beep-selected")]
        public bool IsBeepSelected
        {
            get => (_selectedSound ?? "beep") == "beep";
        }

        // 動的なスライダー無効化は撤回（簡素化）

        /// <summary>
        /// ビープ音の周波数（Hz、100～2000）。カスタムサウンド選択時も編集可能（簡素化）。
        /// </summary>
        [UIValue("beep-frequency")]
        public float BeepFrequency
        {
            get => _beepFrequency;
            set
            {
                if (Math.Abs(_beepFrequency - value) > 0.001f)
                {
                    _beepFrequency = value;
                    PluginConfig.Instance.BeepFrequency = value;
                    Plugin.Log?.Info($"WallHitSound: BeepFrequency changed to {value}");
                    NotifyUIValueChangedBeepFrequency();

                    // 新しい周波数で音声を再読み込み
                    try
                    {
                        if (WallHitSoundManager.Instance != null && WallHitSoundManager.Instance.SoundService != null)
                        {
                            WallHitSoundManager.Instance.SoundService.ReloadSound();
                            Plugin.Log?.Info($"WallHitSound: Sound reloaded with new frequency: {value}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.Warn($"WallHitSound: Failed to reload sound: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// オーディオのピッチ（0.5～2.0）。すべてのサウンドに適用。
        /// </summary>
        [UIValue("audio-pitch")]
        public float AudioPitch
        {
            get => _audioPitch;
            set
            {
                if (Math.Abs(_audioPitch - value) > 0.001f)
                {
                    _audioPitch = value;
                    PluginConfig.Instance.AudioPitch = value;
                    Plugin.Log?.Info($"WallHitSound: AudioPitch changed to {value}");
                    NotifyUIValueChangedAudioPitch();

                    // Apply pitch immediately
                    try
                    {
                        if (WallHitSoundManager.Instance != null && WallHitSoundManager.Instance.SoundService != null)
                        {
                            WallHitSoundManager.Instance.SoundService.SetPitch(value);
                            Plugin.Log?.Info($"WallHitSound: Pitch applied: {value}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.Warn($"WallHitSound: Failed to apply pitch: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 現在の設定で音を再生するテストメソッド。
        /// Menu scope でも Player scope でも機能します。
        /// </summary>
        [UIAction("test-sound")]
        public void TestSound()
        {
            Plugin.Log?.Info("WallHitSound: ===== TEST SOUND START =====");
            try
            {
                // Menu scope のローカル AudioSource を優先的に使用
                if (_localAudioSource != null)
                {
                    Plugin.Log?.Info($"WallHitSound: Playing test sound (Local AudioSource) - Sound={_selectedSound}, Volume={_volume}, Pitch={_audioPitch}");
                    PlaySoundLocal();
                    Plugin.Log?.Info("WallHitSound: Test sound played successfully (Local AudioSource)");
                }
                // Menu scope の WallHitSoundService を試す
                else if (_menuSoundService != null)
                {
                    Plugin.Log?.Info($"WallHitSound: Playing test sound (Menu service) - Sound={_selectedSound}, Volume={_volume}, Pitch={_audioPitch}");
                    _menuSoundService.PlaySound();
                    Plugin.Log?.Info("WallHitSound: Test sound played successfully (Menu service)");
                }
                // Player scope の WallHitSoundManager を使用
                else if (WallHitSoundManager.Instance != null && WallHitSoundManager.Instance.SoundService != null)
                {
                    Plugin.Log?.Info($"WallHitSound: Playing test sound (Player scope) - Sound={_selectedSound}, Volume={_volume}, Pitch={_audioPitch}");
                    WallHitSoundManager.Instance.SoundService.PlaySound();
                    Plugin.Log?.Info("WallHitSound: Test sound played successfully (Player scope)");
                }
                else
                {
                    Plugin.Log?.Warn("WallHitSound: No audio source available for test sound");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"WallHitSound: TestSound failed: {ex.Message}\n{ex.StackTrace}");
            }
            Plugin.Log?.Info("WallHitSound: ===== TEST SOUND END =====");
        }

        /// <summary>
        /// ローカルAudioSourceで音声を再生する。
        /// </summary>
        private void PlaySoundLocal()
        {
            if (_localAudioSource == null)
            {
                Plugin.Log?.Warn("WallHitSound: Local AudioSource is null");
                return;
            }

            try
            {
                _localAudioSource.volume = Mathf.Clamp01(_volume);
                _localAudioSource.pitch = Mathf.Clamp(_audioPitch, 0.5f, 2.0f);

                // ビープ音またはカスタムサウンドを再生
                if (_selectedSound == "beep")
                {
                    // ビープ音を生成して再生
                    AudioClip beep = CreateBeepClip(_beepFrequency);
                    if (beep != null)
                    {
                        _localAudioSource.PlayOneShot(beep, 1.0f);
                        Plugin.Log?.Debug($"WallHitSound: Playing beep at {_beepFrequency}Hz");
                    }
                }
                else
                {
                    // カスタムサウンドを再生
                    AudioClip customClip = LoadCustomAudioClip(_selectedSound);
                    if (customClip != null)
                    {
                        _localAudioSource.PlayOneShot(customClip, 1.0f);
                        Plugin.Log?.Debug($"WallHitSound: Playing custom sound: {_selectedSound}");
                    }
                    else
                    {
                        Plugin.Log?.Warn($"WallHitSound: Failed to load custom sound: {_selectedSound}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"WallHitSound: PlaySoundLocal failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 指定の周波数でビープ音クリップを生成する。
        /// </summary>
        private AudioClip CreateBeepClip(float frequency)
        {
            try
            {
                // サンプル数：44.1kHzで約0.05秒
                int sampleCount = 2205;
                float[] data = new float[sampleCount];

                // 正弦波を生成
                for (int i = 0; i < sampleCount; i++)
                {
                    data[i] = Mathf.Sin((2f * Mathf.PI * frequency * i) / 44100f) * 0.5f;
                }

                AudioClip clip = AudioClip.Create("wallhit_beep", sampleCount, 1, 44100, false);
                clip.SetData(data, 0);
                return clip;
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"WallHitSound: Failed to create beep clip: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// カスタムオーディオファイルを読み込む。
        /// </summary>
        private AudioClip LoadCustomAudioClip(string soundName)
        {
            try
            {
                string userDataPath = Utilities.BeatSaberPathHelper.GetBeatSaberUserDataPath();
                if (userDataPath == null)
                {
                    Plugin.Log?.Error("WallHitSound: Could not determine UserData path");
                    return null;
                }

                // 複数の拡張子で検索
                string[] extensions = { ".wav", ".ogg", ".mp3" };
                foreach (var ext in extensions)
                {
                    string filePath = Path.Combine(userDataPath, soundName + ext);
                    if (File.Exists(filePath))
                    {
                        Plugin.Log?.Debug($"WallHitSound: Loading audio clip from: {filePath}");
                        return LoadAudioClipViaWeb(filePath);
                    }
                }

                Plugin.Log?.Warn($"WallHitSound: Could not find audio file: {soundName}");
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"WallHitSound: LoadCustomAudioClip failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// UnityWebRequest を使用してオーディオをロードする。
        /// </summary>
        private AudioClip LoadAudioClipViaWeb(string filePath)
        {
            try
            {
                string extension = Path.GetExtension(filePath).ToLower();
                AudioType audioType = extension switch
                {
                    ".wav" => AudioType.WAV,
                    ".ogg" => AudioType.OGGVORBIS,
                    ".mp3" => AudioType.MPEG,
                    _ => AudioType.UNKNOWN
                };

                string uriPath = "file:///" + filePath.Replace("\\", "/");
                using (var request = UnityWebRequestMultimedia.GetAudioClip(uriPath, audioType))
                {
                    var task = request.SendWebRequest();
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
                            Plugin.Log?.Info($"WallHitSound: Successfully loaded audio: {Path.GetFileName(filePath)}");
                            return clip;
                        }
                    }
                    else
                    {
                        Plugin.Log?.Error($"WallHitSound: Failed to load audio: {request.error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"WallHitSound: LoadAudioClipViaWeb failed: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// すべてのスライダーをデフォルト値にリセットする。
        /// </summary>
        [UIAction("reset-settings")]
        public void ResetSettings()
        {
            Plugin.Log?.Info("WallHitSound: ===== RESET SETTINGS START =====");

            // デフォルト値を設定
            Enabled = true;
            Volume = 1.0f;
            SelectedSound = "beep";
            BeepFrequency = 1000f;
            AudioPitch = 1.0f;

            // UIコンポーネントへ直接反映
            if (_enabledToggle != null) { _enabledToggle.Value = _enabled; _enabledToggle.ReceiveValue(); }
            if (_volumeSlider != null) { _volumeSlider.Value = _volume; _volumeSlider.ReceiveValue(); }
            if (_soundDropdown != null) { _soundDropdown.Value = _selectedSound; _soundDropdown.ReceiveValue(); }
            if (_beepSlider != null) { _beepSlider.Value = _beepFrequency; _beepSlider.ReceiveValue(); }
            if (_pitchSlider != null) { _pitchSlider.Value = _audioPitch; _pitchSlider.ReceiveValue(); }

            // タブ再構築は行わない（レイアウト崩れ回避）。通知のみで反映。
            if (_verboseLogs) Plugin.Log?.Info("WallHitSound: ===== RESET SETTINGS END =====");
        }
    }
}
