using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TMProOld;
using UnityEngine.EventSystems;

namespace SilksongAccess.Menu
{
    public static class InventoryAccessibility
    {
        private static ManualLogSource _logger;
        private static InventoryItemSelectable _lastAnnouncedItem;
        private static InventoryPane _lastAnnouncedPane;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        private static void AnnounceInventoryItem(InventoryItemSelectable item)
        {
            if (item == null || item == _lastAnnouncedItem) return;
            _lastAnnouncedItem = item;

            StringBuilder sb = new StringBuilder();

            // Announce Display Name
            string displayName = item.DisplayName?.Trim();
            if (item is InventoryItemWideMapZone mapZone)
            {
                var label = Traverse.Create(mapZone).Field<TMP_Text>("labelText").Value;
                if (label != null && !string.IsNullOrEmpty(label.text))
                {
                    displayName = label.text.Trim();
                }
            }

            if (!string.IsNullOrEmpty(displayName))
            {
                sb.Append(displayName);
            }

            // Announce Item-specific details
            if (item is InventoryItemCollectable collectable)
            {
                if (collectable.Item != null && collectable.Item.DisplayAmount)
                {
                    sb.Append($", Amount: {collectable.Item.CollectedAmount}");
                }
            }
            else if (item is InventoryItemTool tool)
            {
                if (tool.ItemData != null && tool.ItemData.IsEquipped)
                {
                    sb.Append(", Equipped");
                }
            }
            else if (item is InventoryToolCrestSlot slot)
            {
                sb.Append($", {slot.Type} slot");
                if (slot.EquippedItem != null)
                {
                    sb.Append($", holding {slot.EquippedItem.DisplayName}");
                }
                else
                {
                    sb.Append(", empty");
                }
            }

            // Announce Index
            var manager = item.GetComponentInParent<InventoryItemManager>();
            if (manager != null)
            {
                var grid = Traverse.Create(manager).Field<InventoryItemGrid>("itemList").Value;
                if (grid != null)
                {
                    var allItems = grid.GetListItems<InventoryItemSelectable>(s => s.gameObject.activeInHierarchy);
                    if (allItems.Count > 1)
                    {
                        int index = allItems.IndexOf(item);
                        if (index != -1)
                        {
                            sb.Append($", {index + 1} of {allItems.Count}");
                        }
                    }
                }
            }

            // Announce Description
            string description = item.Description?.Trim();
            if (!string.IsNullOrEmpty(description) && description != displayName)
            {
                description = description.Replace("\n", " ");
                sb.Append($". {description}");
            }

            if (sb.Length > 0)
            {
                SpeechSynthesizer.Speak(sb.ToString(), true);
            }
        }

        [HarmonyPatch(typeof(EventSystem), "SetSelectedGameObject", typeof(GameObject), typeof(BaseEventData))]
        [HarmonyPriority(Priority.High)]
        private static class AnnounceArrows_Patch
        {
            private static bool Prefix(GameObject selected)
            {
                if (selected == null) return true;

                var paneList = selected.GetComponentInParent<InventoryPaneList>();
                if (paneList != null)
                {
                    var leftArrow = Traverse.Create(paneList).Field("leftArrow").GetValue<Transform>();
                    if (leftArrow != null && selected == leftArrow.gameObject)
                    {
                        SpeechSynthesizer.Speak("Previous Tab", true);
                        return false;
                    }

                    var rightArrow = Traverse.Create(paneList).Field("rightArrow").GetValue<Transform>();
                    if (rightArrow != null && selected == rightArrow.gameObject)
                    {
                        SpeechSynthesizer.Speak("Next Tab", true);
                        return false;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(InventoryItemManager), "SetSelected", typeof(InventoryItemSelectable), typeof(InventoryItemManager.SelectionDirection?), typeof(bool))]
        private static class InventoryItemManager_SetSelected_Patch
        {
            private static void Postfix(InventoryItemSelectable selectable)
            {
                if (selectable != null)
                {
                    AnnounceInventoryItem(selectable);
                }
            }
        }

        [HarmonyPatch(typeof(InventoryPaneList), "BeginPane")]
        private static class InventoryPaneList_BeginPane_Patch
        {
            private static void Postfix(InventoryPane pane)
            {
                if (pane != null && pane != _lastAnnouncedPane)
                {
                    _lastAnnouncedPane = pane;
                    _lastAnnouncedItem = null; // Reset item when pane changes
                    SpeechSynthesizer.Speak(pane.DisplayName, true);
                }
            }
        }

        [HarmonyPatch(typeof(InventoryItemToolManager), "StartSelection")]
        private static class InventoryItemToolManager_StartSelection_Patch
        {
            private static void Postfix(InventoryToolCrestSlot slot)
            {
                if (slot != null)
                {
                    SpeechSynthesizer.Speak($"Select {slot.Type} tool", true);
                }
            }
        }

        [HarmonyPatch(typeof(InventoryItemToolManager), "EndSelection")]
        private static class InventoryItemToolManager_EndSelection_Patch
        {
            private static void Postfix(InventoryItemTool tool)
            {
                if (tool != null && tool.ItemData != null)
                {
                    SpeechSynthesizer.Speak($"{tool.ItemData.DisplayName} equipped.", false);
                }
                else
                {
                    SpeechSynthesizer.Speak("Selection cancelled.", false);
                }
            }
        }

        [HarmonyPatch(typeof(InventoryItemToolManager), "UnequipTool")]
        private static class InventoryItemToolManager_UnequipTool_Patch
        {
            private static void Postfix(ToolItem toolItem)
            {
                if (toolItem != null)
                {
                    SpeechSynthesizer.Speak($"{toolItem.DisplayName} unequipped.", false);
                }
            }
        }

        [HarmonyPatch(typeof(InventoryToolCrestList), "StartSwitchingCrests")]
        private static class InventoryToolCrestList_StartSwitchingCrests_Patch
        {
            private static void Postfix(InventoryToolCrestList __instance)
            {
                SpeechSynthesizer.Speak("Change Crest", true);
                if (__instance.CurrentCrest != null)
                {
                    SpeechSynthesizer.Speak(__instance.CurrentCrest.DisplayName, false);
                }
            }
        }

        [HarmonyPatch(typeof(InventoryToolCrestList), "StopSwitchingCrests")]
        private static class InventoryToolCrestList_StopSwitchingCrests_Patch
        {
            private static void Postfix(InventoryToolCrestList __instance, bool keepNewSelection)
            {
                if (keepNewSelection && __instance.CurrentCrest != null)
                {
                    SpeechSynthesizer.Speak($"Crest set to {__instance.CurrentCrest.DisplayName}", false);
                }
                else
                {
                    SpeechSynthesizer.Speak("Crest selection cancelled", false);
                }
            }
        }

        [HarmonyPatch(typeof(InventoryToolCrestList), "SetCurrentCrest")]
        private static class InventoryToolCrestList_SetCurrentCrest_Patch
        {
            private static void Postfix(InventoryToolCrestList __instance, InventoryToolCrest crest, bool doScroll)
            {
                if (__instance.IsSwitchingCrests && doScroll && crest != null)
                {
                    var unlockedCrests = Traverse.Create(__instance).Field<List<InventoryToolCrest>>("unlockedCrests").Value;
                    int index = unlockedCrests.IndexOf(crest);
                    SpeechSynthesizer.Speak($"{crest.DisplayName}, {index + 1} of {unlockedCrests.Count}", true);
                }
            }
        }
    }
}