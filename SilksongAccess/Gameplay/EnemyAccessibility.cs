using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using GlobalEnums;
using System;
using SilksongAccess.Audio;

namespace SilksongAccess.Gameplay
{
    public static class EnemyAccessibility
    {
        private static ManualLogSource _logger;
        private static GameObject _pingManagerObject;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
            InitializeAudioPingSystem();
        }

        /// <summary>
        /// Initialize the enemy audio ping system.
        /// Creates a persistent GameObject with the EnemyAudioPingManager component.
        /// </summary>
        private static void InitializeAudioPingSystem()
        {
            if (_pingManagerObject == null)
            {
                _pingManagerObject = new GameObject("EnemyAudioPingManager");
                UnityEngine.Object.DontDestroyOnLoad(_pingManagerObject);

                EnemyAudioPingManager.Initialize(_logger);
                _pingManagerObject.AddComponent<EnemyAudioPingManager>();

                _logger.LogInfo("Enemy audio ping system initialized.");
            }
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

        /// <summary>
        /// Auto-aim attack patch.
        /// When EnableAutoAimAttacks is true, modifies the attack direction to face the closest on-screen enemy.
        /// </summary>
        [HarmonyPatch(typeof(HeroController), "Attack")]
        private static class HeroController_Attack_Patch
        {
            private static void Prefix(ref AttackDirection attackDir)
            {
                // Only modify if auto-aim is enabled
                if (!AutoAimAccessibility.EnableAutoAimAttacks) return;

                // Only modify if there are enemies on screen
                if (!AutoAimAccessibility.HasOnScreenEnemies()) return;

                // Get the auto-aimed direction
                AttackDirection newDirection = AutoAimAccessibility.GetAutoAimAttackDirection(attackDir);
                
                // Update the attack direction
                attackDir = newDirection;
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