using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Compatibility with RadRefinements (bryon82, com.raddude.radrefinements), which writes readings as
    /// text on instruments. Two of them told more than the instrument shows with this mod:
    ///  - the sun compass wrote the true latitude. Its shadow marks how far the sun stands from the
    ///    zenith, which is the latitude only when the sun is on the equator; with the axial tilt the
    ///    sun's declination is in it too. The text now gives the ring the shadow's tip lies on.
    ///  - a clock wrote the true time at the zero meridian and the local time. A chronometer gains or
    ///    loses here, so the text now gives the time its own hands show, and no local time (the dial
    ///    has none; it would give the longitude away).
    /// </summary>
    internal static class CompatRadRefinements
    {
        internal const string Guid = "com.raddude.radrefinements";
        internal static bool Loaded { get; private set; }

        // RadRefinements.InstrumentType
        private const int Clock = 3, SunCompass = 5;

        private static FieldInfo fText, fItem, fType;
        private static ConfigEntry<int> sunDecimals;
        private static ConfigEntry<bool> clockGlobal;

        internal static void Init(Harmony harmony)
        {
            Loaded = Chainloader.PluginInfos.ContainsKey(Guid);
            if (!Loaded) return;
            try
            {
                Type rt = AccessTools.TypeByName("RadRefinements.ReadingText");
                Type cfg = AccessTools.TypeByName("RadRefinements.Configs");
                MethodInfo late = rt != null ? AccessTools.Method(rt, "LateUpdate") : null;
                fText = rt != null ? AccessTools.Field(rt, "textMesh") : null;
                fItem = rt != null ? AccessTools.Field(rt, "shipItem") : null;
                fType = rt != null ? AccessTools.Field(rt, "instrumentType") : null;
                if (cfg != null)
                {
                    sunDecimals = AccessTools.Field(cfg, "sunCompassDecimalPlaces")?.GetValue(null) as ConfigEntry<int>;
                    clockGlobal = AccessTools.Field(cfg, "enableClockGlobalText")?.GetValue(null) as ConfigEntry<bool>;
                }
                if (late == null || fText == null || fItem == null || fType == null)
                {
                    Plugin.Log.LogWarning("RadRefinements found, but its reading text was not: RadRefinements changed? Its sun compass and clock texts are left as they are.");
                    return;
                }
                harmony.Patch(late, postfix: new HarmonyMethod(AccessTools.Method(typeof(CompatRadRefinements), nameof(After))));
                Plugin.Log.LogInfo("RadRefinements found: its sun compass and chronometer texts show what the instruments show.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("RadRefinements compatibility failed: " + e);
            }
        }

        private static void After(object __instance)
        {
            if (Plugin.configDisable.Value || Sun.sun == null) return;
            if (!(fText.GetValue(__instance) is TextMesh tm) || !tm.gameObject.activeSelf) return;
            int type = Convert.ToInt32(fType.GetValue(__instance));

            if (type == SunCompass)
            {
                tm.text = SunCompassReading().ToString("F" + (sunDecimals != null ? Mathf.Clamp(sunDecimals.Value, 0, 4) : 1)) + "°";
            }
            else if (type == Clock && Mode.Chronometers != Keeping.Perfect)
            {
                ShipItem item = fItem.GetValue(__instance) as ShipItem;
                if (item == null || !ChronoState.IsChrono(item)) return;
                if (clockGlobal != null && !clockGlobal.Value)
                {
                    tm.gameObject.SetActive(false);     // only the local time was asked for: the dial has none
                    return;
                }
                double t = ChronoState.Of(item).ShownHours;
                int minutes = (int)Math.Floor(t * 60.0) % (24 * 60);       // as the minute hand jumps
                tm.text = (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
            }
        }

        /// <summary>
        /// The sun compass ring the gnomon's shadow reaches: the sun's distance from the zenith, degrees;
        /// negative when the sun stands north of the zenith (as a southern latitude would read).
        /// </summary>
        private static float SunCompassReading()
        {
            Transform sky = Sun.sun.transform.parent != null ? Sun.sun.transform.parent : Sun.sun.transform;
            Vector3 toSun = -sky.forward;
            float z = Vector3.Angle(toSun, Vector3.up);
            return toSun.z > 0f ? -z : z;               // world +Z is north
        }
    }
}
