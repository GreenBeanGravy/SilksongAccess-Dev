using System.Collections.Generic;
using UnityEngine;
using GlobalEnums;
using BepInEx.Logging;

namespace SilksongAccess.Audio
{
    /// <summary>
    /// Manages spatial audio pings for nearby enemies.
    /// Enemies emit radar-style beeps based on their screen position:
    /// - Close to player = fast, loud pings
    /// - Edge of screen = slow, quiet pings
    /// - Vertical position relative to player = pitch (above = high, below = low)
    /// - Horizontal position = stereo panning
    /// Audio properties and ping timing update in real-time as enemies move.
    /// </summary>
    public class EnemyAudioPingManager : MonoBehaviour
    {
        private static ManualLogSource _logger;

        // Ping timing (in seconds)
        private const float PING_INTERVAL_CLOSE = 0.1f;   // ~10 beeps/sec when close
        private const float PING_INTERVAL_FAR = 0.5f;    // ~1.3 beeps/sec at edge

        // Distance threshold where max speed kicks in (in viewport units)
        // 0.25 = 25% of screen treated as "close"
        private const float CLOSE_THRESHOLD = 0.15f;

        // Pitch modulation based on vertical position relative to player
        private const float PITCH_BELOW = 0.7f;    // Enemy below player
        private const float PITCH_NEUTRAL = 1.0f;  // Enemy at same level
        private const float PITCH_ABOVE = 1.3f;    // Enemy above player

        // Vertical range for pitch calculation (in viewport units)
        // Smaller = more dramatic pitch changes
        private const float VERTICAL_PITCH_RANGE = 0.3f;

        // Volume modulation based on distance from player
        private const float VOLUME_FAR = 0.001f;     // At screen edge
        private const float VOLUME_CLOSE = 0.85f;  // Close to player

        // Audio clip duration (constant regardless of distance)
        private const float PING_DURATION = 0.1f; // 150ms beep

        // Audio clip
        private static AudioClip pingBeep;

        // State tracking
        private Dictionary<HealthManager, EnemyPingData> trackedEnemies = new Dictionary<HealthManager, EnemyPingData>();

        // Cached audio mixer group for game integration
        private static UnityEngine.Audio.AudioMixerGroup gameSfxMixerGroup;

        /// <summary>
        /// Toggle to enable/disable enemy audio pings.
        /// </summary>
        public static bool EnableEnemyPings = true;

        /// <summary>
        /// Data for tracking an enemy's audio state.
        /// </summary>
        private class EnemyPingData
        {
            public AudioSource audioSource;
            public float nextPingTime;
            public float lastPingTime;      // When the last ping was triggered
            public float currentInterval;   // Current ping interval for real-time adjustment
            public bool isBoss;
            // Cache current audio parameters for real-time updates
            public float currentVolume;
            public float currentPitch;
            public float currentPan;
        }

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        private void Start()
        {
            _logger?.LogInfo("EnemyAudioPingManager: Initializing with viewport-based spatial audio...");

            // Generate a ping beep with fixed duration
            pingBeep = SynthesizedSoundFactory.CreatePingBeep(800f, PING_DURATION);

            TryFindGameAudioMixer();

            _logger?.LogInfo("EnemyAudioPingManager: Initialized successfully.");
        }

        /// <summary>
        /// Try to find the game's audio mixer group for SFX to integrate with game audio.
        /// </summary>
        private void TryFindGameAudioMixer()
        {
            try
            {
                AudioSource[] existingSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
                foreach (var source in existingSources)
                {
                    if (source.outputAudioMixerGroup != null)
                    {
                        string groupName = source.outputAudioMixerGroup.name.ToLower();
                        if (groupName.Contains("sfx") || groupName.Contains("sound") || groupName.Contains("effect"))
                        {
                            gameSfxMixerGroup = source.outputAudioMixerGroup;
                            _logger?.LogInfo($"EnemyAudioPingManager: Found game audio mixer group: {source.outputAudioMixerGroup.name}");
                            break;
                        }
                        if (gameSfxMixerGroup == null)
                        {
                            gameSfxMixerGroup = source.outputAudioMixerGroup;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger?.LogWarning($"EnemyAudioPingManager: Could not find game audio mixer: {ex.Message}");
            }
        }

        private void Update()
        {
            if (!EnableEnemyPings) return;

            // Use GameReadyHelper to avoid spam from GameManager.instance when game isn't ready
            if (!GameReadyHelper.IsGameReady())
            {
                CleanupAllEnemies();
                return;
            }

            if (IsPlayerDead())
            {
                CleanupAllEnemies();
                return;
            }

            UpdateEnemyPings();
        }

        /// <summary>
        /// Check if the player/hero is dead.
        /// </summary>
        private bool IsPlayerDead()
        {
            if (HeroController.instance == null) return true;

            try
            {
                return HeroController.instance.cState.dead;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Main update loop: scan for enemies and update their ping timing.
        /// </summary>
        private void UpdateEnemyPings()
        {
            Component heroComponent = HeroController.instance as Component;
            if (heroComponent == null) return;
            Vector2 heroPos = heroComponent.transform.position;

            Camera mainCam = GetMainCamera();
            if (mainCam == null) return;

            HashSet<HealthManager> currentEnemies = new HashSet<HealthManager>();
            HealthManager[] allHealthManagers = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None);

            foreach (var hm in allHealthManagers)
            {
                if (!IsValidEnemy(hm)) continue;

                Component healthComponent = hm as Component;
                if (healthComponent == null) continue;
                Vector2 enemyPos = healthComponent.transform.position;

                // Only track enemies that are on screen
                if (!IsOnScreen(enemyPos, mainCam)) continue;

                bool isBoss = IsBossEnemy(hm);
                currentEnemies.Add(hm);

                if (!trackedEnemies.ContainsKey(hm))
                {
                    SetupEnemyAudio(hm, healthComponent.gameObject, isBoss);
                }

                // Update audio parameters and ping interval in real-time every frame
                UpdateAudioParameters(hm, enemyPos, heroPos, mainCam);
                
                // Check if it's time to trigger a new ping
                CheckAndTriggerPing(hm);
            }

            List<HealthManager> toRemove = new List<HealthManager>();
            foreach (var kvp in trackedEnemies)
            {
                if (!currentEnemies.Contains(kvp.Key) || kvp.Key == null)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var hm in toRemove)
            {
                CleanupEnemy(hm);
            }
        }

        /// <summary>
        /// Setup audio for an enemy - using 2D audio with manual panning for stronger stereo effect.
        /// </summary>
        private void SetupEnemyAudio(HealthManager hm, GameObject enemyObject, bool isBoss)
        {
            AudioSource audioSource = enemyObject.AddComponent<AudioSource>();

            audioSource.clip = pingBeep;
            audioSource.loop = false;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.0f; // 2D audio - we control panning manually
            audioSource.dopplerLevel = 0f;

            if (gameSfxMixerGroup != null)
            {
                audioSource.outputAudioMixerGroup = gameSfxMixerGroup;
            }

            trackedEnemies[hm] = new EnemyPingData
            {
                audioSource = audioSource,
                nextPingTime = Time.time,
                lastPingTime = Time.time,
                currentInterval = PING_INTERVAL_FAR,
                isBoss = isBoss,
                currentVolume = VOLUME_CLOSE,
                currentPitch = PITCH_NEUTRAL,
                currentPan = 0f
            };

            _logger?.LogInfo("Setup audio for " + (isBoss ? "BOSS" : "enemy") + ": " + enemyObject.name);
        }

        /// <summary>
        /// Update audio parameters (volume, pitch, pan) and ping interval in real-time.
        /// Also dynamically adjusts nextPingTime when interval changes.
        /// </summary>
        private void UpdateAudioParameters(HealthManager hm, Vector2 enemyPos, Vector2 heroPos, Camera camera)
        {
            if (!trackedEnemies.ContainsKey(hm)) return;

            EnemyPingData pingData = trackedEnemies[hm];
            if (pingData.audioSource == null) return;

            // Get positions in viewport space (0,0 = bottom-left, 1,1 = top-right)
            Vector3 enemyViewport = camera.WorldToViewportPoint(enemyPos);
            Vector3 heroViewport = camera.WorldToViewportPoint(heroPos);

            // Calculate distance from PLAYER in viewport space
            float distFromPlayerX = Mathf.Abs(enemyViewport.x - heroViewport.x);
            float distFromPlayerY = Mathf.Abs(enemyViewport.y - heroViewport.y);
            
            // Raw distance in viewport space (0 = on player, ~0.7 = corner)
            float rawDistance = Mathf.Sqrt(distFromPlayerX * distFromPlayerX + distFromPlayerY * distFromPlayerY);
            
            // If within close threshold, treat as "super close" (max speed/volume)
            float effectiveDistance;
            if (rawDistance <= CLOSE_THRESHOLD)
            {
                effectiveDistance = 0f;
            }
            else
            {
                float maxPossibleDistance = 0.7f;
                effectiveDistance = (rawDistance - CLOSE_THRESHOLD) / (maxPossibleDistance - CLOSE_THRESHOLD);
                effectiveDistance = Mathf.Clamp01(effectiveDistance);
            }

            // Apply curve for dramatic falloff
            float distanceCurve = Mathf.Pow(effectiveDistance, 0.7f);

            // VOLUME: Update in real-time
            float volume = Mathf.Lerp(VOLUME_CLOSE, VOLUME_FAR, distanceCurve);
            pingData.currentVolume = volume;
            pingData.audioSource.volume = volume;

            // PING INTERVAL: Calculate new interval and adjust nextPingTime in real-time
            float newInterval = Mathf.Lerp(PING_INTERVAL_CLOSE, PING_INTERVAL_FAR, distanceCurve);
            
            // Dynamically adjust: next ping should happen after newInterval from last ping
            // This makes pings speed up/slow down immediately as enemy moves
            pingData.nextPingTime = pingData.lastPingTime + newInterval;
            pingData.currentInterval = newInterval;

            // PITCH: Based on vertical position relative to PLAYER in viewport space
            float verticalDiff = enemyViewport.y - heroViewport.y;
            float normalizedVertical = Mathf.Clamp(verticalDiff / VERTICAL_PITCH_RANGE, -1f, 1f);
            
            float pitch;
            if (normalizedVertical >= 0)
            {
                pitch = Mathf.Lerp(PITCH_NEUTRAL, PITCH_ABOVE, normalizedVertical);
            }
            else
            {
                pitch = Mathf.Lerp(PITCH_NEUTRAL, PITCH_BELOW, -normalizedVertical);
            }
            pingData.currentPitch = pitch;
            pingData.audioSource.pitch = pitch;

            // PANNING: Based on horizontal VIEWPORT position - update in real-time
            float pan = (enemyViewport.x - 0.5f) * 2f;
            pan = Mathf.Sign(pan) * Mathf.Pow(Mathf.Abs(pan), 0.6f);
            pan = Mathf.Clamp(pan, -1f, 1f);
            pingData.currentPan = pan;
            pingData.audioSource.panStereo = pan;
        }

        /// <summary>
        /// Check if it's time to trigger a new ping.
        /// </summary>
        private void CheckAndTriggerPing(HealthManager hm)
        {
            if (!trackedEnemies.ContainsKey(hm)) return;

            EnemyPingData pingData = trackedEnemies[hm];
            if (pingData.audioSource == null) return;

            // Time to ping?
            if (Time.time >= pingData.nextPingTime)
            {
                if (pingData.currentVolume > 0.02f)
                {
                    // Play with current real-time parameters already set on AudioSource
                    pingData.audioSource.Play();
                }

                // Record when this ping happened
                pingData.lastPingTime = Time.time;
                // Schedule next ping using current interval
                pingData.nextPingTime = Time.time + pingData.currentInterval;
            }
        }

        /// <summary>
        /// Check if an enemy is valid for tracking.
        /// </summary>
        private bool IsValidEnemy(HealthManager hm)
        {
            if (hm == null || hm.gameObject == null) return false;
            if (!hm.gameObject.activeInHierarchy) return false;
            if (hm.hp <= 0) return false;
            if (hm.gameObject.layer != (int)PhysLayers.ENEMIES) return false;

            return true;
        }

        /// <summary>
        /// Check if an enemy is a boss.
        /// </summary>
        private bool IsBossEnemy(HealthManager hm)
        {
            if (BossSceneController.Instance == null) return false;
            if (BossSceneController.Instance.bosses == null) return false;

            foreach (var boss in BossSceneController.Instance.bosses)
            {
                if (boss == hm) return true;
            }

            return false;
        }

        /// <summary>
        /// Check if a position is visible on screen.
        /// </summary>
        private bool IsOnScreen(Vector2 worldPos, Camera camera)
        {
            if (camera == null) return false;

            Vector3 viewportPoint = camera.WorldToViewportPoint(worldPos);

            return viewportPoint.x >= 0f && viewportPoint.x <= 1f &&
                   viewportPoint.y >= 0f && viewportPoint.y <= 1f &&
                   viewportPoint.z > 0;
        }

        /// <summary>
        /// Get the main camera.
        /// </summary>
        private Camera GetMainCamera()
        {
            try
            {
                if (GameCameras.instance != null)
                {
                    return GameCameras.instance.mainCamera;
                }
            }
            catch
            {
                // GameCameras not ready yet
            }
            return Camera.main;
        }

        /// <summary>
        /// Cleanup audio for a specific enemy.
        /// </summary>
        private void CleanupEnemy(HealthManager hm)
        {
            if (trackedEnemies.ContainsKey(hm))
            {
                EnemyPingData pingData = trackedEnemies[hm];
                if (pingData.audioSource != null)
                {
                    Destroy(pingData.audioSource);
                }
                trackedEnemies.Remove(hm);
            }
        }

        /// <summary>
        /// Cleanup all tracked enemies.
        /// </summary>
        private void CleanupAllEnemies()
        {
            foreach (var kvp in trackedEnemies)
            {
                if (kvp.Value.audioSource != null)
                {
                    Destroy(kvp.Value.audioSource);
                }
            }
            trackedEnemies.Clear();
        }

        private void OnDestroy()
        {
            CleanupAllEnemies();
        }
    }
}
