using IPA.Config.Stores;

namespace WallHitSound
{
    /// <summary>
    /// プラグインの設定を管理するクラス。IPA.Config により自動保存・復元される。
    /// </summary>
    public class PluginConfig
    {
        public static PluginConfig Instance { get; set; }

        /// <summary>プラグイン有効/無効フラグ（デフォルト: 有効）</summary>
        public virtual bool Enabled { get; set; } = true;

        /// <summary>選択されたサウンド名（"beep" またはファイル名、デフォルト: "beep"）</summary>
        public virtual string SelectedClipName { get; set; } = "beep";

        /// <summary>音量設定（0～1、デフォルト: 1.0）</summary>
        public virtual float Volume { get; set; } = 1.0f;

        /// <summary>ビープ音周波数（100～2000Hz、デフォルト: 1000）</summary>
        public virtual float BeepFrequency { get; set; } = 1000f;

        /// <summary>オーディオピッチ（0.5～2.0、デフォルト: 1.0）</summary>
        public virtual float AudioPitch { get; set; } = 1.0f;
    }
}