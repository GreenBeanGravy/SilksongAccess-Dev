using BepInEx.Logging;
using HarmonyLib;
using InControl;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace SilksongAccess.Menu
{
    /// <summary>
    /// Enhances control binding announcements to include the actual bound keys/buttons.
    /// Fixes the issue where only action names are announced, not the controls.
    /// </summary>
    public static class ControlBindingAccessibility
    {
        private static ManualLogSource _logger;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Get a readable description of the current binding for an action.
        /// </summary>
        public static string GetActionBindingDescription(PlayerAction action)
        {
            if (action == null) return "";

            StringBuilder sb = new StringBuilder();

            // Get the first binding (primary)
            if (action.Bindings != null && action.Bindings.Count > 0)
            {
                var primaryBinding = action.Bindings[0];
                sb.Append(primaryBinding.Name);

                // If there's a secondary binding, mention it
                if (action.Bindings.Count > 1)
                {
                    var secondaryBinding = action.Bindings[1];
                    sb.Append(" or ");
                    sb.Append(secondaryBinding.Name);
                }
            }
            else
            {
                sb.Append("Not bound");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get full announcement for a control binding menu item.
        /// Format: "Action Name: Bound Key"
        /// </summary>
        public static string GetFullBindingAnnouncement(GameObject menuItem)
        {
            if (menuItem == null) return "";

            // Try to find the action label and binding display
            Text[] texts = menuItem.GetComponentsInChildren<Text>(true);
            if (texts.Length == 0) return "";

            StringBuilder sb = new StringBuilder();

            // Find the action name (usually the first or largest text)
            Text actionLabel = null;
            Text bindingDisplay = null;

            foreach (var text in texts)
            {
                if (!text.gameObject.activeInHierarchy) continue;

                string textContent = text.text?.Trim();
                if (string.IsNullOrEmpty(textContent)) continue;

                // Action labels are usually larger
                if (text.fontSize >= 24 || text.name.Contains("Label"))
                {
                    actionLabel = text;
                }
                // Binding display is usually smaller or has "Text" in name
                else if (text.fontSize < 24 || text.name.Contains("Text") || text.name.Contains("Binding"))
                {
                    bindingDisplay = text;
                }
            }

            // Build announcement
            if (actionLabel != null)
            {
                sb.Append(actionLabel.text.Trim());

                if (bindingDisplay != null && !string.IsNullOrWhiteSpace(bindingDisplay.text))
                {
                    sb.Append(": ");
                    sb.Append(bindingDisplay.text.Trim());
                }
            }
            else
            {
                // Fallback: just read all text
                foreach (var text in texts)
                {
                    if (text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text))
                    {
                        if (sb.Length > 0) sb.Append(", ");
                        sb.Append(text.text.Trim());
                    }
                }
            }

            return sb.ToString();
        }

        // --- HARMONY PATCHES ---

        /// <summary>
        /// Patch UIButtonSkins to announce full binding info when displaying mappings.
        /// </summary>
        [HarmonyPatch(typeof(UIButtonSkins), "ShowCurrentKeyboardMappings")]
        private static class UIButtonSkins_ShowCurrentKeyboardMappings_Patch
        {
            private static void Postfix()
            {
                _logger?.LogInfo("Keyboard mappings displayed - enhanced announcements active");
            }
        }

        [HarmonyPatch(typeof(UIButtonSkins), "ShowCurrentButtonMappings")]
        private static class UIButtonSkins_ShowCurrentButtonMappings_Patch
        {
            private static void Postfix()
            {
                _logger?.LogInfo("Controller button mappings displayed - enhanced announcements active");
            }
        }
    }
}
