using BepInEx.Logging;
using HarmonyLib;
using System.Text;
using UnityEngine;
using GlobalEnums;
using System.Globalization;

namespace SilksongAccess.Menu
{
    public static class MapAccessibility
    {
        private static ManualLogSource _logger;
        private static string _lastAnnouncedMarkerAction = "";
        private static int _lastAnnouncedMarkerIndex = -1;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        private static string GetFormattedZoneName(MapZone zone)
        {
            if (zone == MapZone.NONE) return "current area";
            // Format the enum name to be more readable
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(zone.ToString().ToLower().Replace('_', ' '));
        }

        [HarmonyPatch(typeof(InventoryMapManager), "ZoomIn")]
        private static class InventoryMapManager_ZoomIn_Patch
        {
            private static void Postfix(MapZone mapZone)
            {
                SpeechSynthesizer.Speak($"Zooming in to {GetFormattedZoneName(mapZone)}", false);
            }
        }

        [HarmonyPatch(typeof(InventoryMapManager), "ZoomOut")]
        private static class InventoryMapManager_ZoomOut_Patch
        {
            private static void Postfix()
            {
                SpeechSynthesizer.Speak("Zooming out to world map", false);
            }
        }

        [HarmonyPatch(typeof(MapMarkerMenu), "Open")]
        private static class MapMarkerMenu_Open_Patch
        {
            private static void Postfix()
            {
                _lastAnnouncedMarkerIndex = -1; // Reset on open
                SpeechSynthesizer.Speak("Map marker menu opened", true);
            }
        }

        [HarmonyPatch(typeof(MapMarkerMenu), "Close")]
        private static class MapMarkerMenu_Close_Patch
        {
            private static void Postfix()
            {
                SpeechSynthesizer.Speak("Map marker menu closed", true);
            }
        }

        [HarmonyPatch(typeof(MapMarkerMenu), "MarkerSelect")]
        private static class MapMarkerMenu_MarkerSelect_Patch
        {
            private static void Postfix(MapMarkerMenu __instance, int selection)
            {
                if (selection == _lastAnnouncedMarkerIndex) return;
                _lastAnnouncedMarkerIndex = selection;

                var amounts = Traverse.Create(__instance).Field<TMProOld.TextMeshPro[]>("amounts").Value;
                if (amounts != null && selection >= 0 && selection < amounts.Length)
                {
                    string markerType = ((MapMarkerMenu.MarkerTypes)selection).ToString();
                    string amountText = amounts[selection].text;
                    SpeechSynthesizer.Speak($"Marker {markerType}, {amountText} remaining", true);
                }
            }
        }

        [HarmonyPatch(typeof(MapMarkerMenu), "PlaceMarker")]
        private static class MapMarkerMenu_PlaceMarker_Patch
        {
            private static void Postfix()
            {
                SpeechSynthesizer.Speak("Marker placed", false);
            }
        }

        [HarmonyPatch(typeof(MapMarkerMenu), "RemoveMarker")]
        private static class MapMarkerMenu_RemoveMarker_Patch
        {
            private static void Postfix()
            {
                SpeechSynthesizer.Speak("Marker removed", false);
            }
        }

        [HarmonyPatch(typeof(MapMarkerMenu), "IsColliding")]
        private static class MapMarkerMenu_IsColliding_Patch
        {
            private static void Postfix()
            {
                if (_lastAnnouncedMarkerAction != "Remove")
                {
                    _lastAnnouncedMarkerAction = "Remove";
                    SpeechSynthesizer.Speak("Remove marker", true);
                }
            }
        }

        [HarmonyPatch(typeof(MapMarkerMenu), "IsNotColliding")]
        private static class MapMarkerMenu_IsNotColliding_Patch
        {
            private static void Postfix()
            {
                if (_lastAnnouncedMarkerAction != "Place")
                {
                    _lastAnnouncedMarkerAction = "Place";
                    SpeechSynthesizer.Speak("Place marker", true);
                }
            }
        }

        [HarmonyPatch(typeof(MapPin), "CycleState")]
        private static class MapPin_CycleState_Patch
        {
            private static void Postfix()
            {
                // The state is cycled *before* this postfix runs, so we read the *next* state to announce what will be shown.
                MapPin.PinVisibilityStates nextState = MapPin.GetNextState(MapPin.CurrentState);
                string announcement = "";
                switch (nextState)
                {
                    case MapPin.PinVisibilityStates.PinsAndKey:
                        announcement = "Showing all pins and key";
                        break;
                    case MapPin.PinVisibilityStates.Pins:
                        announcement = "Showing pins only";
                        break;
                    case MapPin.PinVisibilityStates.None:
                        announcement = "Hiding all pins";
                        break;
                }
                SpeechSynthesizer.Speak(announcement, false);
            }
        }
    }
}