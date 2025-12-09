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
        /// プラグイン初期化処理。コンフィグ生成と Zenject インストーラー登録。
        /// </summary>
        [Init]
        public void Init(IPA.Logging.Logger logger, IPA.Config.Config conf, Zenjector zenjector)
        {
            Instance = this;
            Log = logger;

            // コンフィグを生成
            PluginConfig.Instance = conf.Generated<PluginConfig>();
            Log?.Info("WallHitSound: Config generated");

            // プレイヤースコープインストーラーを登録
            zenjector.Install<Installers.WallHitSoundInstaller>(Location.Player);
            Log?.Info("WallHitSound: Player installer registered");

            // メニュースコープインストーラーを登録
            zenjector.Install<Installers.WallHitSoundMenuInstaller>(Location.Menu);
            Log?.Info("WallHitSound: Menu installer registered");

            Log?.Info("WallHitSound: Init complete");
        }
    }
}
