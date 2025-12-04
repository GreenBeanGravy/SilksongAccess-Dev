using BepInEx.Logging;
using HarmonyLib;
using HutongGames.PlayMaker;
using System.Collections.Generic;
using System.Text;
using TMProOld;
using UnityEngine;
using UnityEngine.UI;

namespace SilksongAccess.Gameplay
{
    public static class FSMMenuAccessibility
    {
        private static ManualLogSource _logger;
        private static Dictionary<string, int> _lastAnnouncedSelections = new Dictionary<string, int>();
        private static Dictionary<FsmInt, PlayMakerFSM> _variableToFSM = new Dictionary<FsmInt, PlayMakerFSM>();
        private static string _lastAnnouncedUIListItem = "";

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        private static string GetFSMIdentifier(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.gameObject == null) return "";
            return $"{fsm.gameObject.GetInstanceID()}_{fsm.FsmName}";
        }

        private static string GetTextFromButton(Button button)
        {
            if (button == null) return "";

            var tmpTexts = button.GetComponentsInChildren<TextMeshPro>(true);
            foreach (var text in tmpTexts)
            {
                if (!string.IsNullOrEmpty(text.text))
                {
                    return text.text.Trim();
                }
            }

            var uiTexts = button.GetComponentsInChildren<Text>(true);
            foreach (var text in uiTexts)
            {
                if (!string.IsNullOrEmpty(text.text))
                {
                    return text.text.Trim();
                }
            }

            return "";
        }

        private static void AnnounceSelectionChange(PlayMakerFSM fsm, int selection, Button[] buttons)
        {
            if (fsm == null || buttons == null || buttons.Length == 0) return;
            if (selection < 0 || selection >= buttons.Length) return;

            string fsmId = GetFSMIdentifier(fsm);
            if (_lastAnnouncedSelections.ContainsKey(fsmId) && _lastAnnouncedSelections[fsmId] == selection)
            {
                return;
            }

            _lastAnnouncedSelections[fsmId] = selection;

            var button = buttons[selection];
            string buttonText = GetTextFromButton(button);

            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(buttonText))
            {
                sb.Append(buttonText);
            }
            else
            {
                sb.Append($"Option {selection + 1}");
            }

            if (buttons.Length > 1)
            {
                sb.Append($", {selection + 1} of {buttons.Length}");
            }

            SpeechSynthesizer.Speak(sb.ToString(), true);
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "Awake")]
        private static class PlayMakerFSM_Awake_Patch
        {
            private static void Postfix(PlayMakerFSM __instance)
            {
                if (__instance == null || __instance.Fsm == null) return;

                try
                {
                    var fsmVars = __instance.Fsm.Variables;
                    if (fsmVars != null && fsmVars.IntVariables != null)
                    {
                        foreach (var intVar in fsmVars.IntVariables)
                        {
                            if (intVar != null)
                            {
                                _variableToFSM[intVar] = __instance;
                            }
                        }
                    }
                }
                catch { }
            }
        }

        [HarmonyPatch(typeof(FsmInt), "Value", MethodType.Setter)]
        private static class FsmInt_Value_Patch
        {
            private static void Postfix(FsmInt __instance)
            {
                if (__instance == null) return;

                if (__instance.Name == "Selection Int" || __instance.Name == "Current Selection" ||
                    __instance.Name == "Selected" || __instance.Name == "Current Index")
                {
                    try
                    {
                        if (!_variableToFSM.TryGetValue(__instance, out PlayMakerFSM fsm) || fsm == null)
                        {
                            return;
                        }

                        int selection = __instance.Value;
                        var go = fsm.gameObject;
                        if (go != null)
                        {
                            var buttons = go.GetComponentsInChildren<Button>(false);
                            if (buttons != null && buttons.Length > 0)
                            {
                                AnnounceSelectionChange(fsm, selection, buttons);
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "OnEnable")]
        private static class PlayMakerFSM_OnEnable_Patch
        {
            private static void Postfix(PlayMakerFSM __instance)
            {
                if (__instance == null) return;

                if (__instance.FsmName == "ui_list")
                {
                    ShopAccessibility.SetInConfirmationDialog(true);
                }

                string fsmId = GetFSMIdentifier(__instance);
                if (_lastAnnouncedSelections.ContainsKey(fsmId))
                {
                    _lastAnnouncedSelections.Remove(fsmId);
                }

                try
                {
                    var fsmVars = __instance.Fsm?.Variables;
                    if (fsmVars != null && fsmVars.IntVariables != null)
                    {
                        foreach (var intVar in fsmVars.IntVariables)
                        {
                            if (intVar != null && !_variableToFSM.ContainsKey(intVar))
                            {
                                _variableToFSM[intVar] = __instance;
                            }
                        }
                    }
                }
                catch { }
            }
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "OnDisable")]
        private static class PlayMakerFSM_OnDisable_Patch
        {
            private static void Postfix(PlayMakerFSM __instance)
            {
                if (__instance == null) return;

                try
                {
                    var fsmVars = __instance.Fsm?.Variables;
                    if (fsmVars != null && fsmVars.IntVariables != null)
                    {
                        foreach (var intVar in fsmVars.IntVariables)
                        {
                            if (intVar != null && _variableToFSM.ContainsKey(intVar))
                            {
                                _variableToFSM.Remove(intVar);
                            }
                        }
                    }
                }
                catch { }

                string fsmId = GetFSMIdentifier(__instance);
                if (_lastAnnouncedSelections.ContainsKey(fsmId))
                {
                    _lastAnnouncedSelections.Remove(fsmId);
                }

                if (__instance.FsmName == "ui_list" || __instance.FsmName == "ui_list_item")
                {
                    _lastAnnouncedUIListItem = "";
                    ShopAccessibility.SetInConfirmationDialog(false);
                }
            }
        }

        [HarmonyPatch(typeof(Fsm), "Event", new[] { typeof(FsmEvent) })]
        private static class Fsm_Event_Patch
        {
            private static void Postfix(Fsm __instance, FsmEvent fsmEvent)
            {
                if (__instance == null || fsmEvent == null) return;

                if (__instance.Name == "ui_list_item" && fsmEvent.Name == "GET SELECTED")
                {
                    try
                    {
                        var go = __instance.GameObject;
                        if (go != null)
                        {
                            if (!go.activeInHierarchy) return;

                            var parent = go.transform.parent;
                            if (parent != null)
                            {
                                var siblings = new List<GameObject>();
                                for (int i = 0; i < parent.childCount; i++)
                                {
                                    var child = parent.GetChild(i);
                                    var fsm = child.GetComponent<PlayMakerFSM>();
                                    if (fsm != null && fsm.FsmName == "ui_list_item" && child.gameObject.activeInHierarchy)
                                    {
                                        siblings.Add(child.gameObject);
                                    }
                                }

                                if (siblings.Count == 0) return;

                                ShopAccessibility.SetInConfirmationDialog(true);

                                siblings.Reverse();

                                int index = siblings.IndexOf(go);
                                if (index >= 0)
                                {
                                    string itemText = GetTextFromGameObject(go);
                                    if (string.IsNullOrEmpty(itemText))
                                    {
                                        itemText = go.name;
                                    }

                                    if (Plugin.IsDebugMode && Plugin.LogChildObjectPaths)
                                    {
                                        _logger.LogInfo($"UI List Item selected: '{itemText}' at index {index} of {siblings.Count}");
                                        for (int i = 0; i < siblings.Count; i++)
                                        {
                                            _logger.LogInfo($"  [{i}] = {GetTextFromGameObject(siblings[i])} ({siblings[i].name})");
                                        }
                                    }

                                    string identifier = $"{parent.GetInstanceID()}_{index}_{itemText}";
                                    if (_lastAnnouncedUIListItem == identifier)
                                    {
                                        return;
                                    }
                                    _lastAnnouncedUIListItem = identifier;

                                    if (siblings.Count > 1)
                                    {
                                        SpeechSynthesizer.Speak($"{itemText}, {index + 1} of {siblings.Count}", true);
                                    }
                                    else
                                    {
                                        SpeechSynthesizer.Speak(itemText, true);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (__instance.Name == "ui_list" && fsmEvent.Name == "CANCEL")
                {
                    ShopAccessibility.SetInConfirmationDialog(false);
                    _lastAnnouncedUIListItem = "";
                }

                if (Plugin.IsDebugMode && Plugin.LogChildObjectPaths)
                {
                    if (__instance.Name.Contains("ui_list"))
                    {
                        _logger.LogInfo($"FSM Event: {__instance.Name} -> {fsmEvent.Name} on {__instance.GameObject?.name}");
                    }
                }
            }
        }

        private static string GetTextFromGameObject(GameObject go)
        {
            if (go == null) return "";

            var tmpTexts = go.GetComponentsInChildren<TextMeshPro>(true);
            foreach (var text in tmpTexts)
            {
                if (!string.IsNullOrEmpty(text.text))
                {
                    return text.text.Trim();
                }
            }

            var uiTexts = go.GetComponentsInChildren<Text>(true);
            foreach (var text in uiTexts)
            {
                if (!string.IsNullOrEmpty(text.text))
                {
                    return text.text.Trim();
                }
            }

            return "";
        }
    }
}