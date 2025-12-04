namespace SilksongAccess
{
    /// <summary>
    /// Helper class to check if the game is ready for gameplay features.
    /// Use this to gate any code that accesses GameManager, HeroController, etc.
    /// </summary>
    public static class GameReadyHelper
    {
        private static bool _cachedReady = false;
        private static float _lastCheckTime = 0f;
        private const float CHECK_INTERVAL = 0.5f; // Only check every 0.5 seconds when not ready

        /// <summary>
        /// Returns true if the game is fully ready for gameplay features.
        /// This means GameManager exists, game is in PLAYING state, and HeroController exists.
        /// Caches the result to avoid spamming GameManager.instance which logs errors.
        /// </summary>
        public static bool IsGameReady()
        {
            // If we were ready before, do a quick check
            if (_cachedReady)
            {
                return QuickCheck();
            }

            // If not ready, only check periodically to avoid spam
            float currentTime = UnityEngine.Time.time;
            if (currentTime - _lastCheckTime < CHECK_INTERVAL)
            {
                return false;
            }
            _lastCheckTime = currentTime;

            // Do the full check
            return FullCheck();
        }

        /// <summary>
        /// Quick check when we were previously ready - just verify we still are.
        /// </summary>
        private static bool QuickCheck()
        {
            try
            {
                // Quick null checks without triggering property getters that log errors
                if (GameManager.instance == null)
                {
                    _cachedReady = false;
                    return false;
                }

                if (GameManager.instance.GameState != GlobalEnums.GameState.PLAYING)
                {
                    _cachedReady = false;
                    return false;
                }

                if (HeroController.instance == null)
                {
                    _cachedReady = false;
                    return false;
                }

                return true;
            }
            catch
            {
                _cachedReady = false;
                return false;
            }
        }

        /// <summary>
        /// Full check when we haven't been ready - wrapped in try-catch to avoid error spam.
        /// </summary>
        private static bool FullCheck()
        {
            try
            {
                var gm = GameManager.instance;
                if (gm == null) return false;

                if (gm.GameState != GlobalEnums.GameState.PLAYING) return false;

                if (HeroController.instance == null) return false;

                // If we got here, we're ready!
                _cachedReady = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Call this when the game state changes (e.g., scene load, death, etc.)
        /// to reset the cached state.
        /// </summary>
        public static void Reset()
        {
            _cachedReady = false;
            _lastCheckTime = 0f;
        }
    }
}
