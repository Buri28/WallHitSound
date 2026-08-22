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

        /// <summary>壁衝突時のパーティクル数（0で無効、デフォルト: 0）。エフェクトが "spark" のときだけ使う</summary>
        public virtual int ParticleCount { get; set; } = 0;

        /// <summary>
        /// 壁衝突時のエフェクト種類（"none" / "spark" / "burst" / "crack"、デフォルト: "spark"）。
        /// 既定を "spark" にしているのは、これまでの挙動（ParticleCount で火花を出す）を
        /// そのまま引き継ぐため。
        /// </summary>
        public virtual string EffectType { get; set; } = "spark";

        /// <summary>エフェクトの大きさ倍率（0.3～2.0、デフォルト: 1.0）</summary>
        public virtual float EffectScale { get; set; } = 1.0f;

        /// <summary>エフェクトの濃さ（0.2～1.0、デフォルト: 1.0）</summary>
        public virtual float EffectOpacity { get; set; } = 1.0f;

        /// <summary>バーストの塗り色の既定値</summary>
        public const string DefaultBurstFillColor = "#FFD21E";
        /// <summary>バーストの縁の色の既定値</summary>
        public const string DefaultBurstEdgeColor = "#E01B24";
        /// <summary>ひび割れの線の色の既定値</summary>
        public const string DefaultCrackColor = "#EFF3FA";

        /// <summary>バーストの塗り色（#RRGGBB）</summary>
        public virtual string BurstFillColor { get; set; } = DefaultBurstFillColor;

        /// <summary>バーストの縁の色（#RRGGBB）</summary>
        public virtual string BurstEdgeColor { get; set; } = DefaultBurstEdgeColor;

        /// <summary>ひび割れの線の色（#RRGGBB）</summary>
        public virtual string CrackColor { get; set; } = DefaultCrackColor;
    }
}
