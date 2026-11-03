using System;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Seasons and equation of time for the sun.
    ///
    /// Sun.Update spins "sun parent" about its right axis by the local clock time and tilts it by the
    /// latitude, so the vanilla sun always sits on the celestial equator (Dec 0, noon at 12:00).
    /// After that we
    ///  1. remember the polar axis and the clock-sun direction (the moon uses them),
    ///  2. move the sun along the equator by the equation of time and north/south by its declination
    ///     by turning the whole "sun parent" (lights, sun disc, clouds light, zodiacal light go with it),
    ///  3. redo the end of Sun.Update (sun light angle, dawn/night lerps) from the sun's real height,
    ///     because vanilla times dawn and dusk by the clock, not by where the sun is.
    /// With tilt 0 and equation of time off nothing is changed.
    /// </summary>
    [HarmonyPatch(typeof(Sun), "Update")]
    internal static class SunTiltPatch
    {
        private static readonly AccessTools.FieldRef<Sun, float> NightLerp = AccessTools.FieldRefAccess<Sun, float>("nightLerp");
        private static readonly AccessTools.FieldRef<Sun, float> DawnLerp = AccessTools.FieldRefAccess<Sun, float>("dawnLerp");
        private static readonly AccessTools.FieldRef<Sun, Quaternion> InitialSunLightLocalRot = AccessTools.FieldRefAccess<Sun, Quaternion>("initialSunLightLocalRot");
        internal static readonly AccessTools.FieldRef<Sun, Transform> Player = AccessTools.FieldRefAccess<Sun, Transform>("player");

        [HarmonyPostfix]
        [HarmonyPriority(Priority.High)]
        private static void Postfix(Sun __instance)
        {
            Transform sunParent = __instance.transform.parent;
            if (sunParent == null)
            {
                return;
            }

            // 1. sky axes of the untouched vanilla sun
            Vector3 axis = sunParent.right;
            Vector3 meanSun = Vector3.ProjectOnPlane(-sunParent.forward, axis).normalized;
            Vector3 north = Vector3.Dot(axis, Vector3.forward) >= 0f ? axis : -axis;
            Celestial.PoleAxis = axis;
            Celestial.NorthPole = north;
            Celestial.MeanSunEq = meanSun;
            Celestial.HaveSkyAxes = true;

            if (!Celestial.SunReworkActive)
            {
                return;
            }

            // 2. move the sun
            double t = Celestial.Now;
            float eot = (float)Celestial.EquationOfTime(t);   // degrees west of the clock sun
            float dec = (float)Celestial.SunDec(t) * Mathf.Deg2Rad;

            // the sky turns by AngleAxis(-15 deg/h, axis), so "further west" is a negative angle
            Vector3 eq = Quaternion.AngleAxis(-eot, axis) * meanSun;
            Vector3 dir = (Mathf.Cos(dec) * eq + Mathf.Sin(dec) * north).normalized;

            Vector3 current = -sunParent.forward;
            sunParent.rotation = Quaternion.FromToRotation(current, dir) * sunParent.rotation;

            // 3a. sun light angle (same formula as the end of Sun.Update, with the new sun height)
            if (__instance.sunLight != null && __instance.dummySun != null)
            {
                float lightLerp = Mathf.InverseLerp(__instance.nightLightOffHeight - 500f, __instance.nightLightOffHeight, __instance.dummySun.position.y);
                __instance.sunLight.localRotation = Quaternion.Lerp(
                    Quaternion.Inverse(sunParent.rotation) * Quaternion.LookRotation(Vector3.up),
                    InitialSunLightLocalRot(__instance),
                    lightLerp);
            }

            // 3b. dawn / night: find the clock time at which the vanilla sun (Dec 0) would stand as high
            // as the real sun does now, and feed that to the vanilla curves
            Transform player = Player(__instance);
            if (player == null || FloatingOriginManager.instance == null)
            {
                return;
            }
            float lat = FloatingOriginManager.instance.GetGlobeCoords(player).z * Mathf.Deg2Rad;
            float cosLat = Mathf.Max(0.01f, Mathf.Cos(lat));
            float sinAlt = Mathf.Clamp(dir.y, -1f, 1f);
            float hourAngle = Mathf.Acos(Mathf.Clamp(sinAlt / cosLat, -1f, 1f)) * Mathf.Rad2Deg / 15f; // hours from noon

            float apparentLocal = Mathf.Repeat(__instance.localTime + eot / 15f, 24f);
            float lt = apparentLocal < 12f ? 12f - hourAngle : 12f + hourAngle;

            if (lt <= 12f)
            {
                NightLerp(__instance) = Mathf.InverseLerp(__instance.sunriseStart, __instance.dawnBorder, lt);
                DawnLerp(__instance) = Mathf.InverseLerp(__instance.dayBorder, __instance.dawnBorder, lt);
            }
            else
            {
                NightLerp(__instance) = Mathf.InverseLerp(24f - __instance.sunriseStart, 24f - __instance.dawnBorder, lt);
                DawnLerp(__instance) = Mathf.InverseLerp(24f - __instance.dayBorder, 24f - __instance.dawnBorder, lt);
            }
        }
    }

    /// <summary>Tilts the planets' ecliptic against the stars by the axial tilt.</summary>
    internal static class EclipticTilt
    {
        private static Quaternion baseRotation;
        private static bool captured;

        /// <summary>Called every frame from the sky update, so a config change applies at once.</summary>
        internal static void Apply()
        {
            Transform ecl = SkyboxManager.eclipticSkybox != null ? SkyboxManager.eclipticSkybox.transform : null;
            if (ecl == null)
            {
                return;
            }
            if (!captured)
            {
                baseRotation = ecl.localRotation;
                captured = true;
            }
            // ecliptic longitude 0 lies along local -Z and 90 along +X; turning about +Z lifts
            // longitude 90 to the north by the tilt (see the matching formula in Celestial.EclipticToEquatorial)
            ecl.localRotation = baseRotation * Quaternion.AngleAxis(Mode.AxialTilt, Vector3.forward);
        }
    }
}
