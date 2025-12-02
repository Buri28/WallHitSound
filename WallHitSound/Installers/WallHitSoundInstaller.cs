using Zenject;

namespace WallHitSound.Installers
{
    /// <summary>
    /// プレイヤースコープの Zenject インストーラー。
    /// WallHitSoundService と WallHitSoundManager をバインドする。
    /// </summary>
    public class WallHitSoundInstaller : Installer
    {
        /// <summary>Zenject バインディングを設定する。</summary>
        public override void InstallBindings()
        {
            // プレイヤースコープ用のサービスをバインド
            Container.BindInterfacesAndSelfTo<Services.WallHitSoundService>().AsSingle();

            // Manager をインスタンス化（ObstacleMonitor は内部で処理）
            Container.InstantiateComponentOnNewGameObject<Services.WallHitSoundManager>();
        }
    }
}
