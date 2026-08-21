using Zenject;
using UnityEngine;

namespace WallHitSound.Installers
{
    /// <summary>
    /// メニュースコープの Zenject インストーラー。
    /// ゲームプレイ設定画面用の ViewController を生成してバインドする。
    /// </summary>
    public class WallHitSoundMenuInstaller : Installer
    {
        /// <summary>Zenject バインディングを設定する。</summary>
        public override void InstallBindings()
        {
            // ViewController を新しい GameObject に追加。
            // 壁ヒット音のクリップの用意（Prewarm）は、AddComponent で同期的に走る
            // ViewController の Awake が行う（デフォルト音の生成より後に呼ぶ必要があるため）
            var gameObject = new GameObject("WallHitSoundViewController");
            gameObject.SetActive(true);
            Container.Bind<UI.WallHitSoundViewController>()
                .FromInstance(gameObject.AddComponent<UI.WallHitSoundViewController>())
                .AsSingle();

            Plugin.LogInfo("WallHitSoundMenuInstaller: WallHitSoundViewController instantiated and bound");

            // メニュースコープに入ったのでゲームプレイ状態フラグを落とす
            Plugin.IsInGameplay = false;
        }
    }
}
