using UnityEngine;
using WallHitSound.Services.Effects;

namespace WallHitSound.Services
{
    /// <summary>
    /// プレイヤーの頭部が障害物に入ったかどうかを監視し、
    /// 接触開始を検出したら音声サービスに通知する MonoBehaviour。
    /// </summary>
    public class ObstacleMonitor : MonoBehaviour
    {
        private WallHitSoundService service;
        private PlayerHeadAndObstacleInteraction interaction;
        private bool previousFrameInObstacle = false;

        /// <summary>
        /// サービスとプレイヤーのインタラクション参照で初期化する。
        /// </summary>
        public void Initialize(WallHitSoundService svc, PlayerHeadAndObstacleInteraction interactionRef)
        {
            service = svc;
            interaction = interactionRef;
        }

        /// <summary>
        /// 毎フレーム呼ばれる監視処理。接触の立ち上がりを検出して通知する。
        /// </summary>
        private void LateUpdate()
        {
            if (service == null || interaction == null) return;
            bool current = interaction.playerHeadIsInObstacle;

            // プラグインの有効フラグを確認（デフォルトは有効扱い）
            bool pluginActive = PluginConfig.Instance?.Enabled ?? true;

            // ゲームプレイ中でプラグインが無効なら自動トリガーをスキップする。
            // ただし previousFrameInObstacle は常に更新しておくことで、
            // ゲームプレイから復帰した際に誤検出（立ち上がり）を防ぐ。
            if (Plugin.IsInGameplay && !pluginActive)
            {
                previousFrameInObstacle = current;
                return;
            }

            if (!previousFrameInObstacle && current)
            {
                service.PlaySound();

                // 見た目のエフェクト。位置決めと種類の振り分けはサービス側で行う
                // （形も GameObject も作り置き済みなので、ここでは表示するだけ）
                HitEffectService.Play();
            }
            previousFrameInObstacle = current;
        }
    }
}
