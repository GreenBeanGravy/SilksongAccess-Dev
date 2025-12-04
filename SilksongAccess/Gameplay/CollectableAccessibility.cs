using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System;
using static CollectableItem;

namespace SilksongAccess.Gameplay
{
    public static class CollectableAccessibility
    {
        private static ManualLogSource _logger;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        [HarmonyPatch(typeof(CollectableItemPickup), "DoPickupAction", new Type[] { typeof(bool) })]
        private static class CollectableItemPickup_DoPickupAction_Patch
        {
            private static void Postfix(CollectableItemPickup __instance, bool __result)
            {
                if (!__result) return;

                var item = __instance.Item;
                if (item != null)
                {
                    string itemName = GetItemName(item);
                    SpeechSynthesizer.Speak($"Picked up {itemName}", false);
                }
            }
        }

        [HarmonyPatch(typeof(CollectableItem), "Collect", new Type[] { typeof(int), typeof(bool) })]
        private static class CollectableItem_Collect_Patch
        {
            private static void Postfix(CollectableItem __instance, int amount, bool showPopup)
            {
                if (!showPopup) return;

                string itemName = GetItemName(__instance);
                if (amount > 1)
                {
                    SpeechSynthesizer.Speak($"Collected {amount} {itemName}", false);
                }
                else
                {
                    SpeechSynthesizer.Speak($"Collected {itemName}", false);
                }
            }
        }

        [HarmonyPatch(typeof(CollectableItem), "Take", new Type[] { typeof(int), typeof(bool) })]
        private static class CollectableItem_Take_Patch
        {
            private static void Postfix(CollectableItem __instance, int amount, bool showCounter)
            {
                if (!showCounter) return;

                string itemName = GetItemName(__instance);
                if (amount > 1)
                {
                    SpeechSynthesizer.Speak($"Used {amount} {itemName}", false);
                }
                else
                {
                    SpeechSynthesizer.Speak($"Used {itemName}", false);
                }
            }
        }

        private static string GetItemName(SavedItem item)
        {
            if (item == null) return "Unknown item";

            try
            {
                string name = item.GetPopupName();
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }
            catch { }

            try
            {
                CollectableItem collectableItem = item as CollectableItem;
                if (collectableItem != null)
                {
                    string name = collectableItem.GetDisplayName(ReadSource.GetPopup);
                    if (!string.IsNullOrEmpty(name))
                    {
                        return name;
                    }
                }
            }
            catch { }

            return item.name;
        }
    }
}