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

        // 曲開始時にシーン全体を走査する FindObjectOfType を避けるため、Zenject の注入で受け取る。
        // Beat Saber のバージョンによってはバインドされていない可能性があるので Optional にし、
        // 取れなかった場合だけ従来どおり検索へフォールバックする
        [Inject(Optional = true)] private PlayerHeadAndObstacleInteraction injectedObstacleInteraction = null;

        [Inject]
        public void Construct(WallHitSoundService service)
        {
            SoundService = service;
        }

        private void Awake()
        {
            Plugin.LogInfo("WallHitSound: WallHitSoundManager Awake");
            // シングルトンパターンの実装
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void Start()
        {
            // 音声サービスを初期化
            SoundService.Initialize();

            // プレイヤーの障害物との衝突判定を取得（注入で取れていればシーン走査はしない）
            obstacleInteraction = injectedObstacleInteraction;
            if (obstacleInteraction == null)
            {
                // 注入が効かなくなったことに気づけるよう、この警告はログ抑制の対象外にする
                Plugin.LogWarn("WallHitSound: PlayerHeadAndObstacleInteraction was not injected, falling back to FindObjectOfType");
                obstacleInteraction = UnityEngine.Object.FindObjectOfType<PlayerHeadAndObstacleInteraction>();
            }
            if (obstacleInteraction == null)
            {
                Plugin.Log?.Error("WallHitSound: PlayerHeadAndObstacleInteraction not found");
                return;
            }

            // 障害物監視システムをセットアップ
            obstacleMonitor = gameObject.AddComponent<ObstacleMonitor>();
            obstacleMonitor.Initialize(SoundService, obstacleInteraction);

            Plugin.LogInfo("WallHitSound: Manager initialized successfully");
        }

        private void OnDestroy()
        {
            // AudioSource とクリップはアプリ全体で常駐し曲をまたいで使い回すので、
            // ここでは参照を外すだけ（破棄すると次の曲で作り直すコストがかかる）
            SoundService = null;

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
