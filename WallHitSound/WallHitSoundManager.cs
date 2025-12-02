using UnityEngine;
using Zenject;

namespace WallHitSound
{
    public class WallHitSoundManager : MonoBehaviour
    {
        public static WallHitSoundManager Instance { get; private set; }

        public Services.WallHitSoundService SoundService { get; private set; }
        private Services.ObstacleMonitor obstacleMonitor;
        private PlayerHeadAndObstacleInteraction obstacleInteraction;

        [Inject]
        public void Construct(Services.WallHitSoundService service)
        {
            SoundService = service;
        }

        private void Awake()
        {
            Plugin.Log?.Info("WallHitSound: WallHitSoundManager Awake");
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void Start()
        {
            try
            {
                // Initialize sound service
                SoundService.Initialize();

                // Find PlayerHeadAndObstacleInteraction
                obstacleInteraction = UnityEngine.Object.FindObjectOfType<PlayerHeadAndObstacleInteraction>();
                if (obstacleInteraction == null)
                {
                    Plugin.Log?.Error("WallHitSound: PlayerHeadAndObstacleInteraction not found");
                    return;
                }

                // Create obstacle monitor as a component on this GameObject
                obstacleMonitor = gameObject.AddComponent<Services.ObstacleMonitor>();
                obstacleMonitor.Initialize(SoundService, obstacleInteraction);

                Plugin.Log?.Info("WallHitSound: Manager initialized successfully");
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.Error($"WallHitSound Manager initialization error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void OnDestroy()
        {
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
