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

                // パーティクル表示（設定が0なら無効）
                int count = PluginConfig.Instance?.ParticleCount ?? 0;
                if (count > 0)
                {
                    // 物理APIが参照されていない環境もあるため、低負荷な前方オフセットにフォールバック
                    // セイバーではなく頭（HMD）位置基準にする
                    Vector3 origin = (Camera.main != null)
                        ? Camera.main.transform.position
                        : this.transform.position;
                    Vector3 forward = (Camera.main != null)
                        ? Camera.main.transform.forward
                        : this.transform.forward;
                    // 少し奥＋少し上にオフセットして、頭の高さ付近で視認しやすく表示
                    float forwardOffset = 0.50f; // 0.18〜0.50で調整可
                    float upOffset = 0.06f;      // 0.05〜0.12で調整可
                    Vector3 spawnPos = origin + forward * forwardOffset + Vector3.up * upOffset;
                    ParticleEffectService.SpawnHemisphere(spawnPos, count, new Color(1.0f, 0.15f, 0.15f));
                }
            }
            previousFrameInObstacle = current;
        }
    }
}