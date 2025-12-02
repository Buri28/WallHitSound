using BeatSaberMarkupLanguage.Attributes;
using System.Collections.Generic;

namespace WallHitSound.UI
{
    /// <summary>
    /// WallHitSound プラグイン設定コントローラー。
    /// Beat Saber のソロ → Mods メニューに表示される設定画面を管理する。
    /// </summary>
    public class SettingController
    {
        /// <summary>シングルトンインスタンス</summary>
        public static SettingController Instance { get; } = new SettingController();

        /// <summary>プラグイン有効/無効</summary>
        [UIValue("enabled")]
        public bool Enabled
        {
            get => PluginConfig.Instance.Enabled;
            set => PluginConfig.Instance.Enabled = value;
        }

        /// <summary>音量（0～1）</summary>
        [UIValue("volume")]
        public float Volume
        {
            get => PluginConfig.Instance.Volume;
            set => PluginConfig.Instance.Volume = value;
        }

        /// <summary>利用可能なサウンドオプション一覧</summary>
        [UIValue("sound-options")]
        public List<object> SoundOptions => new List<object>
        {
            "beep",
            "custom"
        };

        /// <summary>選択されたサウンド</summary>
        [UIValue("selected-sound")]
        public string SelectedSound
        {
            get => PluginConfig.Instance.SelectedClipName ?? "beep";
            set => PluginConfig.Instance.SelectedClipName = value;
        }
    }
}
