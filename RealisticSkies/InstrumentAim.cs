using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// A steadier hand at the instrument. While an instrument is up to the eye (the quintant held, the
    /// cross-staff, astrolabe or nocturnal raised, the quadrant held), the look is slowed in step with
    /// the instrument's narrower view and the mouse's jitter smoothed out, so a star or the Moon's edge
    /// can be brought in and held. Config: Instruments / Aim Sensitivity, Aim Smoothing.
    /// </summary>
    internal static class InstrumentAim
    {
        private static int markedFrame = -100;

        /// <summary>Called every frame by an instrument that is being aimed.</summary>
        internal static void Mark() => markedFrame = Time.frameCount;

        internal static bool Active => Time.frameCount - markedFrame <= 2;
    }

    /// <summary>
    /// The game's look runs as ever; what it turned this frame is then taken back and given again
    /// slowed and smoothed. (Two look components: the body turns, the head nods.)
    /// </summary>
    [HarmonyPatch(typeof(MouseLook), "Update")]
    internal static class InstrumentAimLookPatch
    {
        private static readonly AccessTools.FieldRef<MouseLook, float> RotationY = AccessTools.FieldRefAccess<MouseLook, float>("rotationY");

        private struct Before { public float yaw, rotY; }
        private static float smoothYaw, smoothNod;          // degrees a frame, smoothed

        [HarmonyPrefix]
        private static void Pre(MouseLook __instance, out Before __state)
        {
            __state = new Before { yaw = __instance.transform.localEulerAngles.y, rotY = RotationY(__instance) };
        }

        [HarmonyPostfix]
        private static void Post(MouseLook __instance, Before __state)
        {
            if (StudyMode.Active)
            {
                // studying a chart: the look holds still, and what it would have turned pans the page
                Transform st = __instance.transform;
                if (__instance.axes != MouseLook.RotationAxes.MouseY)
                {
                    StudyMode.Look(Mathf.DeltaAngle(__state.yaw, st.localEulerAngles.y), 0f);
                    Vector3 e0 = st.localEulerAngles; e0.y = __state.yaw; st.localEulerAngles = e0;
                }
                if (__instance.axes != MouseLook.RotationAxes.MouseX)
                {
                    StudyMode.Look(0f, RotationY(__instance) - __state.rotY);
                    RotationY(__instance) = __state.rotY;
                    Vector3 e1 = st.localEulerAngles; e1.x = -__state.rotY; st.localEulerAngles = e1;
                }
                smoothYaw = smoothNod = 0f;
                return;
            }
            if (ModItemTraverseBoard.LookFrozen)
            {
                // working a traverse board: the look holds still, and what it would have turned moves the view over the board
                Transform st = __instance.transform;
                if (__instance.axes != MouseLook.RotationAxes.MouseY)
                {
                    ModItemTraverseBoard.LookTurn(Mathf.DeltaAngle(__state.yaw, st.localEulerAngles.y), 0f);
                    Vector3 e0 = st.localEulerAngles; e0.y = __state.yaw; st.localEulerAngles = e0;
                }
                if (__instance.axes != MouseLook.RotationAxes.MouseX)
                {
                    ModItemTraverseBoard.LookTurn(0f, RotationY(__instance) - __state.rotY);
                    RotationY(__instance) = __state.rotY;
                    Vector3 e1 = st.localEulerAngles; e1.x = -__state.rotY; st.localEulerAngles = e1;
                }
                smoothYaw = smoothNod = 0f;
                return;
            }
            if (ModItemRingDial.LookFrozen)
            {
                // sighting the ring dial: the look holds still, and what it would have turned turns the dial
                Transform st = __instance.transform;
                if (__instance.axes != MouseLook.RotationAxes.MouseY)
                {
                    ModItemRingDial.LookTurn(Mathf.DeltaAngle(__state.yaw, st.localEulerAngles.y));
                    Vector3 e0 = st.localEulerAngles; e0.y = __state.yaw; st.localEulerAngles = e0;
                }
                if (__instance.axes != MouseLook.RotationAxes.MouseX)
                {
                    RotationY(__instance) = __state.rotY;
                    Vector3 e1 = st.localEulerAngles; e1.x = -__state.rotY; st.localEulerAngles = e1;
                }
                smoothYaw = smoothNod = 0f;
                return;
            }
            if (!InstrumentAim.Active || Plugin.configAimSensitivity == null)
            {
                smoothYaw = smoothNod = 0f;
                return;
            }
            Camera cam = Camera.main;
            float fov = cam != null ? cam.fieldOfView : 60f;
            float slow = Mathf.Clamp(Plugin.configAimSensitivity.Value * fov / 60f, 0.05f, 1f);
            float tau = Mathf.Max(0f, Plugin.configAimSmoothing.Value);
            float k = tau <= 0f ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime / tau);
            Transform t = __instance.transform;
            if (__instance.axes != MouseLook.RotationAxes.MouseY)
            {
                float d = Mathf.DeltaAngle(__state.yaw, t.localEulerAngles.y);
                smoothYaw = Mathf.Lerp(smoothYaw, d * slow, k);
                Vector3 e = t.localEulerAngles;
                e.y = __state.yaw + smoothYaw;
                t.localEulerAngles = e;
            }
            if (__instance.axes != MouseLook.RotationAxes.MouseX)
            {
                float now = RotationY(__instance);
                float d = now - __state.rotY;
                smoothNod = Mathf.Lerp(smoothNod, d * slow, k);
                float rotY = Mathf.Clamp(__state.rotY + smoothNod, __instance.minimumY, __instance.maximumY);
                RotationY(__instance) = rotY;
                Vector3 e = t.localEulerAngles;
                e.x = -rotY;
                t.localEulerAngles = e;
            }
        }
    }
}
