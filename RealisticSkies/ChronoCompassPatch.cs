using System;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    internal class ChronoAlmanacCalibration : MonoBehaviour
    {
        public float decOffset;
        public float eotOffset;
    }

    [HarmonyPatch]
    internal static class ChronoCompassPatches
    {
        private const float Step = 0.5f;

        private static ChronoAlmanacCalibration GetCalibration(ChronometerLatitude cl)
        {
            var c = cl.GetComponent<ChronoAlmanacCalibration>();
            if (c == null) c = cl.gameObject.AddComponent<ChronoAlmanacCalibration>();
            return c;
        }

        [HarmonyPatch(typeof(ShipItemCompass), "OnScroll")]
        [HarmonyPrefix]
        private static bool OnCompassScroll(ShipItemCompass __instance, float input)
        {
            if (__instance.chronoLatitude == null || !__instance.sold || __instance.held == null || input == 0f)
                return true;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            // 1. Shift + Alt + Scroll -> Заводка пружины хронокомпаса
            if (shift && alt)
            {
                if (input <= 0f) return false;

                Keeping k = Mode.Chronometers;
                if (k == Keeping.Perfect) return false;

                ChronoState s = ChronoState.Of(__instance);
                if (s.Dead)
                {
                    NotificationUi.instance.ShowNotification("It has stopped for good: no one aboard can mend it.");
                    return false;
                }
                if (k == Keeping.Relic || Mode.CabinBoyWinds) return false;

                s.Advance(true);
                if (s.Wind(1))
                {
                    UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.15f, 1.8f);
                    if (s.runLeft >= ChronoState.MaxRun)
                        NotificationUi.instance.ShowNotification("The chronometer is fully wound.");
                }
                return false; // Блокируем наклон кольца широты при заводке!
            }

            // 2. Alt + Scroll -> Ручная настройка EoT (долгота)
            if (alt)
            {
                var cal = GetCalibration(__instance.chronoLatitude);
                cal.eotOffset = Mathf.Clamp(
                    Mathf.Round((cal.eotOffset + Mathf.Sign(input) * Step) * 10f) / 10f,
                    -15f, 15f
                );

                UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.08f, 2.5f);

                float eotMinutes = cal.eotOffset * 4f;
                string sign = cal.eotOffset >= 0 ? "+" : "";
                NotificationUi.instance.ShowNotification(
                    $"Almanac EoT: {sign}{cal.eotOffset:0.0}° ({sign}{eotMinutes:0.0} min)"
                );
                return false;
            }

            // 3. Shift + Scroll -> Ручная настройка Dec (широта)
            if (shift)
            {
                var cal = GetCalibration(__instance.chronoLatitude);
                cal.decOffset = Mathf.Clamp(
                    Mathf.Round((cal.decOffset + Mathf.Sign(input) * Step) * 10f) / 10f,
                    -25f, 25f
                );

                UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.08f, 2.2f);

                string hemi = cal.decOffset >= 0f ? "N" : "S";
                NotificationUi.instance.ShowNotification(
                    $"Almanac Sun Dec: {Mathf.Abs(cal.decOffset):0.0}° {hemi}"
                );
                return false;
            }

            return true; // Обычный ванильный скролл без клавиш -> наклон кольца широты
        }

        [HarmonyPatch(typeof(ChronometerTime), "Update")]
        [HarmonyPostfix]
        private static void AdjustChronometerTime(ChronometerTime __instance)
        {
            var compass = __instance.GetComponentInParent<ShipItemCompass>();
            float chronoErrorDeg = 0f;

            if (compass != null)
            {
                var s = ChronoState.Of(compass);
                if (s != null)
                {
                    s.Advance(compass.sold);
                    if (Mode.Chronometers != Keeping.Perfect)
                        chronoErrorDeg = s.error * 15f;
                }
            }

            var cal = compass?.GetComponentInChildren<ChronoAlmanacCalibration>();
            float eotDegrees = (cal != null && Mathf.Abs(cal.eotOffset) > 0.01f) ? cal.eotOffset : 0f;

            float totalOffset = chronoErrorDeg + eotDegrees;
            if (Mathf.Abs(totalOffset) < 0.001f) return;

            Vector3 angles = __instance.transform.localEulerAngles;
            angles.z += totalOffset;
            __instance.transform.localEulerAngles = angles;
        }

        [HarmonyPatch(typeof(ChronometerLatitude), "Update")]
        [HarmonyPostfix]
        private static void AdjustLatitudeDial(ChronometerLatitude __instance)
        {
            if (__instance.latitudeDial == null) return;

            var cal = __instance.GetComponent<ChronoAlmanacCalibration>();
            if (cal == null || Mathf.Abs(cal.decOffset) < 0.01f) return;

            float dialShift = cal.decOffset * 2.5f;
            Vector3 euler = __instance.latitudeDial.localEulerAngles;
            euler.y -= dialShift;
            __instance.latitudeDial.localEulerAngles = euler;
        }
    }
}