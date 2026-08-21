using System;
using System.Runtime.CompilerServices;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using SiraUtil.Zenject;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace WallHitSound
{
    /// <summary>
    /// WallHitSoundプラグインのエントリーポイント。
    /// BSIPA の初期化と Zenject インストーラー登録を管理する。
    /// </summary>
    [Plugin(RuntimeOptions.DynamicInit)]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; }
        public static IPA.Logging.Logger Log { get; private set; }
        // ゲームプレイ中かどうかを示すフラグ。
        // インストーラーやシーン遷移ハンドラで切り替える。
        public static bool IsInGameplay { get; set; } = false;

        /// <summary>
        /// 詳細ログを出すかどうか。方針としてエラー以外は出さないので通常は false。
        /// 調査したいときだけ true にしてビルドし直す。
        /// </summary>
        public static readonly bool VerboseLogs = false;

        /// <summary>詳細ログ（VerboseLogs が true のときだけ出力）。</summary>
        public static void LogDebug(string msg) { if (VerboseLogs) Log?.Debug(msg); }
        /// <summary>詳細ログ（VerboseLogs が true のときだけ出力）。</summary>
        public static void LogInfo(string msg) { if (VerboseLogs) Log?.Info(msg); }
        /// <summary>警告ログ（VerboseLogs が true のときだけ出力）。動作は継続できている事象に使う。</summary>
        public static void LogWarn(string msg) { if (VerboseLogs) Log?.Warn(msg); }

        /// <summary>
        /// プラグイン初期化処理。コンフィグ生成と Zenject インストーラー登録。
        /// </summary>
        [Init]
        public void Init(IPA.Logging.Logger logger, IPA.Config.Config conf, Zenjector zenjector)
        {
            Instance = this;
            Log = logger;

            // コンフィグを生成
            PluginConfig.Instance = conf.Generated<PluginConfig>();
            LogInfo("WallHitSound: Config generated");

            // プレイヤースコープインストーラーを登録
            zenjector.Install<Installers.WallHitSoundInstaller>(Location.Player);
            LogInfo("WallHitSound: Player installer registered");

            // メニュースコープインストーラーを登録
            zenjector.Install<Installers.WallHitSoundMenuInstaller>(Location.Menu);
            LogInfo("WallHitSound: Menu installer registered");

            LogInfo("WallHitSound: Init complete");
        }
    }
}
