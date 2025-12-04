using UnityEngine;
using GlobalEnums;
using BepInEx.Logging;

namespace SilksongAccess.Gameplay
{
    /// <summary>
    /// Provides auto-aim functionality for attacks.
    /// When enabled, attacks will automatically face the closest on-screen enemy.
    /// </summary>
    public static class AutoAimAccessibility
    {
        private static ManualLogSource _logger;

        /// <summary>
        /// Toggle to enable/disable auto-aim attacks toward closest enemy.
        /// When enabled, attacks will automatically face the closest on-screen enemy.
        /// </summary>
        public static bool EnableAutoAimAttacks = false;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
            _logger?.LogInfo("AutoAimAccessibility initialized.");
        }

        /// <summary>
        /// Finds the closest valid on-screen enemy to the player.
        /// Returns null if no enemies are on screen.
        /// </summary>
        public static HealthManager GetClosestOnScreenEnemy()
        {
            if (HeroController.instance == null) return null;

            Component heroComponent = HeroController.instance as Component;
            if (heroComponent == null) return null;
            Vector2 heroPos = heroComponent.transform.position;

            Camera mainCam = GetMainCamera();
            if (mainCam == null) return null;

            HealthManager closestEnemy = null;
            float closestDistance = float.MaxValue;

            HealthManager[] allHealthManagers = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None);

            foreach (var hm in allHealthManagers)
            {
                // Validate enemy
                if (hm == null || hm.gameObject == null) continue;
                if (!hm.gameObject.activeInHierarchy) continue;
                if (hm.hp <= 0) continue;
                if (hm.gameObject.layer != (int)PhysLayers.ENEMIES) continue;

                Component healthComponent = hm as Component;
                if (healthComponent == null) continue;
                Vector2 enemyPos = healthComponent.transform.position;

                // Check if on screen
                Vector3 viewportPoint = mainCam.WorldToViewportPoint(enemyPos);
                bool isOnScreen = viewportPoint.x >= 0f && viewportPoint.x <= 1f &&
                                  viewportPoint.y >= 0f && viewportPoint.y <= 1f &&
                                  viewportPoint.z > 0;

                if (!isOnScreen) continue;

                // Calculate distance
                float distance = Vector2.Distance(heroPos, enemyPos);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = hm;
                }
            }

            return closestEnemy;
        }

        /// <summary>
        /// Determines the best attack direction to hit the closest enemy.
        /// Also sets the player's facing direction if needed.
        /// </summary>
        /// <param name="originalDirection">The original attack direction from player input</param>
        /// <returns>The modified attack direction to hit the closest enemy</returns>
        public static AttackDirection GetAutoAimAttackDirection(AttackDirection originalDirection)
        {
            if (!EnableAutoAimAttacks) return originalDirection;

            HealthManager closestEnemy = GetClosestOnScreenEnemy();
            if (closestEnemy == null) return originalDirection;

            if (HeroController.instance == null) return originalDirection;

            Component heroComponent = HeroController.instance as Component;
            if (heroComponent == null) return originalDirection;
            Vector2 heroPos = heroComponent.transform.position;

            Component enemyComponent = closestEnemy as Component;
            if (enemyComponent == null) return originalDirection;
            Vector2 enemyPos = enemyComponent.transform.position;

            // Calculate direction to enemy
            Vector2 toEnemy = enemyPos - heroPos;

            // Determine the best attack direction based on angle
            float angle = Mathf.Atan2(toEnemy.y, toEnemy.x) * Mathf.Rad2Deg;

            // Normalize angle to 0-360
            if (angle < 0) angle += 360f;

            // Thresholds for directional attacks:
            // Up: 45 to 135 degrees
            // Down: 225 to 315 degrees
            // Left/Right (normal): everything else

            AttackDirection newDirection;

            if (angle >= 45f && angle < 135f)
            {
                // Enemy is above - attack upward
                newDirection = AttackDirection.upward;
            }
            else if (angle >= 225f && angle < 315f)
            {
                // Enemy is below - attack downward
                newDirection = AttackDirection.downward;
            }
            else
            {
                // Enemy is to the side - normal attack
                newDirection = AttackDirection.normal;

                // Set facing direction toward enemy
                bool shouldFaceRight = toEnemy.x > 0;
                
                // Only change facing if we need to
                if (HeroController.instance.cState.facingRight != shouldFaceRight)
                {
                    // Flip the sprite to face the enemy
                    HeroController.instance.FlipSprite();
                }
            }

            return newDirection;
        }

        /// <summary>
        /// Checks if there are any valid enemies on screen.
        /// </summary>
        public static bool HasOnScreenEnemies()
        {
            return GetClosestOnScreenEnemy() != null;
        }

        /// <summary>
        /// Get the main camera.
        /// </summary>
        private static Camera GetMainCamera()
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
    }
}
