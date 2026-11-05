using System;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The game's quadrant writes its reading into the Navigator's Journal too: right click holds the
    /// plumb line against the scale (the game's own "inspect"), and that angle is the altitude.
    /// </summary>
    [HarmonyPatch(typeof(ShipItemQuadrant), "OnAltActivate")]
    internal static class QuadrantPatch
    {
        [HarmonyPostfix]
        private static void Note(ShipItemQuadrant __instance)
        {
            if (!__instance.sold) return;
            var t = Traverse.Create(__instance);
            if (!t.Field("inspecting").GetValue<bool>()) return;          // only when the line is clamped
            Transform dial = t.Field("dial").GetValue<Transform>();
            if (dial == null) return;
            float x = dial.localEulerAngles.x;
            if (x > 180f) x -= 360f;
            double alt = Math.Round(Mathf.Clamp(x, 0f, 90f) * 4.0) / 4.0;        // read to a quarter degree on its scale
            Camera cam = Camera.main;
            float look = cam != null ? Mathf.Asin(Mathf.Clamp(cam.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg : float.NaN;
            QuadrantPlumb pl = __instance.GetComponent<QuadrantPlumb>();
            Debug.Log("RealisticSkies: quadrant altitude" + Celestial.Format(alt) + "', looking up " + look.ToString("0.00") + " deg, plumb swing " +
                      (pl != null ? pl.SwingDeg.ToString("0.00") : "0") + " deg");
            NavJournal.NoteAltitude(alt);
        }
    }

    /// <summary>
    /// The quadrant's plumb line, a light bob on a long thread: it swings with the ship's motion and
    /// settles slowly, where the game's hangs dead plumb. In harbour it hardly matters; at
    /// sea the line wanders a few degrees, and you clamp it when it passes the middle. (The heavy
    /// astrolabe swings less and settles quicker: the deck instrument.)
    /// </summary>
    internal class QuadrantPlumb : MonoBehaviour
    {
        private const float Length = 0.30f, Damping = 0.05f;
        private float swing, rate;
        private Vector3 lastPos, lastVel;
        private bool haveLast;

        internal float SwingDeg => swing * Mathf.Rad2Deg;

        internal void Step(Camera cam)
        {
            float dt = Time.deltaTime;
            if (cam == null || dt <= 0f) return;
            Vector3 pos = cam.transform.position;
            if (!haveLast) { lastPos = pos; lastVel = Vector3.zero; haveLast = true; return; }
            Vector3 vel = (pos - lastPos) / dt, acc = (vel - lastVel) / dt;
            lastPos = pos; lastVel = vel;
            if (acc.magnitude > 30f || vel.magnitude > 50f) acc = Vector3.zero;       // the world was moved (floating origin)
            Vector3 along = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            float a = along.sqrMagnitude > 1e-4f ? Vector3.Dot(acc, along.normalized) : 0f;
            float w = Mathf.Sqrt(9.81f / Length);
            int n = Mathf.Clamp(Mathf.CeilToInt(dt / 0.005f), 1, 20);
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                float accel = -w * w * Mathf.Sin(swing) - 2f * Damping * w * rate - a / Length;
                rate += accel * h;
                swing += rate * h;
            }
            swing = Mathf.Clamp(swing, -0.35f, 0.35f);
        }

        internal void Settle()
        {
            swing = rate = 0f;
            haveLast = false;
        }
    }

    [HarmonyPatch(typeof(ShipItemQuadrant), "ExtraLateUpdate")]
    internal static class QuadrantSwingPatch
    {
        [HarmonyPostfix]
        private static void Swing(ShipItemQuadrant __instance)
        {
            if (!__instance.sold) return;
            if (__instance.held != null) InstrumentAim.Mark();
            var t = Traverse.Create(__instance);
            QuadrantPlumb pl = __instance.GetComponent<QuadrantPlumb>() ?? __instance.gameObject.AddComponent<QuadrantPlumb>();
            if (__instance.held == null) { pl.Settle(); return; }
            if (t.Field("inspecting").GetValue<bool>() || t.Field("rotating").GetValue<bool>()) return;     // clamped: the line stays
            pl.Step(Camera.main);
            Transform dial = t.Field("dial").GetValue<Transform>();
            if (dial == null) return;
            // the game has just set the line plumb: tip it by the swing
            Vector3 e = dial.localEulerAngles;
            float x = e.x > 180f ? e.x - 360f : e.x;
            e.x = Mathf.Clamp(x + pl.SwingDeg, 0f, 88f);
            dial.localEulerAngles = e;
        }
    }
}
