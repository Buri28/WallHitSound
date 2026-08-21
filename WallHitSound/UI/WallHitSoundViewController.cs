using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.GameplaySetup;
using UnityEngine;
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
        private void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
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
        // ビルド時は未割り当てなので CS0649 を抑制（BSML がランタイムで設定）
#pragma warning disable 0649
        [UIComponent("enabled-toggle")] private ToggleSetting _enabledToggle;
        [UIComponent("volume-slider")] private SliderSetting _volumeSlider;
        [UIComponent("sound-dropdown")] private DropDownListSetting _soundDropdown;
        [UIComponent("beep-slider")] private SliderSetting _beepSlider;
        [UIComponent("pitch-slider")] private SliderSetting _pitchSlider;
        [UIComponent("particle-slider")] private SliderSetting _particleSlider;
#pragma warning restore 0649

        // ローカルバインディング用の変数（UI表示用）
        private bool _enabled = true;
        private float _volume = 1.0f;
        private string _selectedSound = "beep";
        private float _beepFrequency = 1000f;
        private float _audioPitch = 1.0f;
        private float _particleCount = 0f;

        // 設定の反映・テスト再生に使うサービス。
        // AudioSource・クリップはサービス側で static に常駐するので、
        // どのインスタンスから触っても同じ音を扱える（Zenject の注入は不要）
        private WallHitSoundService _soundService;

        /// <summary>設定の反映・テスト再生に使うサービス。実際の再生と同じ経路を通す。</summary>
        private WallHitSoundService SoundService => _soundService ??= new WallHitSoundService();



        private void Awake()
        {
            Plugin.LogInfo("WallHitSoundViewController Awake");

            // ローカル変数をPluginConfigから初期化
            _enabled = PluginConfig.Instance.Enabled;
            _volume = PluginConfig.Instance.Volume;
            _selectedSound = PluginConfig.Instance.SelectedClipName ?? "beep";
            _beepFrequency = PluginConfig.Instance.BeepFrequency;
            _audioPitch = PluginConfig.Instance.AudioPitch;
            _particleCount = PluginConfig.Instance.ParticleCount;
            Plugin.LogInfo($"WallHitSound: Initialized local fields - Enabled={_enabled}, Volume={_volume}, Sound={_selectedSound}, Freq={_beepFrequency}, Pitch={_audioPitch}");

            // カスタムサウンドファイルを読み込む（初回はここでデフォルト音が生成される）
            LoadCustomSoundFiles();

            // クリップを事前に用意しておく（曲開始時に読み込みが走らないように）。
            // デフォルト音の生成より後に呼ぶこと。先に呼ぶと、まだファイルが無い状態で
            // 「読み込み失敗→ビープ」がキャッシュされてしまう
            SoundService.Prewarm();
        }

        private void Start()
        {
            Plugin.LogInfo("WallHitSoundViewController started");
            AddGameplayTabIfNeeded();
        }

        // BSMLのパース完了後に確実に初期値をUIへ反映
        [UIAction("#post-parse")]
        private void OnPostParse()
        {
            Plugin.LogInfo("WallHitSoundViewController OnPostParse - syncing UI components");
            // UI コンポーネントの初期同期
            if (_enabledToggle != null) { _enabledToggle.Value = _enabled; _enabledToggle.ReceiveValue(); }
            if (_volumeSlider != null) { _volumeSlider.Value = _volume; _volumeSlider.ReceiveValue(); }
            if (_soundDropdown != null) { _soundDropdown.Value = _selectedSound; _soundDropdown.ReceiveValue(); }
            if (_beepSlider != null) { _beepSlider.Value = _beepFrequency; _beepSlider.ReceiveValue(); }
            if (_pitchSlider != null) { _pitchSlider.Value = _audioPitch; _pitchSlider.ReceiveValue(); }
            if (_particleSlider != null) { _particleSlider.Value = _particleCount; _particleSlider.ReceiveValue(); }
        }

        private void OnEnable()
        {
            Plugin.LogInfo("WallHitSoundViewController OnEnable");
        }

        private void OnDestroy()
        {
            Plugin.LogInfo("WallHitSoundViewController destroyed");
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
                    Plugin.LogInfo($"WallHitSound: Created UserData/WallHitSound directory: {userDataPath}");
                }

                // デフォルトサウンドをセットアップ。
                // 毎回呼ぶのは、後から種類を増やしたときに既存ユーザーにも届くようにするため。
                // 既にあるファイルはスキップされるので、差し替えた音が上書きされることはない
                Services.DefaultSoundInitializer.InitializeDefaultSounds(userDataPath);

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

                Plugin.LogInfo($"WallHitSound: Loaded {audioFiles.Count} custom sound files from {userDataPath}");
                foreach (var file in audioFiles)
                {
                    Plugin.LogDebug($"  - {file}");
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
                GameplaySetup.Instance.AddTab("WallHitSound", "WallHitSound.UI.Settings.bsml", this, MenuType.All);
                _tabAdded = true;
                Plugin.LogInfo("WallHitSoundViewController: added GameplaySetup tab 'WallHitSound'");
            }
            catch (Exception ex)
            {
                Plugin.LogWarn($"WallHitSoundViewController: AddGameplayTab failed: {ex.Message}");
            }
        }

        // ここは不要 v1.0.1
        // /// <summary>
        // /// LateUpdate で GameplaySetup が利用可能になるのを待つ。
        // /// </summary>
        // private void LateUpdate()
        // {
        //     if (!_tabAdded && GameplaySetup.Instance != null)
        //     {
        //         AddGameplayTabIfNeeded();
        //     }
        // }

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
                    Plugin.LogInfo($"WallHitSound: Enabled changed to {value}");
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
                    Plugin.LogInfo($"WallHitSound: Volume changed to {value}");
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
                    Plugin.LogInfo($"WallHitSound: SelectedSound changed to {value}");
                    NotifyUIValueChangedSelectedSound();
                    NotifyUIValueChangedIsBeepSelected();

                    // サウンド選択変更時に新しいクリップを用意しておく。
                    // カスタム音の読み込みは非同期なのでここでフレームは飛ばない。
                    // 設定画面で済ませておくことで、曲開始時に読み込みが走るのを避ける
                    try
                    {
                        SoundService.ReloadSound();
                        Plugin.LogInfo($"WallHitSound: Sound reloaded successfully: {value}");
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
                    Plugin.LogInfo($"WallHitSound: BeepFrequency changed to {value}");
                    NotifyUIValueChangedBeepFrequency();

                    // 新しい周波数でビープ音を作り直しておく
                    try
                    {
                        SoundService.ReloadSound();
                        // カスタム音を選んでいる場合、鳴る音自体は周波数では変わらない
                        Plugin.LogInfo($"WallHitSound: Beep frequency applied: {value}");
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogWarn($"WallHitSound: Failed to reload sound: {ex.Message}");
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
                    Plugin.LogInfo($"WallHitSound: AudioPitch changed to {value}");
                    NotifyUIValueChangedAudioPitch();

                    // Apply pitch immediately
                    try
                    {
                        SoundService.SetPitch(value);
                        Plugin.LogInfo($"WallHitSound: Pitch applied: {value}");
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogWarn($"WallHitSound: Failed to apply pitch: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 衝突時のパーティクル数（0で無効）。
        /// </summary>
        [UIValue("particle-count")]
        public float ParticleCount
        {
            get => _particleCount;
            set
            {
                if (Math.Abs(_particleCount - value) > 0.0001f)
                {
                    _particleCount = Mathf.Clamp(value, 0f, 200f);
                    PluginConfig.Instance.ParticleCount = Mathf.RoundToInt(_particleCount);
                    Plugin.LogInfo($"WallHitSound: ParticleCount changed to {PluginConfig.Instance.ParticleCount}");
                }
            }
        }

        /// <summary>
        /// 現在の設定で音を再生するテストメソッド。
        /// 実際の再生と同じサービスを通すので、鳴る音はプレイ中とまったく同じになる。
        /// </summary>
        [UIAction("test-sound")]
        public void TestSound()
        {
            Plugin.LogInfo("WallHitSound: ===== TEST SOUND START =====");
            try
            {
                Plugin.LogInfo($"WallHitSound: Playing test sound - Sound={_selectedSound}, Volume={_volume}, Pitch={_audioPitch}");
                SoundService.PlaySound();
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"WallHitSound: TestSound failed: {ex.Message}\n{ex.StackTrace}");
            }
            Plugin.LogInfo("WallHitSound: ===== TEST SOUND END =====");
        }

        /// <summary>
        /// すべてのスライダーをデフォルト値にリセットする。
        /// </summary>
        [UIAction("reset-settings")]
        public void ResetSettings()
        {
            Plugin.LogInfo("WallHitSound: ===== RESET SETTINGS START =====");

            // デフォルト値を設定
            Enabled = true;
            Volume = 1.0f;
            SelectedSound = "beep";
            BeepFrequency = 1000f;
            AudioPitch = 1.0f;
            ParticleCount = 0f;

            // UIコンポーネントへ直接反映
            if (_enabledToggle != null) { _enabledToggle.Value = _enabled; _enabledToggle.ReceiveValue(); }
            if (_volumeSlider != null) { _volumeSlider.Value = _volume; _volumeSlider.ReceiveValue(); }
            if (_soundDropdown != null) { _soundDropdown.Value = _selectedSound; _soundDropdown.ReceiveValue(); }
            if (_beepSlider != null) { _beepSlider.Value = _beepFrequency; _beepSlider.ReceiveValue(); }
            if (_pitchSlider != null) { _pitchSlider.Value = _audioPitch; _pitchSlider.ReceiveValue(); }
            if (_particleSlider != null) { _particleSlider.Value = _particleCount; _particleSlider.ReceiveValue(); }

            // タブ再構築は行わない（レイアウト崩れ回避）。通知のみで反映。
            Plugin.LogInfo("WallHitSound: ===== RESET SETTINGS END =====");
        }

        /// <summary>
        /// 小数点なしでフォーマットした表示文字列を返す
        /// </summary>
        [UIAction("FormatNoDecimal")]
        public string FormatNoDecimal(float value)
        {
            return value.ToString("F0"); // 小数点なしで表示
        }
    }
}
