using BepInEx.Logging;
using GlobalEnums;
using HarmonyLib;
using System;
using System.Collections;
using UnityEngine;

namespace SilksongAccess.Gameplay
{
    public static class HeroAccessibility
    {
        private static ManualLogSource _logger;
        private static PlayerData _playerData;

        /// <summary>
        /// Controls whether action announcements (jump, dash, attack) are spoken.
        /// Set to false to disable these specific announcements.
        /// </summary>
        public static bool AnnounceActions = false;

        // --- State Tracking ---
        private static int _lastHealth;
        private static int _lastSilk;
        private static int _lastGeo;
        private static string _lastDamageSource = "Unknown";

        // --- Cooldowns to prevent spam ---
        private static float _nextGeoTime = 0f;
        private const float GEO_COOLDOWN = 0.25f;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        private static void UpdatePlayerData()
        {
            if (_playerData == null)
            {
                _playerData = PlayerData.instance;
            }
        }

        // Helper function to get a clean name for an object.
        private static string GetDisplayName(GameObject go)
        {
            if (go == null) return "Unknown";
            // Cleans up "(Clone)" from instantiated object names and trims whitespace
            string name = go.name.Replace("(Clone)", "").Trim();
            // Further clean up common non-descriptive names
            if (name.StartsWith("Spike Hit")) return "Spikes";
            return name;
        }

        // --- Patches ---

        [HarmonyPatch(typeof(HeroController), "Start")]
        private static class HeroController_Start_Patch
        {
            private static void Postfix()
            {
                UpdatePlayerData();
                if (_playerData != null)
                {
                    _lastHealth = _playerData.health;
                    _lastSilk = _playerData.silk;
                    _lastGeo = _playerData.geo;
                    _logger.LogInfo("HeroAccessibility state initialized.");
                }
            }
        }

        /// <summary>
        /// Captures the source of damage just before it's dealt.
        /// This prevents spam by only storing the source, not announcing it.
        /// </summary>
        [HarmonyPatch(typeof(HeroController), "TakeDamage")]
        private static class HeroController_TakeDamage_Patch
        {
            private static void Prefix(HeroController __instance, GameObject go, HazardType hazardType)
            {
                // Only update the source if the player is not currently invincible
                if (__instance.CanTakeDamage())
                {
                    _lastDamageSource = (hazardType == HazardType.ENEMY && go != null) ? GetDisplayName(go) : hazardType.ToString();
                }
            }
        }

        /// <summary>
        /// Centralized patch to announce any health gain.
        /// </summary>
        [HarmonyPatch(typeof(PlayerData), "AddHealth")]
        private static class PlayerData_AddHealth_Patch
        {
            private static void Prefix(PlayerData __instance)
            {
                _lastHealth = __instance.health;
            }

            private static void Postfix(PlayerData __instance)
            {
                if (__instance.health > _lastHealth)
                {
                    int healthGained = __instance.health - _lastHealth;
                    SpeechSynthesizer.Speak($"Gained {healthGained} health", true);
                    _lastHealth = __instance.health;
                }
            }
        }

        /// <summary>
        /// Centralized patch to announce any health loss.
        /// This is the single source of truth for damage announcements.
        /// </summary>
        [HarmonyPatch(typeof(PlayerData), "TakeHealth")]
        private static class PlayerData_TakeHealth_Patch
        {
            private static void Prefix(PlayerData __instance)
            {
                _lastHealth = __instance.health;
            }

            private static void Postfix(PlayerData __instance)
            {
                if (__instance.health < _lastHealth)
                {
                    int healthLost = _lastHealth - __instance.health;
                    SpeechSynthesizer.Speak($"Lost {healthLost} health from {_lastDamageSource}", true);
                    _lastHealth = __instance.health;
                    _lastDamageSource = "Unknown"; // Reset after use
                }
            }
        }

        [HarmonyPatch(typeof(HeroController), "AddSilk", new Type[] { typeof(int), typeof(bool), typeof(SilkSpool.SilkAddSource), typeof(bool) })]
        private static class HeroController_AddSilk_Patch
        {
            private static void Postfix(int amount)
            {
                UpdatePlayerData();
                if (_playerData != null && _playerData.silk != _lastSilk)
                {
                    SpeechSynthesizer.Speak($"Gained {amount} silk", false);
                    _lastSilk = _playerData.silk;
                }
            }
        }

        [HarmonyPatch(typeof(HeroController), "TakeSilk", new Type[] { typeof(int), typeof(SilkSpool.SilkTakeSource) })]
        private static class HeroController_TakeSilk_Patch
        {
            private static void Postfix(int amount)
            {
                UpdatePlayerData();
                if (_playerData != null && _playerData.silk != _lastSilk)
                {
                    SpeechSynthesizer.Speak($"Used {amount} silk", false);
                    _lastSilk = _playerData.silk;
                }
            }
        }

        [HarmonyPatch(typeof(HeroController), "AddGeo")]
        private static class HeroController_AddGeo_Patch
        {
            private static void Postfix(int amount)
            {
                if (Time.time < _nextGeoTime) return;
                _nextGeoTime = Time.time + GEO_COOLDOWN;

                UpdatePlayerData();
                if (_playerData != null && _playerData.geo != _lastGeo)
                {
                    SpeechSynthesizer.Speak($"{amount} Geo", false);
                    _lastGeo = _playerData.geo;
                }
            }
        }

        [HarmonyPatch(typeof(HeroController), "Die")]
        private static class HeroController_Die_Patch
        {
            private static void Postfix()
            {
                SpeechSynthesizer.Speak("Died", true);
            }
        }

        /// <summary>
        /// Announces the correct, player-facing area name when the small title card appears.
        /// </summary>
        [HarmonyPatch(typeof(AreaTitleController), "VisitPause")]
        private static class AreaTitleController_VisitPause_Patch
        {
            private static void Postfix(AreaTitleController __instance)
            {
                AnnounceAreaTitle(__instance);
            }
        }

        /// <summary>
        /// Announces the correct, player-facing area name when the large title card appears.
        /// </summary>
        [HarmonyPatch(typeof(AreaTitleController), "UnvisitPause")]
        private static class AreaTitleController_UnvisitPause_Patch
        {
            private static void Postfix(AreaTitleController __instance)
            {
                AnnounceAreaTitle(__instance);
            }
        }

        private static void AnnounceAreaTitle(AreaTitleController instance)
        {
            try
            {
                // Correctly use Traverse to access the private nested struct's field.
                var controllerTraverse = Traverse.Create(instance);
                object areaStructObject = controllerTraverse.Field("currentAreaData").GetValue();
                var areaTraverse = Traverse.Create(areaStructObject);
                string identifier = areaTraverse.Field<string>("Identifier").Value;

                if (!string.IsNullOrEmpty(identifier))
                {
                    // The identifier is the key for the localized area name.
                    SpeechSynthesizer.Speak(identifier, true);
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Error in AnnounceAreaTitle: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(HeroController), "HeroJump", new Type[] { typeof(bool) })]
        private static class HeroController_HeroJump_Patch
        {
            private static void Postfix()
            {
                if (!AnnounceActions) return;
                SpeechSynthesizer.Speak("Jump", false);
            }
        }

        [HarmonyPatch(typeof(HeroController), "DoDoubleJump")]
        private static class HeroController_DoDoubleJump_Patch
        {
            private static void Postfix()
            {
                if (!AnnounceActions) return;
                SpeechSynthesizer.Speak("Double Jump", false);
            }
        }

        [HarmonyPatch(typeof(HeroController), "HeroDash")]
        private static class HeroController_HeroDash_Patch
        {
            private static void Postfix()
            {
                if (!AnnounceActions) return;
                SpeechSynthesizer.Speak("Dash", false);
            }
        }

        /// <summary>
        /// Announces directional attacks.
        /// </summary>
        [HarmonyPatch(typeof(HeroController), "Attack")]
        private static class HeroController_Attack_Patch
        {
            private static void Postfix(HeroController __instance, AttackDirection attackDir)
            {
                if (!AnnounceActions) return;

                string announcement = "";
                switch (attackDir)
                {
                    case AttackDirection.normal:
                        announcement = __instance.cState.facingRight ? "Right slash" : "Left slash";
                        break;
                    case AttackDirection.upward:
                        announcement = "Up slash";
                        break;
                    case AttackDirection.downward:
                        announcement = __instance.cState.facingRight ? "Down right slash" : "Down left slash";
                        break;
                }
                SpeechSynthesizer.Speak(announcement, false);
            }
        }

        [HarmonyPatch(typeof(HeroController), "ActivateQuickening")]
        private static class HeroController_ActivateQuickening_Patch
        {
            private static void Postfix()
            {
                SpeechSynthesizer.Speak("Quickening", true);
            }
        }
    }
}