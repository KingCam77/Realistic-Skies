using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Studying a chart: hold Shift + right click with the Star Map unfolded, or the Almanac or the
    /// Celestial Atlas open, in hand. It comes up close to the eye (Instruments / Study Zoom), the look
    /// stops, and moving the mouse glides over the page. Let go and it goes back into the hand.
    /// </summary>
    internal static class StudyMode
    {
        private static int markedFrame = -100;
        private static PickupableItem item;
        private static Vector2 pan, panSmooth;          // metres, in the view's right/up
        private static float blend;                     // 0 in the hand .. 1 at the eye
        private const float PanPerDegree = 0.004f;      // metres of page per degree the mouse would turn the look
        private const float BlendTime = 0.18f;

        internal static bool Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        /// <summary>The items that can be studied, in the state that can be.</summary>
        internal static bool Studiable(Component c)
        {
            if (c is ModItemAlmanac a) return a.sold && a.amount > 0f;
            if (c is ModItemCharts at) return at.sold && at.amount > 0f;
            if (c is ShipItemFoldable f) return f.sold && f.amount <= 0f && IsStarMap(f);
			if (c is ModItemAstrolabe || c is ModItemCrossStaff || c is ModItemNocturnal || c is ModItemTraverseBoard) return ((ShipItem)c).sold;
            if (c is ModItemRingDial rd) return rd.sold && !rd.IsOpen;
            return false;
        }

        private static bool IsStarMap(Component c)
        {
            foreach (Renderer r in c.GetComponentsInChildren<Renderer>(true))
            {
                Material m = r.sharedMaterial;
                if (m != null && m.name.StartsWith("map item stars")) return true;
            }
            return false;
        }

        /// <summary>The look is held still while studying (InstrumentAimLookPatch).</summary>
        internal static bool Active => item != null && Time.frameCount - markedFrame <= 2;
		/// <summary>This item is being studied now.</summary>
        internal static bool Studying(PickupableItem it) => Active && item == it;

        internal static void Mark(PickupableItem it)
        {
            if (item != it) { pan = panSmooth = Vector2.zero; blend = 0f; }
            item = it;
            markedFrame = Time.frameCount;
        }

        /// <summary>What the mouse would have turned the look by this frame, in degrees.</summary>
        internal static void Look(float yaw, float nod)
        {
            pan.x += yaw * PanPerDegree;
            pan.y += nod * PanPerDegree;
        }

        /// <summary>After the pointer has put the held item in the hand: bring it to the eye.</summary>
        internal static void Place(GoPointer pointer)
        {
            if (item == null) return;
            PickupableItem held = pointer.GetHeldItem();
            bool on = Active && held == item && Studiable(item) && !NavJournal.IsOpen;
            blend = Mathf.MoveTowards(blend, on ? 1f : 0f, Time.deltaTime / BlendTime);
            if (held != item || blend <= 0f)
            {
                if (!on) { item = null; pan = panSmooth = Vector2.zero; }
                return;
            }

            Transform eye = pointer.transform;
            // the page's half-size in the view, to keep its edges in reach
            Vector2 half = new Vector2(0.3f, 0.2f);
            BoxCollider bc = item.GetComponent<BoxCollider>();
            if (bc != null)
            {
                Vector3 s = eye.InverseTransformVector(item.transform.TransformVector(bc.size));
                half = new Vector2(Mathf.Abs(s.x), Mathf.Abs(s.y)) * 0.5f;
            }
            pan.x = Mathf.Clamp(pan.x, -half.x, half.x);
            pan.y = Mathf.Clamp(pan.y, -half.y, half.y);
            float tau = Plugin.configAimSmoothing != null ? Mathf.Max(0.02f, Plugin.configAimSmoothing.Value * 1.5f) : 0.08f;
            panSmooth = Vector2.Lerp(panSmooth, pan, 1f - Mathf.Exp(-Time.deltaTime / tau));

            float zoom = Mathf.Max(1f, Plugin.configStudyZoom.Value / 100f);
            Vector3 study = eye.position + eye.forward * (item.holdDistance / zoom) - eye.right * panSmooth.x - eye.up * panSmooth.y;
            float e = Mathf.SmoothStep(0f, 1f, blend);
            item.transform.position = Vector3.Lerp(item.transform.position, study, e);
        }
    }

    [HarmonyPatch]
    internal static class StudyModePatches
    {
        [HarmonyPatch(typeof(GoPointerButton), nameof(GoPointerButton.OnAltHeld), new System.Type[0])]
        [HarmonyPostfix]
        private static void Held(GoPointerButton __instance)
        {
            if (StudyMode.Shift && __instance is PickupableItem p && StudyMode.Studiable(p)) StudyMode.Mark(p);
        }

        [HarmonyPatch(typeof(GoPointer), "LateUpdate")]
        [HarmonyPostfix]
        private static void Place(GoPointer __instance) => StudyMode.Place(__instance);

        // Shift + right click studies them, instead of folding the map or closing the book
        [HarmonyPatch(typeof(ShipItemFoldable), nameof(ShipItemFoldable.OnAltActivate), new System.Type[0])]
        [HarmonyPrefix]
        private static bool NoFold(ShipItemFoldable __instance) => !(StudyMode.Shift && StudyMode.Studiable(__instance));

        [HarmonyPatch(typeof(ModItemAlmanac), nameof(ModItemAlmanac.OnAltActivate), new System.Type[0])]
        [HarmonyPrefix]
        private static bool NoCloseAlmanac(ModItemAlmanac __instance) => !(StudyMode.Shift && StudyMode.Studiable(__instance));

        [HarmonyPatch(typeof(ModItemCharts), nameof(ModItemCharts.OnAltActivate), new System.Type[0])]
        [HarmonyPrefix]
        private static bool NoCloseAtlas(ModItemCharts __instance) => !(StudyMode.Shift && StudyMode.Studiable(__instance));
    }
}
