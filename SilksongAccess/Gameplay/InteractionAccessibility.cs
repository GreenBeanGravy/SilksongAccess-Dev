using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SilksongAccess.Gameplay
{
    public static class InteractionAccessibility
    {
        private static ManualLogSource _logger;
        private static InteractableBase _lastInteractable = null;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        [HarmonyPatch(typeof(InteractableBase), "ShowInteraction")]
        private static class InteractableBase_ShowInteraction_Patch
        {
            private static void Postfix(InteractableBase __instance)
            {
                if (__instance == _lastInteractable) return;

                _lastInteractable = __instance;
                string labelText = GetFriendlyPromptLabel(__instance.InteractLabel);
                SpeechSynthesizer.Speak(labelText, true);
            }
        }

        [HarmonyPatch(typeof(InteractableBase), "HideInteraction")]
        private static class InteractableBase_HideInteraction_Patch
        {
            private static void Postfix(InteractableBase __instance)
            {
                if (_lastInteractable == __instance)
                {
                    _lastInteractable = null;
                }
            }
        }

        [HarmonyPatch(typeof(InteractableBase), "QueueInteraction")]
        private static class InteractableBase_QueueInteraction_Patch
        {
            private static void Postfix(InteractableBase __instance)
            {
                string labelText = GetFriendlyPromptLabel(__instance.InteractLabel);
                //SpeechSynthesizer.Speak($"Interacting: {labelText}", false);
            }
        }

        private static string GetFriendlyPromptLabel(InteractableBase.PromptLabels label)
        {
            switch (label)
            {
                case InteractableBase.PromptLabels.Inspect:
                    return "Inspect";
                case InteractableBase.PromptLabels.Speak:
                    return "Speak";
                case InteractableBase.PromptLabels.Listen:
                    return "Listen";
                case InteractableBase.PromptLabels.Enter:
                    return "Enter";
                case InteractableBase.PromptLabels.Ascend:
                    return "Ascend";
                case InteractableBase.PromptLabels.Rest:
                    return "Rest";
                case InteractableBase.PromptLabels.Shop:
                    return "Shop";
                case InteractableBase.PromptLabels.Travel:
                    return "Travel";
                case InteractableBase.PromptLabels.Challenge:
                    return "Challenge";
                case InteractableBase.PromptLabels.Exit:
                    return "Exit";
                case InteractableBase.PromptLabels.Descend:
                    return "Descend";
                case InteractableBase.PromptLabels.Sit:
                    return "Sit";
                case InteractableBase.PromptLabels.Trade:
                    return "Trade";
                case InteractableBase.PromptLabels.Accept:
                    return "Accept";
                case InteractableBase.PromptLabels.Watch:
                    return "Watch";
                case InteractableBase.PromptLabels.Ascend_GG:
                    return "Ascend";
                case InteractableBase.PromptLabels.Consume:
                    return "Consume";
                case InteractableBase.PromptLabels.Track:
                    return "Track";
                case InteractableBase.PromptLabels.TurnIn:
                    return "Turn in";
                case InteractableBase.PromptLabels.Attack:
                    return "Attack";
                case InteractableBase.PromptLabels.Give:
                    return "Give";
                case InteractableBase.PromptLabels.Take:
                    return "Take";
                case InteractableBase.PromptLabels.Claim:
                    return "Claim";
                case InteractableBase.PromptLabels.Call:
                    return "Call";
                case InteractableBase.PromptLabels.Play:
                    return "Play";
                case InteractableBase.PromptLabels.Dive:
                    return "Dive";
                case InteractableBase.PromptLabels.Take_Living:
                    return "Take";
                case InteractableBase.PromptLabels.Play_Game:
                    return "Play game";
                default:
                    return label.ToString();
            }
        }
    }
}