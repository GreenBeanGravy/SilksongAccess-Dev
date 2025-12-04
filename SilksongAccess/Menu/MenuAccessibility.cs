using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

namespace SilksongAccess.Menu
{
    public static class MenuAccessibility
    {
        private static ManualLogSource _logger;

        // State tracking flags
        private static bool _isTransitioningMenu = false;
        private static string _currentMenuTitle = "";
        internal static bool _isRebinding = false;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        private static string GetGameObjectPath(GameObject go)
        {
            if (go == null) return "null";
            string path = go.name;
            Transform current = go.transform;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }
            return path;
        }

        private static string GetTextFromChildren(GameObject go)
        {
            if (go == null) return "";
            Text[] texts = go.GetComponentsInChildren<Text>(true);
            if (texts.Length > 0)
            {
                return string.Join(", ", texts
                    .Where(t => t.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(t.text))
                    .Select(t => t.text.Trim()));
            }
            return "";
        }

        public static void AnnounceGameObject(GameObject go, bool isMenuTransition = false)
        {
            if (go == null) return;

            StringBuilder sb = new StringBuilder();

            if (isMenuTransition)
            {
                sb.Append(_currentMenuTitle);
            }

            string elementText = "";

            SaveSlotButton saveSlot = go.GetComponentInParent<SaveSlotButton>();
            if (saveSlot != null)
            {
                elementText = AnnounceSaveSlot(saveSlot, go);
            }
            else
            {
                elementText = GetTextFromChildren(go);
            }

            if (_isRebinding)
            {
                var uiSkins = UIManager.instance?.uiButtonSkins;
                if (uiSkins != null)
                {
                    if (uiSkins.listeningKey != null && uiSkins.listeningKey.gameObject == go)
                    {
                        elementText = "Press any key to bind";
                    }
                    else if (uiSkins.listeningButton != null && uiSkins.listeningButton.gameObject == go)
                    {
                        elementText = "Press any button to bind";
                    }
                }
            }

            if (!string.IsNullOrEmpty(elementText))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(elementText);
            }

            List<Selectable> selectables = null;
            var menuList = go.GetComponentInParent<MenuButtonList>();

            if (menuList != null)
            {
                selectables = Traverse.Create(menuList).Field<List<Selectable>>("activeSelectables").Value;
            }
            else if (go.transform.parent != null)
            {
                selectables = new List<Selectable>();
                for (int i = 0; i < go.transform.parent.childCount; i++)
                {
                    var child = go.transform.parent.GetChild(i);
                    var selectable = child.GetComponent<Selectable>();
                    if (selectable != null && selectable.interactable && child.gameObject.activeInHierarchy)
                    {
                        selectables.Add(selectable);
                    }
                }
            }

            if (selectables != null && selectables.Count > 1)
            {
                Selectable currentSelectable = go.GetComponent<Selectable>();
                if (currentSelectable != null)
                {
                    int index = selectables.IndexOf(currentSelectable);
                    if (index != -1)
                    {
                        if (selectables.Count == 2 && (go.name == "ClearSaveButton" || go.name == "RestoreSaveButton"))
                        {
                            index = (index == 0) ? 1 : 0;
                        }

                        if (sb.Length > 0) sb.Append(", ");
                        sb.Append($"{index + 1} of {selectables.Count}");
                    }
                }
            }

            if (sb.Length > 0)
            {
                SpeechSynthesizer.Speak(sb.ToString(), true);
            }
        }

        private static string AnnounceSaveSlot(SaveSlotButton saveSlot, GameObject selectedObject)
        {
            string slotName = saveSlot.gameObject.name;
            string slotNumber = "Unknown";
            if (slotName.Contains("One")) slotNumber = "1";
            else if (slotName.Contains("Two")) slotNumber = "2";
            else if (slotName.Contains("Three")) slotNumber = "3";
            else if (slotName.Contains("Four")) slotNumber = "4";

            if (selectedObject.name == "ClearSaveButton") return $"Clear Save, Slot {slotNumber}";
            if (selectedObject.name == "RestoreSaveButton") return $"Restore Save, Slot {slotNumber}";

            Transform activeSaveContainer = saveSlot.transform.Find("ActiveSaveSlot");
            Transform newGameContainer = saveSlot.transform.Find("NewGameText");

            if (activeSaveContainer != null && activeSaveContainer.gameObject.activeInHierarchy)
            {
                var bottomSection = activeSaveContainer.Find("Bottom Section");
                if (bottomSection != null)
                {
                    var playTimeText = bottomSection.Find("PlayTimeText")?.GetComponent<Text>();

                    if (playTimeText != null && playTimeText.gameObject.activeInHierarchy && playTimeText.text.Trim() == "0m")
                    {
                        return $"Slot {slotNumber}, New Game";
                    }

                    StringBuilder sb = new StringBuilder();
                    sb.Append($"Slot {slotNumber}");

                    var locationText = bottomSection.Find("LocationText")?.GetComponent<Text>();
                    var completionText = bottomSection.Find("CompletionText")?.GetComponent<Text>();

                    if (locationText != null && locationText.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(locationText.text))
                    {
                        sb.Append($", {locationText.text.Trim()}");
                    }
                    if (playTimeText != null && playTimeText.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(playTimeText.text))
                    {
                        sb.Append($", Time {playTimeText.text.Trim()}");
                    }
                    if (completionText != null && completionText.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(completionText.text))
                    {
                        sb.Append($", {completionText.text.Trim()} completion");
                    }
                    return sb.ToString();
                }
            }

            if (newGameContainer != null && newGameContainer.gameObject.activeInHierarchy)
            {
                return $"Slot {slotNumber}, New Game";
            }

            return $"Slot {slotNumber}";
        }

        // --- UI HARMONY PATCHES ---

        [HarmonyPatch(typeof(EventSystem), "SetSelectedGameObject", typeof(GameObject), typeof(BaseEventData))]
        private static class EventSystem_SetSelectedGameObject_Patch
        {
            private static void Postfix(GameObject selected)
            {
                if (selected == null) return;

                if (Plugin.IsDebugMode)
                {
                    _logger.LogInfo($"--- Debug Info for Selected Object ---");
                    _logger.LogInfo($"Main Path: {GetGameObjectPath(selected)}");

                    if (Plugin.LogChildObjectPaths)
                    {
                        Text[] texts = selected.GetComponentsInChildren<Text>(true);
                        if (texts.Any())
                        {
                            _logger.LogInfo($"Found {texts.Length} Text Component(s):");
                            foreach (var text in texts)
                            {
                                _logger.LogInfo($"  - Path: {GetGameObjectPath(text.gameObject)} | Content: '{text.text}' | Active: {text.gameObject.activeInHierarchy}");
                            }
                        }
                    }
                    _logger.LogInfo($"------------------------------------");
                }

                if (_isTransitioningMenu)
                {
                    AnnounceGameObject(selected, isMenuTransition: true);
                    _isTransitioningMenu = false;
                }
                else
                {
                    AnnounceGameObject(selected, isMenuTransition: false);
                }
            }
        }

        [HarmonyPatch(typeof(MenuAudioSlider), "Start")]
        private static class MenuAudioSlider_Start_Patch
        {
            private static void Postfix(MenuAudioSlider __instance)
            {
                var slider = Traverse.Create(__instance).Field<Slider>("slider").Value;
                var textUI = Traverse.Create(__instance).Field<Text>("textUI").Value;

                if (slider != null && textUI != null)
                {
                    slider.onValueChanged.AddListener((float val) => {
                        if (EventSystem.current.currentSelectedGameObject == slider.gameObject)
                        {
                            SpeechSynthesizer.Speak(textUI.text, false);
                        }
                    });
                }
            }
        }

        [HarmonyPatch(typeof(MenuSetting), "ChangeSetting")]
        private static class MenuSetting_ChangeSetting_Patch
        {
            private static void Postfix(MenuSetting __instance)
            {
                if (__instance.optionList != null)
                {
                    Plugin.Instance.StartCoroutine(AnnounceSettingChange(__instance.optionList));
                }
            }

            private static IEnumerator AnnounceSettingChange(MenuOptionHorizontal optionList)
            {
                yield return new WaitForEndOfFrame();
                var optionText = Traverse.Create(optionList).Field<Text>("optionText").Value;
                if (optionText != null && !string.IsNullOrWhiteSpace(optionText.text))
                {
                    SpeechSynthesizer.Speak(optionText.text, true);
                }
            }
        }

        [HarmonyPatch(typeof(UIManager), "ShowMenu")]
        private static class UIManager_ShowMenu_Patch
        {
            private static void Postfix(MenuScreen menu)
            {
                if (menu == null) return;
                Text titleText = menu.GetComponentsInChildren<Text>(true)
                                   .OrderByDescending(t => t.fontSize)
                                   .FirstOrDefault();
                _currentMenuTitle = titleText != null ? titleText.text.Trim() : "";
                _isTransitioningMenu = true;
            }
        }

        [HarmonyPatch(typeof(UIManager), "GoToMainMenu")]
        private static class UIManager_GoToMainMenu_Patch
        {
            private static void Postfix()
            {
                _currentMenuTitle = "Main Menu";
                _isTransitioningMenu = true;
            }
        }

        [HarmonyPatch(typeof(UIManager), "GoToProfileMenu")]
        private static class UIManager_GoToProfileMenu_Patch
        {
            private static void Postfix()
            {
                _currentMenuTitle = "Select Profile";
                _isTransitioningMenu = true;
            }
        }

        // --- REBINDING HARMONY PATCHES ---

        [HarmonyPatch(typeof(UIManager), "UIGoToRemapControllerMenu")]
        private static class UIManager_UIGoToRemapControllerMenu_Patch
        {
            private static void Postfix()
            {
                _isRebinding = true;
            }
        }

        [HarmonyPatch(typeof(UIManager), "UIGoToKeyboardMenu")]
        private static class UIManager_UIGoToKeyboardMenu_Patch
        {
            private static void Postfix()
            {
                _isRebinding = true;
            }
        }

        [HarmonyPatch(typeof(UIManager), "ApplyRemapGamepadMenuSettings")]
        private static class UIManager_ApplyRemapGamepadMenuSettings_Patch
        {
            private static void Prefix()
            {
                _isRebinding = false;
            }
        }

        [HarmonyPatch(typeof(UIManager), "ApplyKeyboardMenuSettings")]
        private static class UIManager_ApplyKeyboardMenuSettings_Patch
        {
            private static void Prefix()
            {
                _isRebinding = false;
            }
        }

        [HarmonyPatch(typeof(InControl.PlayerAction), "AddBinding")]
        private static class PlayerAction_AddBinding_Patch
        {
            private static void Postfix(InControl.PlayerAction __instance, InControl.BindingSource binding)
            {
                if (!_isRebinding) return;

                var uiSkins = UIManager.instance?.uiButtonSkins;
                if (uiSkins == null) return;

                bool isKeyboardListen = uiSkins.listeningKey != null;
                bool isControllerListen = uiSkins.listeningButton != null;

                bool isKeyboardBind = binding is InControl.KeyBindingSource || binding is InControl.MouseBindingSource;
                bool isControllerBind = binding is InControl.DeviceBindingSource;

                if ((isKeyboardListen && isKeyboardBind) || (isControllerListen && isControllerBind))
                {
                    SpeechSynthesizer.Speak($"{binding.Name} bound to {__instance.Name}", true);
                }
            }
        }
    }
}