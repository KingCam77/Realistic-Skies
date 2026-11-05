using System;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Makes the moon deterministic and gives it an orbit that the almanac can tabulate.
    ///
    /// Vanilla behaviour (Moon.Update):
    ///  * currentPhase += dt * timescale / 24 / 28 every frame - it keeps running while the sun is
    ///    paused (shops, shipyard), so it slowly drifts away from the game clock;
    ///  * "moon parent" is a child of "sun parent", rotated 360 * phase about the polar axis from an
    ///    initial rotation that is yawed ~12 deg, so the orbit is inclined but its nodes are locked to
    ///    the sun (every full moon has the same declination).
    ///
    /// With the rework the phase is computed from day + time, and the moon is placed from
    /// Celestial.MoonElongation / MoonDec, measured from the real sun direction and polar axis.
    /// The vanilla lighting / colour code still runs; we only move the moon afterwards.
    /// </summary>
    [HarmonyPatch(typeof(Moon))]
    internal static class MoonOrbitPatch
    {
        private static Renderer moonRenderer;

        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        private static void AwakePostfix(Moon __instance)
        {
            moonRenderer = __instance.GetComponent<Renderer>();
        }

        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        private static void UpdatePrefix(Moon __instance)
        {
            if (!Celestial.MoonReworkActive || Sun.sun == null)
            {
                return;
            }
            // stop vanilla accumulation and drive the phase from the game clock instead
            __instance.debugSpeedOverride = 0f;
            __instance.currentPhase = (float)Celestial.MoonPhase(Celestial.Now);
        }

        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        private static void UpdatePostfix(Moon __instance)
        {
            if (!Celestial.MoonReworkActive || Sun.sun == null)
            {
                return;
            }

            Transform moon = __instance.transform;
            Transform moonParent = moon.parent;
            Transform sunParent = Sun.sun.transform.parent;
            if (moonParent == null || sunParent == null)
            {
                return;
            }

            double t = Celestial.Now;
            Vector3 dir = MoonDirection(sunParent, (float)Celestial.MoonElongation(t), (float)Celestial.MoonDec(t));

            // rotate the whole moon parent so the moon (and its child lights) point along dir
            Vector3 current = (moon.position - moonParent.position).normalized;
            moonParent.rotation = Quaternion.FromToRotation(current, dir) * moonParent.rotation;
            moon.LookAt(__instance.sunLocation);

            // vanilla tints the moon by its height before we moved it; redo that with the new height
            if (moonRenderer != null)
            {
                float y = moon.position.y;
                float lerp = Mathf.InverseLerp(__instance.dayStartHeight, __instance.dayHeight, y);
                Color c;
                if (lerp > 0f)
                {
                    c = Color.Lerp(__instance.moonriseColor, __instance.moonDayColor, lerp);
                }
                else
                {
                    lerp = Mathf.InverseLerp(0f, __instance.dayStartHeight, y);
                    c = Color.Lerp(__instance.horizonFadeColor, __instance.moonriseColor, lerp);
                }
                moonRenderer.material.SetColor("_EmissionColor", c);
            }
        }

        /// <summary>Celestial north pole as a world direction.</summary>
        internal static Vector3 NorthPole(Transform sunParent)
        {
            // the sun turns about sunParent.right; world +Z is north in Sailwind
            Vector3 pole = sunParent.right;
            return Vector3.Dot(pole, Vector3.forward) >= 0f ? pole : -pole;
        }

        /// <summary>
        /// World direction of a body that is 'elongation' degrees east of the sun along the equator,
        /// with the given declination.
        /// </summary>
        internal static Vector3 MoonDirection(Transform sunParent, float elongation, float dec)
        {
            // Sun.Update spins sunParent about its own right axis by -15 deg per hour, so the sun moves
            // by AngleAxis(-15*dt, right). A body further east is where the sun was earlier,
            // i.e. AngleAxis(+E, right) applied to the sun direction.
            // use the axes captured before the sun was moved for seasons / equation of time
            Vector3 axis = Celestial.HaveSkyAxes ? Celestial.PoleAxis : sunParent.right;
            Vector3 sunDir = Celestial.HaveSkyAxes
                ? Celestial.MeanSunEq
                : Vector3.ProjectOnPlane(-sunParent.forward, axis).normalized;
            Vector3 eq = Quaternion.AngleAxis(elongation, axis) * sunDir;

            Vector3 north = Celestial.HaveSkyAxes ? Celestial.NorthPole : NorthPole(sunParent);
            float d = dec * Mathf.Deg2Rad;
            return (Mathf.Cos(d) * eq + Mathf.Sin(d) * north).normalized;
        }
    }
}
