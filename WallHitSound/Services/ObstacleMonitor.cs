using UnityEngine;

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
            if (!previousFrameInObstacle && current)
            {
                service.PlaySound();
            }
            previousFrameInObstacle = current;
        }
    }
}