using Zenject;
using UnityEngine;

namespace WallHitSound.Installers
{
    /// <summary>
    /// メニュースコープの Zenject インストーラー。
    /// ゲームプレイ設定画面用の ViewController と Menu 用の WallHitSoundService をバインドする。
    /// </summary>
    public class WallHitSoundMenuInstaller : Installer
    {
        /// <summary>Zenject バインディングを設定する。</summary>
        public override void InstallBindings()
        {
            // Menu scope 用の WallHitSoundService をバインド
            Container.Bind<Services.WallHitSoundService>().AsSingle();
            Plugin.Log?.Info("WallHitSoundMenuInstaller: WallHitSoundService bound to Menu scope");

            // ViewController を新しい GameObject に追加
            var gameObject = new GameObject("WallHitSoundViewController");
            gameObject.SetActive(true);
            Container.Bind<UI.WallHitSoundViewController>()
                .FromInstance(gameObject.AddComponent<UI.WallHitSoundViewController>())
                .AsSingle();

            Plugin.Log?.Info("WallHitSoundMenuInstaller: WallHitSoundViewController instantiated and bound");
        }
    }
}
