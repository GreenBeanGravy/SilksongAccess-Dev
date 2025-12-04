using BepInEx.Logging;
using GlobalEnums;
using HarmonyLib;

namespace SilksongAccess.Cutscenes
{
    public static class CutsceneAccessibility
    {
        private static ManualLogSource _logger;
        private static bool _isInCutscene = false;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        [HarmonyPatch(typeof(UIManager), "SetState")]
        private static class UIManager_SetState_Patch
        {
            private static void Postfix(UIState newState)
            {
                if (newState == UIState.CUTSCENE && !_isInCutscene)
                {
                    _isInCutscene = true;
                    string sceneName = GameManager.instance.GetSceneNameString();
                    if (Plugin.IsDebugMode)
                    {
                        _logger.LogInfo($"[CUTSCENE] Cutscene Started in scene: {sceneName}");
                    }
                    SpeechSynthesizer.Speak("Cutscene starting", false);
                }
                else if (_isInCutscene && newState == UIState.PLAYING)
                {
                    _isInCutscene = false;
                    if (Plugin.IsDebugMode)
                    {
                        _logger.LogInfo($"[CUTSCENE] Cutscene Ended.");
                    }
                    SpeechSynthesizer.Speak("Cutscene ended", false);
                }
            }
        }

        [HarmonyPatch(typeof(InputHandler), "SetSkipMode")]
        private static class InputHandler_SetSkipMode_Patch
        {
            private static void Postfix(SkipPromptMode newMode)
            {
                if (_isInCutscene && (newMode == SkipPromptMode.SKIP_PROMPT || newMode == SkipPromptMode.SKIP_INSTANT))
                {
                    SpeechSynthesizer.Speak("Press any button to skip", false);
                }
            }
        }

        [HarmonyPatch(typeof(GameManager), "SkipCutscene")]
        private static class GameManager_SkipCutscene_Patch
        {
            private static void Prefix()
            {
                if (_isInCutscene)
                {
                    SpeechSynthesizer.Speak("Cutscene skipped", true);
                    _isInCutscene = false;
                }
            }
        }
    }
}