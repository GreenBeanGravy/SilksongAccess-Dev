using BepInEx.Logging;
using HarmonyLib;
using HutongGames.PlayMaker;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMProOld;
using UnityEngine;
using static CollectableItem;

namespace SilksongAccess.Gameplay
{
    public static class ShopAccessibility
    {
        private static ManualLogSource _logger;
        private static string _currentShopTitle = "";
        private static int _lastAnnouncedIndex = -1;
        private static int _lastAnnouncedSubIndex = -1;
        private static ShopItem _lastAnnouncedItem = null;
        private static ShopMenuStock _currentShop = null;
        private static bool _isInConfirmationDialog = false;
        private static float _shopOpenTime = 0f;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        public static void SetInConfirmationDialog(bool inDialog)
        {
            _isInConfirmationDialog = inDialog;
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

        private static void AnnounceShopItem(ShopItemStats itemStats, List<ShopItemStats> allItems)
        {
            if (itemStats == null) return;

            ShopItem item = itemStats.Item;
            if (item == null) return;

            // Don't announce if shop isn't visible
            if (_currentShop == null || !_currentShop.gameObject.activeInHierarchy) return;

            // Don't announce if in confirmation dialog
            if (_isInConfirmationDialog) return;

            // Don't announce too soon after shop opens (prevents initialization spam)
            if (Time.time - _shopOpenTime < 0.2f) return;

            int currentIndex = allItems.IndexOf(itemStats);
            if (currentIndex == _lastAnnouncedIndex && item == _lastAnnouncedItem) return;

            _lastAnnouncedIndex = currentIndex;
            _lastAnnouncedItem = item;
            _lastAnnouncedSubIndex = -1;

            StringBuilder sb = new StringBuilder();

            string itemName = item.DisplayName;
            if (!string.IsNullOrEmpty(itemName))
            {
                sb.Append(itemName);
            }

            if (allItems.Count > 1)
            {
                sb.Append($", {currentIndex + 1} of {allItems.Count}");
            }

            if (item.IsPurchased)
            {
                sb.Append(", Purchased");
            }
            else if (!item.IsAvailable)
            {
                sb.Append(", Not Available");
            }
            else
            {
                int cost = item.Cost;
                string currencyName = item.CurrencyType == CurrencyType.Money ? "Rosaries" : "Shards";
                sb.Append($", Cost {cost} {currencyName}");

                int currentCurrency = 0;
                if (item.CurrencyType == CurrencyType.Money)
                {
                    currentCurrency = PlayerData.instance.geo;
                }
                else
                {
                    currentCurrency = PlayerData.instance.ShellShards;
                }
                sb.Append($", Have {currentCurrency} {currencyName}");

                if (item.RequiredItem != null)
                {
                    string requiredItemName = GetItemName(item.RequiredItem);
                    sb.Append($", Requires {item.RequiredItemAmount} {requiredItemName}");
                }

                if (item.HasSubItems)
                {
                    sb.Append($", {item.SubItemsCount} variants");
                }

                string description = item.Description;
                if (!string.IsNullOrEmpty(description))
                {
                    description = description.Replace("\n", " ").Trim();
                    if (!string.IsNullOrEmpty(description))
                    {
                        sb.Append($", {description}");
                    }
                }
            }

            SpeechSynthesizer.Speak(sb.ToString(), true);
        }

        private static void AnnounceSubItem(ShopItem item, int subIndex)
        {
            if (item == null || subIndex < 0 || subIndex >= item.SubItemsCount) return;
            if (subIndex == _lastAnnouncedSubIndex && item == _lastAnnouncedItem) return;

            _lastAnnouncedSubIndex = subIndex;
            _lastAnnouncedItem = item;

            ShopItem.SubItem subItem = item.GetSubItem(subIndex);
            StringBuilder sb = new StringBuilder();

            string variantName = subItem.Value.ToString();
            if (!string.IsNullOrEmpty(variantName) && variantName != "None")
            {
                sb.Append(variantName);
                sb.Append(", ");
            }

            sb.Append($"{subIndex + 1} of {item.SubItemsCount}");

            SpeechSynthesizer.Speak(sb.ToString(), true);
        }

        private static string CleanShopTitle(string rawTitle)
        {
            if (string.IsNullOrEmpty(rawTitle))
            {
                return "Shop";
            }

            string cleanedTitle = rawTitle;

            if (cleanedTitle.StartsWith("!!") && cleanedTitle.EndsWith("!!"))
            {
                cleanedTitle = cleanedTitle.Substring(2, cleanedTitle.Length - 4);

                int lastSlash = cleanedTitle.LastIndexOf('/');
                if (lastSlash != -1)
                {
                    cleanedTitle = cleanedTitle.Substring(lastSlash + 1);
                }

                int lastUnderscore = cleanedTitle.LastIndexOf('_');
                if (lastUnderscore != -1)
                {
                    cleanedTitle = cleanedTitle.Substring(lastUnderscore + 1);
                }
            }

            cleanedTitle = cleanedTitle.Replace('_', ' ').ToLower();
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            return textInfo.ToTitleCase(cleanedTitle).Trim();
        }

        [HarmonyPatch(typeof(ShopMenuStock), "Start")]
        private static class ShopMenuStock_Start_Patch
        {
            private static void Postfix(ShopMenuStock __instance)
            {
                _currentShop = __instance;
                _currentShopTitle = "";
                _lastAnnouncedIndex = -1;
                _lastAnnouncedItem = null;
                _lastAnnouncedSubIndex = -1;
            }
        }

        [HarmonyPatch(typeof(ShopMenuStock), "BuildItemList")]
        private static class ShopMenuStock_BuildItemList_Patch
        {
            private static void Postfix(ShopMenuStock __instance)
            {
                _currentShop = __instance;
                _shopOpenTime = Time.time;
                _isInConfirmationDialog = false;

                string shopTitle = "";
                var titleField = Traverse.Create(__instance).Field<TMProOld.TextMeshPro>("titleText").Value;
                if (titleField != null && !string.IsNullOrEmpty(titleField.text))
                {
                    shopTitle = titleField.text;
                }
                else
                {
                    shopTitle = __instance.Title;
                }

                _currentShopTitle = CleanShopTitle(shopTitle);

                if (__instance.gameObject.activeInHierarchy && !string.IsNullOrEmpty(_currentShopTitle))
                {
                    SpeechSynthesizer.Speak($"{_currentShopTitle}", false);
                    Plugin.Instance.StartCoroutine(AnnounceSelectedItemDelayed());
                }
            }

            private static IEnumerator AnnounceSelectedItemDelayed()
            {
                yield return new WaitForSeconds(0.3f);

                if (_currentShop != null && _currentShop.gameObject.activeInHierarchy)
                {
                    var availableStock = Traverse.Create(_currentShop).Field<List<ShopItemStats>>("availableStock").Value;
                    if (availableStock != null && availableStock.Count > 0)
                    {
                        var shopFsm = _currentShop.GetComponent<PlayMakerFSM>();
                        int currentIndex = 0;
                        if (shopFsm != null)
                        {
                            var fsmInt = shopFsm.FsmVariables.FindFsmInt("Current Item");
                            if (fsmInt != null)
                            {
                                currentIndex = fsmInt.Value;
                            }
                        }

                        if (currentIndex >= 0 && currentIndex < availableStock.Count)
                        {
                            _lastAnnouncedIndex = -1;
                            AnnounceShopItem(availableStock[currentIndex], availableStock);
                        }
                        else
                        {
                            _lastAnnouncedIndex = -1;
                            AnnounceShopItem(availableStock[0], availableStock);
                        }
                    }
                }
            }
        }

        private static IEnumerator AnnounceShopItemDelayed(ShopItemStats itemStats, List<ShopItemStats> allItems)
        {
            yield return new WaitForEndOfFrame();

            if (_isInConfirmationDialog)
            {
                yield break;
            }

            AnnounceShopItem(itemStats, allItems);
        }


        [HarmonyPatch(typeof(FsmInt), "Value", MethodType.Setter)]
        private static class FsmInt_Value_Patch
        {
            private static void Postfix(FsmInt __instance)
            {
                if (_currentShop == null || __instance.Name != "Current Item") return;
                if (!_currentShop.gameObject.activeInHierarchy) return;

                try
                {
                    int itemNum = __instance.Value;
                    var availableStock = Traverse.Create(_currentShop).Field<List<ShopItemStats>>("availableStock").Value;

                    if (availableStock != null && itemNum >= 0 && itemNum < availableStock.Count)
                    {
                        Plugin.Instance.StartCoroutine(AnnounceShopItemDelayed(availableStock[itemNum], availableStock));
                    }
                }
                catch { }
            }
        }

        [HarmonyPatch(typeof(ShopSubItemSelection), "SetItem")]
        private static class ShopSubItemSelection_SetItem_Patch
        {
            private static void Postfix(ShopSubItemSelection __instance, GameObject itemObj, int initialSelection)
            {
                if (itemObj == null) return;

                ShopItemStats itemStats = itemObj.GetComponent<ShopItemStats>();
                if (itemStats == null || itemStats.Item == null) return;

                ShopItem item = itemStats.Item;
                string prompt = item.SubItemSelectPrompt;

                if (!string.IsNullOrEmpty(prompt))
                {
                    SpeechSynthesizer.Speak(prompt, false);
                }

                if (initialSelection >= 0 && initialSelection < item.SubItemsCount)
                {
                    AnnounceSubItem(item, initialSelection);
                }
            }
        }

        [HarmonyPatch(typeof(ShopSubItemSelection), "SetSelected")]
        private static class ShopSubItemSelection_SetSelected_Patch
        {
            private static void Prefix(ShopSubItemSelection __instance, int index)
            {
                var itemField = Traverse.Create(__instance).Field<ShopItemStats>("item").Value;
                if (itemField != null && itemField.Item != null)
                {
                    AnnounceSubItem(itemField.Item, index);
                }
            }
        }

        [HarmonyPatch(typeof(ShopItemStats), "SetPurchased")]
        private static class ShopItemStats_SetPurchased_Patch
        {
            private static void Postfix(ShopItemStats __instance)
            {
                if (__instance == null || __instance.Item == null) return;

                string itemName = __instance.Item.DisplayName ?? "Item";
                SpeechSynthesizer.Speak($"{itemName} purchased", false);
            }
        }

        [HarmonyPatch(typeof(ShopItemStats), "BuyFail")]
        private static class ShopItemStats_BuyFail_Patch
        {
            private static void Postfix()
            {
                SpeechSynthesizer.Speak("Cannot afford", false);
            }
        }
    }
}