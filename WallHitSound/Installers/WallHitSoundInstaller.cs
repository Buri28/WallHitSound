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
            // （AudioSource・クリップは static で常駐するので、スコープ破棄時の後始末は不要）
            Container.Bind<Services.WallHitSoundService>().AsSingle();

            // Manager をインスタンス化（ObstacleMonitor は内部で処理）。
            // InstantiateComponentOnNewGameObject は install の途中で注入まで走らせてしまい、
            // その時点ではゲーム側の binding が揃っていないため、依存の解決に失敗すると
            // シーンの install ごと巻き添えで落ちる（画面が真っ黒になる）。
            // NonLazy にして、すべての installer が終わったあとに作らせる。
            Container.Bind<Services.WallHitSoundManager>()
                     .FromNewComponentOnNewGameObject()
                     .AsSingle()
                     .NonLazy();

            // プレイヤースコープに入ったのでゲームプレイ状態フラグを立てる
            // メニューの設定画面でテストサウンドを鳴らすために
            // メニュースコープにもWallHitSoundManagerがインストールされているため
            // フラグで状態を区別する必要がある
            Plugin.IsInGameplay = true;
        }
    }
}
