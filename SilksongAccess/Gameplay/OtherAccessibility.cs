using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SilksongAccess.Gameplay
{
    public static class OtherAccessibility
    {
        private static ManualLogSource _logger;
        private static bool _isLoading = false;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Announces when a loading spinner becomes active.
        /// </summary>
        [HarmonyPatch(typeof(LoadingSpinner), "SetActive")]
        private static class LoadingSpinner_SetActive_Patch
        {
            private static void Postfix(bool value)
            {
                if (value && !_isLoading)
                {
                    _isLoading = true;
                    SpeechSynthesizer.Speak("Loading", false);
                }
                else if (!value)
                {
                    _isLoading = false;
                }
            }
        }

        /// <summary>
        /// Announces each new line of dialogue, correctly identifying the speaker for every line.
        /// </summary>
        [HarmonyPatch(typeof(NPCControlBase), "NewLineStarted")]
        private static class NPCControlBase_NewLineStarted_Patch
        {
            private static void Postfix(NPCControlBase __instance, DialogueBox.DialogueLine line)
            {
                if (string.IsNullOrEmpty(line.Text)) return;

                if (line.IsPlayer)
                {
                    SpeechSynthesizer.Speak($"Choice: {line.Text}", true);
                    return;
                }

                string speakerName = "";

                NpcDialogueTitle dialogueTitle = __instance.GetComponent<NpcDialogueTitle>();
                if (dialogueTitle != null)
                {
                    var traverse = Traverse.Create(dialogueTitle);
                    var titles = traverse.Field<NpcDialogueTitle.SpeakerTitle[]>("titles").Value;

                    if (titles != null && titles.Length > 0)
                    {
                        NpcDialogueTitle.SpeakerTitle currentTitle = null;

                        foreach (var title in titles)
                        {
                            if (currentTitle == null || title.SpeakerEvent == line.Event)
                            {
                                currentTitle = title;
                            }
                        }

                        if (currentTitle != null)
                        {
                            speakerName = currentTitle.Title;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(speakerName))
                {
                    SpeechSynthesizer.Speak($"{speakerName}: {line.Text}", true);
                }
                else
                {
                    SpeechSynthesizer.Speak(line.Text, true);
                }
            }
        }
    }
}