using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using GlobalEnums;
using System;

namespace SilksongAccess.Gameplay
{
    public static class EnemyAccessibility
    {
        private static ManualLogSource _logger;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Announces when an enemy is killed.
        /// Patches SendDeathEvent to avoid the AmbiguousMatchException on the overloaded Die() method.
        /// </summary>
        [HarmonyPatch(typeof(HealthManager), "SendDeathEvent")]
        private static class HealthManager_SendDeathEvent_Patch
        {
            private static void Postfix(HealthManager __instance)
            {
                // We only want to announce enemy deaths, not environmental objects etc.
                if (__instance.gameObject.layer == (int)PhysLayers.ENEMIES)
                {
                    string enemyName = GetDisplayName(__instance.gameObject);
                    SpeechSynthesizer.Speak($"{enemyName} defeated", false);
                }
            }
        }

        // Helper function to get a clean name for an object.
        private static string GetDisplayName(GameObject go)
        {
            if (go == null) return "Unknown";
            // Cleans up "(Clone)" from instantiated object names and trims whitespace
            return go.name.Replace("(Clone)", "").Trim();
        }
    }
}