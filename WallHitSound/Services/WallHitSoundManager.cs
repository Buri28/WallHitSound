using UnityEngine;
using Zenject;

namespace WallHitSound.Services
{
    /// <summary>
    /// プラグイン全体のライフサイクル管理とサービス連携を行う MonoBehaviour。
    /// WallHitSoundService の初期化と ObstacleMonitor の設定を担当する。
    /// </summary>
    public class WallHitSoundManager : MonoBehaviour
    {
        public static WallHitSoundManager Instance { get; private set; }

        public WallHitSoundService SoundService { get; private set; }
        private ObstacleMonitor obstacleMonitor;
        private PlayerHeadAndObstacleInteraction obstacleInteraction;

        [Inject]
        public void Construct(WallHitSoundService service)
        {
            SoundService = service;
        }

        private static bool SuppressGameplayLogs = true;

        private void Awake()
        {
            if (!SuppressGameplayLogs) Plugin.Log?.Info("WallHitSound: WallHitSoundManager Awake");
            // シングルトンパターンの実装
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void Start()
        {
            // 音声サービスを初期化
            // プレイ中の性能負荷軽減のため、Playerスコープではログを抑制
            // プレイ中はサービスのログも抑制
            SoundService.SetLogSuppressed(SuppressGameplayLogs);
            SoundService.Initialize();

            // プレイヤーの障害物との衝突判定を取得
            obstacleInteraction = UnityEngine.Object.FindObjectOfType<PlayerHeadAndObstacleInteraction>();
            if (obstacleInteraction == null)
            {
                if (!SuppressGameplayLogs) Plugin.Log?.Error("WallHitSound: PlayerHeadAndObstacleInteraction not found");
                return;
            }

            // 障害物監視システムをセットアップ
            obstacleMonitor = gameObject.AddComponent<ObstacleMonitor>();
            obstacleMonitor.Initialize(SoundService, obstacleInteraction);

            if (!SuppressGameplayLogs) Plugin.Log?.Info("WallHitSound: Manager initialized successfully");
        }

        private void OnDestroy()
        {
            // リソースをクリーンアップ
            if (SoundService != null)
            {
                SoundService.Dispose();
                SoundService = null;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
