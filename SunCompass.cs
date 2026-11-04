using System;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The game's sun compass reads the latitude from the noon shadow of its needle, its scale made
    /// for a Sun that never leaves the equator. With the seasons the noon Sun stands higher or lower
    /// by its declination, and the reading is out by just that much. So the dial can be set for the
    /// day: scroll while holding it to tilt the dial by the Sun's declination from the Almanac
    /// (Shift: finely), and it reads true again. Kept in the save's spare item value.
    /// </summary>
    internal class SunCompassSetting : MonoBehaviour
    {
        internal float dec;                 // degrees, north positive
        internal Quaternion baseActive;     // the dial's unfolded pose as the game made it
        internal bool haveBase;
    }

    [HarmonyPatch]
    internal static class SunCompassPatches
    {
        private const float Step = 0.5f, FineStep = 0.1f, Max = 30f;
        // which way the tilt goes on the dial (+1 or -1); set once by testing in the game
        internal static float Sense = 1f;

        private static readonly AccessTools.FieldRef<ShipItemCompass, Quaternion> ActiveRot =
            AccessTools.FieldRefAccess<ShipItemCompass, Quaternion>("sundialActiveRot");

        private static SunCompassSetting Of(ShipItemCompass c)
        {
            SunCompassSetting s = c.GetComponent<SunCompassSetting>();
            if (s == null) s = c.gameObject.AddComponent<SunCompassSetting>();
            return s;
        }

        private static void Apply(ShipItemCompass c, SunCompassSetting s)
        {
            if (!s.haveBase || c.sunCompassSundial == null) return;
            ActiveRot(c) = s.baseActive * Quaternion.Euler(Sense * s.dec, 0f, 0f);
        }

        [HarmonyPatch(typeof(ShipItemCompass), "OnLoad")]
        [HarmonyPostfix]
        private static void Loaded(ShipItemCompass __instance)
        {
            if (__instance.sunCompassSundial == null) return;
            SunCompassSetting s = Of(__instance);
            if (!s.haveBase)
            {
                s.baseActive = ActiveRot(__instance);         // set by the game's OnLoad just now
                s.haveBase = true;
            }
            Apply(__instance, s);
        }

        [HarmonyPatch(typeof(ShipItemCompass), "OnScroll")]
        [HarmonyPrefix]
        private static bool Scroll(ShipItemCompass __instance, float input)
        {
            if (__instance.sunCompassSundial == null || !__instance.sold || __instance.held == null || input == 0f) return true;
            // a compass with the game's own latitude dial keeps its scroll; the declination is then on Ctrl
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (__instance.chronoLatitude != null && !ctrl) return true;
            SunCompassSetting s = Of(__instance);
            bool fine = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            s.dec = Mathf.Clamp(Mathf.Round((s.dec + Mathf.Sign(input) * (fine ? FineStep : Step)) * 10f) / 10f, -Max, Max);
            Apply(__instance, s);
            UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.06f, 2.6f);
            try
            {
                NotificationUi.instance.ShowNotification("Sun compass set for the Sun's declination " +
                    Mathf.Abs(s.dec).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "° " + (s.dec >= 0f ? "N" : "S"));
            }
            catch (Exception) { }
            return false;
        }

        [HarmonyPatch(typeof(SaveablePrefab), "PrepareSaveData")]
        [HarmonyPostfix]
        private static void Save(SaveablePrefab __instance, SavePrefabData __result)
        {
            if (__result == null) return;
            SunCompassSetting s = __instance.GetComponent<SunCompassSetting>();
            if (s != null) __result.extraValue0 = s.dec;
        }

        [HarmonyPatch(typeof(SaveablePrefab), "Load")]
        [HarmonyPostfix]
        private static void Load(SaveablePrefab __instance, SavePrefabData data)
        {
            if (data == null) return;
            ShipItemCompass c = __instance.GetComponent<ShipItemCompass>();
            if (c == null || c.sunCompassSundial == null) return;
            SunCompassSetting s = Of(c);
            s.dec = Mathf.Clamp(data.extraValue0, -Max, Max);
            Apply(c, s);
        }
    }
}
