using BepInEx;
using HarmonyLib;
using SilksongAccess.Menu;
using SilksongAccess.Gameplay;
using SilksongAccess.Cutscenes;

namespace SilksongAccess
{
    [BepInPlugin("Green", "Silksong Access", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        private static Harmony harmony;

        public static bool IsDebugMode = true;
        public static bool LogChildObjectPaths = false;

        private void Awake()
        {
            Instance = this;

            SpeechSynthesizer.Initialize(Logger);
            Logger.LogInfo("Speech Synthesizer initialized.");

            MenuAccessibility.Initialize(Logger);
            Logger.LogInfo("Menu Accessibility module initialized.");

            InventoryAccessibility.Initialize(Logger);
            Logger.LogInfo("Inventory Accessibility module initialized.");

            MapAccessibility.Initialize(Logger);
            Logger.LogInfo("Map Accessibility module initialized.");

            OtherAccessibility.Initialize(Logger);
            Logger.LogInfo("Gameplay Accessibility module initialized.");

            HeroAccessibility.Initialize(Logger);
            Logger.LogInfo("Hero Accessibility module initialized.");

            EnemyAccessibility.Initialize(Logger);
            Logger.LogInfo("Enemy Accessibility module initialized.");

            CutsceneAccessibility.Initialize(Logger);
            Logger.LogInfo("Cutscene Accessibility module initialized.");

            InteractionAccessibility.Initialize(Logger);
            Logger.LogInfo("Interaction Accessibility module initialized.");

            CollectableAccessibility.Initialize(Logger);
            Logger.LogInfo("Collectable Accessibility module initialized.");

            ShopAccessibility.Initialize(Logger);
            Logger.LogInfo("Shop Accessibility module initialized.");

            FSMMenuAccessibility.Initialize(Logger);
            Logger.LogInfo("FSM Menu Accessibility module initialized.");

            harmony = new Harmony("Green");
            Logger.LogInfo("Harmony instance created. Patching methods...");
            harmony.PatchAll();
            Logger.LogInfo("Patching complete.");

            SpeechSynthesizer.Speak("Accessibility mod loaded.", false);
        }

        private void OnDestroy()
        {
            Logger.LogInfo("Shutting down synthesizer.");
            SpeechSynthesizer.Shutdown();
            Logger.LogInfo("Speech Synthesizer shut down.");
        }
    }
}