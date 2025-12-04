using UnityEngine;

namespace SilksongAccess.Audio
{
    /// <summary>
    /// Handles spatial audio positioning for enemy pings.
    /// Calculates stereo pan, pitch, and volume based on enemy position relative to player.
    /// Based on TerrariaAccess implementation.
    /// </summary>
    public static class SpatialAudioPanner
    {
        // Configuration constants (tuned to match TerrariaAccess)
        private const float PAN_SCALE_PIXELS = 520f;    // Horizontal range for full stereo spread
        private const float PITCH_SCALE_PIXELS = 320f;  // Vertical range for pitch variation
        private const float PITCH_RANGE = 0.8f;         // Max pitch shift (+/- 0.8)

        // Volume attenuation settings
        private const float VOLUME_MIN = 0.3f;          // Minimum volume at max distance
        private const float VOLUME_SCALE = 0.7f;        // Scale factor for distance attenuation
        private const float ATTENUATION_EXPONENT = 1.1f; // Exponential falloff curve

        /// <summary>
        /// Result of spatial audio calculations.
        /// </summary>
        public struct SpatialAudioData
        {
            public float pan;       // Stereo pan (-1 left to 1 right)
            public float pitch;     // Pitch shift (-0.8 to 0.8, add to 1.0 for final pitch)
            public float volume;    // Final volume (0 to 1)
            public bool isMuffled;  // True if obstructed by wall
        }

        /// <summary>
        /// Calculate all spatial audio parameters for an enemy relative to the player.
        /// </summary>
        /// <param name="playerPos">Player world position</param>
        /// <param name="enemyPos">Enemy world position</param>
        /// <param name="isBoss">Whether this is a boss enemy (gets volume bonus)</param>
        /// <param name="isPrimary">Whether this is the primary (closest) enemy</param>
        /// <param name="maxRange">Maximum detection range in pixels</param>
        /// <param name="checkWalls">Whether to check for wall occlusion</param>
        /// <returns>Spatial audio parameters</returns>
        public static SpatialAudioData CalculateSpatialAudio(
            Vector2 playerPos,
            Vector2 enemyPos,
            bool isBoss = false,
            bool isPrimary = true,
            float maxRange = 832f,
            bool checkWalls = true)
        {
            SpatialAudioData result = new SpatialAudioData();

            Vector2 offset = enemyPos - playerPos;
            float distance = offset.magnitude;

            // Calculate stereo pan based on horizontal offset
            result.pan = Mathf.Clamp(offset.x / PAN_SCALE_PIXELS, -1f, 1f);

            // Calculate pitch shift based on vertical offset
            float pitchOffset = Mathf.Clamp(offset.y / PITCH_SCALE_PIXELS, -PITCH_RANGE, PITCH_RANGE);
            result.pitch = pitchOffset;

            // Calculate distance-based volume
            float distanceTiles = distance / 16f; // Convert pixels to tiles
            float referenceTiles = maxRange / 16f;
            result.volume = CalculateDistanceVolume(distanceTiles, referenceTiles);

            // Boss bonus
            if (isBoss)
            {
                result.volume += 0.12f;
            }

            // Secondary cue scaling (2nd and 3rd enemies are quieter)
            if (!isPrimary)
            {
                result.volume *= 0.25f;
            }

            // Check for wall muffling
            result.isMuffled = false;
            if (checkWalls)
            {
                // Use layer mask 8448 (from Silksong's own wall detection)
                RaycastHit2D hit = Physics2D.Linecast(playerPos, enemyPos, 8448);
                if (hit.collider != null)
                {
                    result.isMuffled = true;
                    result.volume *= 0.4f; // Muffle to 40% volume
                }
            }

            // Clamp final volume
            result.volume = Mathf.Clamp01(result.volume);

            return result;
        }

        /// <summary>
        /// Calculate volume based on distance with exponential falloff.
        /// Matches TerrariaAccess attenuation formula.
        /// </summary>
        private static float CalculateDistanceVolume(float distanceTiles, float referenceTiles)
        {
            // Normalize distance (0 at player, 1 at max range)
            float normalized = Mathf.Clamp01(distanceTiles / referenceTiles);

            // Invert and apply exponent for falloff curve
            float inverted = 1f - normalized;
            float shaped = Mathf.Pow(inverted, ATTENUATION_EXPONENT);

            // Interpolate between min and max volume
            float volume = Mathf.Lerp(VOLUME_MIN, 1f, shaped);

            // Apply scale factor
            return VOLUME_MIN + (volume - VOLUME_MIN) * VOLUME_SCALE;
        }

        /// <summary>
        /// Apply spatial audio data to an AudioSource component.
        /// </summary>
        public static void ApplyToAudioSource(AudioSource source, SpatialAudioData data)
        {
            if (source == null) return;

            source.panStereo = data.pan;
            source.pitch = 1f + data.pitch;
            source.volume = data.volume;
        }

        /// <summary>
        /// Check if a position is on screen (with buffer).
        /// </summary>
        public static bool IsOnScreen(Vector2 worldPos, Camera camera = null, float buffer = 2f)
        {
            if (camera == null)
            {
                // Try to get the main camera
                if (GameCameras.instance != null)
                {
                    camera = GameCameras.instance.mainCamera;
                }
                else
                {
                    camera = Camera.main;
                }
            }

            if (camera == null) return false;

            Vector3 viewportPoint = camera.WorldToViewportPoint(worldPos);

            // Check with buffer (allow slightly off-screen)
            return viewportPoint.x >= -buffer && viewportPoint.x <= 1f + buffer &&
                   viewportPoint.y >= -buffer && viewportPoint.y <= 1f + buffer &&
                   viewportPoint.z > 0;
        }
    }
}
