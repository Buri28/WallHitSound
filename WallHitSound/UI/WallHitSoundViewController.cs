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
using WallHitSound.Services.Effects;
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
        [UIComponent("effect-dropdown")] private DropDownListSetting _effectDropdown;
        [UIComponent("effect-scale-slider")] private SliderSetting _effectScaleSlider;
        [UIComponent("effect-opacity-slider")] private SliderSetting _effectOpacitySlider;
#pragma warning restore 0649

        // ローカルバインディング用の変数（UI表示用）
        private bool _enabled = true;
        private float _volume = 1.0f;
        private string _selectedSound = "beep";
        private float _beepFrequency = 1000f;
        private float _audioPitch = 1.0f;
        private float _particleCount = PluginConfig.DefaultParticleCount;
        private string _effectType = PluginConfig.DefaultEffectType;
        private float _effectScale = PluginConfig.DefaultEffectScale;
        private float _effectOpacity = PluginConfig.DefaultEffectOpacity;

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
            _effectType = PluginConfig.Instance.EffectType ?? PluginConfig.DefaultEffectType;
            // 設定ファイルを手で編集して範囲外の値が入っていると、UI（スライダー側で丸められる）と
            // 保存値が食い違うので、読み込んだ時点で範囲に収めておく
            _effectScale = Mathf.Clamp(PluginConfig.Instance.EffectScale, 0.3f, 2.0f);
            _effectOpacity = Mathf.Clamp(PluginConfig.Instance.EffectOpacity, 0.2f, 1.0f);
            PluginConfig.Instance.EffectScale = _effectScale;
            PluginConfig.Instance.EffectOpacity = _effectOpacity;
            Plugin.LogInfo($"WallHitSound: Initialized local fields - Enabled={_enabled}, Volume={_volume}, Sound={_selectedSound}, Freq={_beepFrequency}, Pitch={_audioPitch}");

            // カスタムサウンドファイルを読み込む（初回はここでデフォルト音が生成される）
            LoadCustomSoundFiles();

            // 初回起動時のサウンドを決める。ファイルが揃ったあとでないと判定できない
            ResolveInitialSound();

            // クリップを事前に用意しておく（曲開始時に読み込みが走らないように）。
            // デフォルト音の生成より後に呼ぶこと。先に呼ぶと、まだファイルが無い状態で
            // 「読み込み失敗→ビープ」がキャッシュされてしまう
            SoundService.Prewarm();

            // エフェクトの形と GameObject も同じ理由でここで用意しておく
            HitEffectService.Prewarm();
        }

        private void Start()
        {
            Plugin.LogInfo("WallHitSoundViewController started");
            AddGameplayTabIfNeeded();
        }

        /// <summary>
        /// UI コンポーネントへの反映を 1 つずつ包む。設定ファイルを手で編集した、
        /// あるいは選んでいたカスタム音のファイルを消した場合、ドロップダウンの選択肢に
        /// 無い値が入って ReceiveValue が落ちうるため、1 つ失敗しても残りは反映させる。
        /// </summary>
        private void SyncComponent(string name, Action apply)
        {
            try
            {
                apply();
            }
            catch (Exception ex)
            {
                Plugin.LogWarn($"WallHitSound: Failed to sync '{name}': {ex.Message}");
            }
        }

        // BSMLのパース完了後に確実に初期値をUIへ反映
        [UIAction("#post-parse")]
        private void OnPostParse()
        {
            Plugin.LogInfo("WallHitSoundViewController OnPostParse - syncing UI components");
            // UI コンポーネントの初期同期
            SyncComponent("enabled-toggle", () => { if (_enabledToggle != null) { _enabledToggle.Value = _enabled; _enabledToggle.ReceiveValue(); } });
            SyncComponent("volume-slider", () => { if (_volumeSlider != null) { _volumeSlider.Value = _volume; _volumeSlider.ReceiveValue(); } });
            SyncComponent("sound-dropdown", () => { if (_soundDropdown != null) { _soundDropdown.Value = _selectedSound; _soundDropdown.ReceiveValue(); } });
            SyncComponent("beep-slider", () => { if (_beepSlider != null) { _beepSlider.Value = _beepFrequency; _beepSlider.ReceiveValue(); } });
            SyncComponent("pitch-slider", () => { if (_pitchSlider != null) { _pitchSlider.Value = _audioPitch; _pitchSlider.ReceiveValue(); } });
            SyncComponent("particle-slider", () => { if (_particleSlider != null) { _particleSlider.Value = _particleCount; _particleSlider.ReceiveValue(); } });
            SyncComponent("effect-dropdown", () => { if (_effectDropdown != null) { _effectDropdown.Value = _effectType; _effectDropdown.ReceiveValue(); } });
            SyncComponent("effect-scale-slider", () => { if (_effectScaleSlider != null) { _effectScaleSlider.Value = _effectScale; _effectScaleSlider.ReceiveValue(); } });
            SyncComponent("effect-opacity-slider", () => { if (_effectOpacitySlider != null) { _effectOpacitySlider.Value = _effectOpacity; _effectOpacitySlider.ReceiveValue(); } });
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

        /// <summary>
        /// 初回起動（サウンド未設定）のときに既定のサウンドを決める。
        /// thud があれば thud、無ければ beep。すでに選んでいる人の設定は書き換えない。
        /// </summary>
        private void ResolveInitialSound()
        {
            if (!string.IsNullOrEmpty(PluginConfig.Instance.SelectedClipName)) return;

            string initial = DefaultSoundName();
            _selectedSound = initial;
            PluginConfig.Instance.SelectedClipName = initial;
            Plugin.LogInfo($"WallHitSound: Initial sound set to {initial}");
        }

        /// <summary>
        /// 既定にしたいサウンド名。thud が用意できていればそれ、無ければ beep。
        /// </summary>
        private string DefaultSoundName()
        {
            return _customSoundFiles.Contains(PluginConfig.PreferredSoundName)
                ? PluginConfig.PreferredSoundName
                : PluginConfig.FallbackSoundName;
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

                    // 火花を設定数ぶん用意しておく（曲中に作り足さないため）。
                    // spark 以外を選んでいるときは使わないので作らない
                    // （spark に切り替えた時点で SelectedEffect 側の Prewarm が用意する）
                    try
                    {
                        if (_effectType == HitEffectService.TypeSpark)
                        {
                            ParticleEffectService.Prewarm(PluginConfig.Instance.ParticleCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogWarn($"WallHitSound: Failed to prewarm sparks: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 衝突エフェクトの種類の選択肢。
        /// </summary>
        [UIValue("effect-options")]
        public List<object> EffectOptions => new List<object>
        {
            HitEffectService.TypeNone,
            HitEffectService.TypeSpark,
            HitEffectService.TypeBurst,
            HitEffectService.TypeCrack,
        };

        /// <summary>
        /// 選択された衝突エフェクト。切り替え時に形とプールを用意し直す。
        /// </summary>
        [UIValue("selected-effect")]
        public string SelectedEffect
        {
            get => _effectType;
            set
            {
                if (_effectType != value)
                {
                    _effectType = value;
                    PluginConfig.Instance.EffectType = value;
                    Plugin.LogInfo($"WallHitSound: EffectType changed to {value}");
                    NotifyUI("selected-effect");

                    // 曲開始時にも衝突時にも生成が走らないよう、ここで作り置きしておく
                    try
                    {
                        HitEffectService.Prewarm();
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogWarn($"WallHitSound: Failed to prewarm effects: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// エフェクトの大きさ倍率（0.3～2.0）。
        /// </summary>
        [UIValue("effect-scale")]
        public float EffectScale
        {
            get => _effectScale;
            set
            {
                float clamped = Mathf.Clamp(value, 0.3f, 2.0f);
                if (Math.Abs(_effectScale - clamped) > 0.001f)
                {
                    _effectScale = clamped;
                    PluginConfig.Instance.EffectScale = _effectScale;
                    Plugin.LogInfo($"WallHitSound: EffectScale changed to {_effectScale}");
                    NotifyUI("effect-scale");
                }
            }
        }

        /// <summary>
        /// エフェクトの濃さ（0.2～1.0）。譜面が見づらいときはここを下げる。
        /// </summary>
        [UIValue("effect-opacity")]
        public float EffectOpacity
        {
            get => _effectOpacity;
            set
            {
                float clamped = Mathf.Clamp(value, 0.2f, 1.0f);
                if (Math.Abs(_effectOpacity - clamped) > 0.001f)
                {
                    _effectOpacity = clamped;
                    PluginConfig.Instance.EffectOpacity = _effectOpacity;
                    Plugin.LogInfo($"WallHitSound: EffectOpacity changed to {_effectOpacity}");
                    NotifyUI("effect-opacity");
                }
            }
        }

        /// <summary>
        /// 現在の設定でエフェクトだけを出すテストメソッド。
        /// 実際の衝突と同じ経路を通すので、見え方はプレイ中とまったく同じになる。
        /// </summary>
        [UIAction("test-effect")]
        public void TestEffect()
        {
            try
            {
                Vector3 position;
                if (!EffectCamera.TryGetSpawnPoint(out position))
                {
                    Plugin.LogWarn("WallHitSound: No camera found for the effect test");
                    return;
                }
                HitEffectService.PlayAt(_effectType, position);
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"WallHitSound: TestEffect failed: {ex.Message}");
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
            SelectedSound = DefaultSoundName();
            BeepFrequency = 1000f;
            AudioPitch = 1.0f;
            ParticleCount = PluginConfig.DefaultParticleCount;
            SelectedEffect = PluginConfig.DefaultEffectType;
            EffectScale = PluginConfig.DefaultEffectScale;
            EffectOpacity = PluginConfig.DefaultEffectOpacity;

            // 色は設定画面に項目が無いぶん、ここで戻せないと JSON を直すしかなくなる
            PluginConfig.Instance.BurstFillColor = PluginConfig.DefaultBurstFillColor;
            PluginConfig.Instance.BurstEdgeColor = PluginConfig.DefaultBurstEdgeColor;
            PluginConfig.Instance.CrackColor = PluginConfig.DefaultCrackColor;
            try
            {
                HitEffectService.Prewarm();
            }
            catch (Exception ex)
            {
                Plugin.LogWarn($"WallHitSound: Failed to prewarm effects: {ex.Message}");
            }

            // UIコンポーネントへ直接反映
            SyncComponent("enabled-toggle", () => { if (_enabledToggle != null) { _enabledToggle.Value = _enabled; _enabledToggle.ReceiveValue(); } });
            SyncComponent("volume-slider", () => { if (_volumeSlider != null) { _volumeSlider.Value = _volume; _volumeSlider.ReceiveValue(); } });
            SyncComponent("sound-dropdown", () => { if (_soundDropdown != null) { _soundDropdown.Value = _selectedSound; _soundDropdown.ReceiveValue(); } });
            SyncComponent("beep-slider", () => { if (_beepSlider != null) { _beepSlider.Value = _beepFrequency; _beepSlider.ReceiveValue(); } });
            SyncComponent("pitch-slider", () => { if (_pitchSlider != null) { _pitchSlider.Value = _audioPitch; _pitchSlider.ReceiveValue(); } });
            SyncComponent("particle-slider", () => { if (_particleSlider != null) { _particleSlider.Value = _particleCount; _particleSlider.ReceiveValue(); } });
            SyncComponent("effect-dropdown", () => { if (_effectDropdown != null) { _effectDropdown.Value = _effectType; _effectDropdown.ReceiveValue(); } });
            SyncComponent("effect-scale-slider", () => { if (_effectScaleSlider != null) { _effectScaleSlider.Value = _effectScale; _effectScaleSlider.ReceiveValue(); } });
            SyncComponent("effect-opacity-slider", () => { if (_effectOpacitySlider != null) { _effectOpacitySlider.Value = _effectOpacity; _effectOpacitySlider.ReceiveValue(); } });

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
