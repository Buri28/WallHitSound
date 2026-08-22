using System;
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

        // 曲開始時にシーン全体を走査する FindObjectOfType を避けるため、Zenject から受け取る。
        // ただし install の最中に解決しようとしてはいけない。その時点ではゲーム側の binding が
        // 揃っておらず、PlayerHeadAndObstacleInteraction の組み立てに失敗すると
        // シーンの install ごと巻き添えで落ちる。コンテナだけ先に受け取り、
        // すべて揃った Start で解決する
        private DiContainer container;

        [Inject]
        public void Construct(WallHitSoundService service, DiContainer diContainer)
        {
            SoundService = service;
            container = diContainer;
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

            // プレイヤーの障害物との衝突判定を取得
            obstacleInteraction = ResolveObstacleInteraction();
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

        /// <summary>
        /// 障害物との衝突判定を取り出す。コンテナから取れなければシーン走査に落とす。
        /// 解決に失敗しても曲を巻き添えにしないよう、例外はここで止める。
        /// </summary>
        private PlayerHeadAndObstacleInteraction ResolveObstacleInteraction()
        {
            try
            {
                var resolved = container?.TryResolve<PlayerHeadAndObstacleInteraction>();
                if (resolved != null) return resolved;
            }
            catch (Exception ex)
            {
                // 注入が効かなくなったことに気づけるよう、この警告はログ抑制の対象外にする
                Plugin.Log?.Warn($"WallHitSound: Failed to resolve PlayerHeadAndObstacleInteraction: {ex.Message}");
            }

            Plugin.Log?.Warn("WallHitSound: PlayerHeadAndObstacleInteraction was not resolved, falling back to FindObjectOfType");
            return UnityEngine.Object.FindObjectOfType<PlayerHeadAndObstacleInteraction>();
        }

        private void OnDestroy()
        {
            // AudioSource とクリップはアプリ全体で常駐し曲をまたいで使い回すので、
            // ここでは参照を外すだけ（破棄すると次の曲で作り直すコストがかかる）
            SoundService = null;
            container = null;

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
