using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The game's chronometer (ShipItemClock) as the difficulty has it. Perfect: the base game's.
    /// Imperfect: each one has a number, a rate of its own (minutes gained or lost a day) and a little
    /// random wander; it runs 56 hours on a winding (the cabin boy winds it, unless the config says
    /// you do). Relic (Magellan): it runs a few days and then stops for good.
    /// Its state goes into the save's spare item values: error, number and rate, spring, end of life.
    /// </summary>
    internal class ChronoState : MonoBehaviour
    {
        internal const float MaxRun = 56f;              // game hours on a full winding
        private const float WindPerNotch = 4f;
        private const float RateSigma = 0.5f;           // minutes a day
        private const float RateMax = 1.5f;
        private const float WanderSigma = 0.1f;         // minutes per sqrt(day)
        private const float JumpsPerDay = 0.08f;        // Custom's rate jumps

        internal int serial;
        internal float error;           // hours ahead of the true time
        internal float rate;            // minutes gained a day
        internal float runLeft;         // hours left in the spring
        internal double deathAt;        // Magellan: when it stops for good (0 = not yet decided)
        private double lastTime = -1;
        private bool made;

        internal static ChronoState Of(ShipItemClock clock)
        {
            ChronoState s = clock.GetComponent<ChronoState>();
            if (s == null)
            {
                s = clock.gameObject.AddComponent<ChronoState>();
                s.New();
            }
            return s;
        }

        private static float Gauss()
        {
            float u1 = Mathf.Max(1e-6f, UnityEngine.Random.value), u2 = UnityEngine.Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }

        private void New()
        {
            if (made) return;
            made = true;
            serial = UnityEngine.Random.Range(1000, 10000);
            rate = Mathf.Clamp(Gauss() * RateSigma, -RateMax, RateMax);
            error = Gauss() * 0.5f / 60f;
            runLeft = MaxRun;
            deathAt = 0;
        }

        // ---------------- saving ----------------

        internal void Write(SavePrefabData d)
        {
            d.extraValue0 = error;
            int code = Mathf.Clamp(Mathf.RoundToInt(rate * 100f) + 500, 0, 999);
            d.extraValue1 = serial * 1000 + code;
            d.extraValue2 = runLeft;
            d.extraValue3 = (float)deathAt;
        }

        internal void Read(SavePrefabData d)
        {
            if (d.extraValue1 < 1000f)
            {
                New();
                return;
            }
            made = true;
            int packed = Mathf.RoundToInt(d.extraValue1);
            serial = packed / 1000;
            rate = (packed % 1000 - 500) / 100f;
            error = d.extraValue0;
            runLeft = Mathf.Clamp(d.extraValue2, 0f, MaxRun);
            deathAt = d.extraValue3;
            lastTime = -1;
        }

        // ---------------- running ----------------

        internal bool Dead => Mode.Chronometers == Keeping.Relic && deathAt > 0 && NavMath.NowAbs >= deathAt;
        internal bool Running => Mode.Chronometers == Keeping.Perfect || (!Dead && (runLeft > 0f || Mode.CabinBoyWinds));

        /// <summary>The time on its dial, 0..24.</summary>
        internal double ShownHours
        {
            get
            {
                if (Sun.sun == null) return 0;
                double t = Sun.sun.globalTime + (Mode.Chronometers == Keeping.Perfect ? 0 : error);
                return Celestial.Repeat(t, 24.0);
            }
        }

        internal void Advance(bool sold)
        {
            double now = NavMath.NowAbs;
            double dt = lastTime >= 0 && Sun.sun != null ? now - lastTime : 0;
            if (Sun.sun != null) lastTime = now;
            Keeping k = Mode.Chronometers;
            if (k == Keeping.Relic && deathAt <= 0 && sold) deathAt = now + UnityEngine.Random.Range(2f, 5f) * 24f;
            if (dt <= 0 || !sold || k == Keeping.Perfect) return;      // in a shop it is kept right
            if (dt > 24 * 365) dt = 24 * 365;

            float run;
            if (k == Keeping.Relic)
            {
                run = (float)Math.Max(0.0, Math.Min(dt, deathAt - (now - dt)));
            }
            else
            {
                if (Mode.CabinBoyWinds) runLeft = MaxRun;
                run = Mathf.Min((float)dt, runLeft);
                runLeft -= run;
            }
            if (run > 0f)
            {
                float days = run / 24f;
                error += (rate * days + WanderSigma * Mathf.Sqrt(days) * Gauss()) / 60f;
                if (Mode.RateJumps && UnityEngine.Random.value < 1f - Mathf.Pow(1f - JumpsPerDay, days))
                {
                    rate = Mathf.Clamp(Gauss() * RateSigma * 2f, -RateMax * 2f, RateMax * 2f);
                }
            }
            error -= (float)dt - run;                 // stopped: it falls behind
            error = Mathf.Repeat(error + 12f, 24f) - 12f;
        }

        internal bool Wind(int notches)
        {
            if (notches <= 0 || runLeft >= MaxRun) return false;
            runLeft = Mathf.Min(MaxRun, runLeft + WindPerNotch * notches);
            return true;
        }

        /// <summary>The watchmaker's work: right to the second, wound, and a much smaller rate. Returns the rate he measured.</summary>
        internal float Regulate()
        {
            Advance(true);
            error = 0f;
            rate = Mathf.Clamp(Gauss() * RateSigma * 0.35f, -RateMax, RateMax);
            runLeft = MaxRun;
            if (deathAt > 0) deathAt = 0;
            return rate;
        }

        /// <summary>The sold chronometers within reach of a point.</summary>
        internal static List<ChronoState> Near(Vector3 at, float within)
        {
            var list = new List<ChronoState>();
            foreach (ShipItemClock c in UnityEngine.Object.FindObjectsOfType<ShipItemClock>())
            {
                if (!c.sold || Vector3.Distance(c.transform.position, at) > within) continue;
                list.Add(Of(c));
            }
            list.Sort((a, b) => Vector3.Distance(a.transform.position, at).CompareTo(Vector3.Distance(b.transform.position, at)));
            return list;
        }
    }

    [HarmonyPatch]
    internal static class ChronometerPatches
    {
        private static readonly AccessTools.FieldRef<LookUI, TextMesh> ExtraText = AccessTools.FieldRefAccess<LookUI, TextMesh>("extraText");

        /// <summary>
        /// Its number under it when you look at it or hold it: many are nailed down and never picked
        /// up. Added to the text as it is shown, not written into lookText, so another mod's own
        /// lookText (the alarm clock's "alarm 06:30") stays and shows above it.
        /// </summary>
        private static void AddNumber(LookUI ui, Component target)
        {
            if (Mode.Chronometers == Keeping.Perfect || !(target is ShipItemClock clock) || !clock.sold) return;
            TextMesh t = ExtraText(ui);
            if (t == null) return;
            ChronoState s = ChronoState.Of(clock);
            string number = "No. " + s.serial + (s.Running ? "" : " (stopped)");
            t.text = string.IsNullOrEmpty(t.text) ? number : t.text + "\n" + number;
        }

        [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowLookText))]
        [HarmonyPostfix]
        private static void NumberOnLook(LookUI __instance, GoPointerButton button) => AddNumber(__instance, button);

        [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowHoldText))]
        [HarmonyPostfix]
        private static void NumberOnHold(LookUI __instance, PickupableItem item) => AddNumber(__instance, item);

        /// <summary>The hands, from the chronometer's own time when it isn't the base game's perfect one.</summary>
        [HarmonyPatch(typeof(ShipItemClock), "ExtraLateUpdate")]
        [HarmonyPrefix]
        private static bool Hands(ShipItemClock __instance)
        {
            if (Mode.Chronometers == Keeping.Perfect) return true;
            ChronoState s = ChronoState.Of(__instance);
            s.Advance(__instance.sold);
            double t = s.ShownHours;
            var tr = Traverse.Create(__instance);
            Transform hour = tr.Field("hourHand").GetValue<Transform>();
            Transform minute = tr.Field("minuteHand").GetValue<Transform>();
            // as the game sets them: 30 degrees an hour, 6 a minute, the minute hand a minute at a time
            if (hour != null) hour.localEulerAngles = new Vector3((float)(t * 30.0) + 90f, -90f, 90f);
            if (minute != null) minute.localEulerAngles = new Vector3((float)(Math.Floor(t * 60.0) * 6.0) + 90f, -90f, 90f);
            return false;
        }

        /// <summary>Winding it yourself, when the cabin boy doesn't.</summary>
        [HarmonyPatch(typeof(PickupableItem), "OnScroll")]
        [HarmonyPrefix]
        private static bool Wind(PickupableItem __instance, float input)
        {
            if (!(__instance is ShipItemClock clock) || !clock.sold || clock.held == null || input <= 0f) return true;
            Keeping k = Mode.Chronometers;
            if (k == Keeping.Perfect) return true;
            ChronoState s = ChronoState.Of(clock);
            if (s.Dead)
            {
                NotificationUi.instance.ShowNotification("It has stopped for good: no one aboard can mend it.");
                return false;
            }
            if (k == Keeping.Relic || Mode.CabinBoyWinds) return true;
            s.Advance(true);
            if (s.Wind(1))
            {
                UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.15f, 1.8f);
                if (s.runLeft >= ChronoState.MaxRun) NotificationUi.instance.ShowNotification("The chronometer is fully wound.");
            }
            return false;
        }

        /// <summary>Its number when you pick it up (a label on it would lie over the dial).</summary>
        [HarmonyPatch(typeof(GoPointer), "PickUpItem")]
        [HarmonyPostfix]
        private static void Picked(PickupableItem item)
        {
            if (!(item is ShipItemClock clock) || !clock.sold || Mode.Chronometers == Keeping.Perfect) return;
            ChronoState s = ChronoState.Of(clock);
            NotificationUi.instance.ShowNotification("Chronometer No. " + s.serial + (s.Running ? "" : " (stopped)"));
        }

        /// <summary>None for sale when the difficulty has none.</summary>
        [HarmonyPatch(typeof(ShopItemSpawner), "SpawnItem")]
        [HarmonyPrefix]
        private static bool Sell(ShopItemSpawner __instance)
        {
            GameObject p = __instance.itemPrefab;
            if (p == null) return true;
            if (!Mode.SellChronometers && p.GetComponent<ShipItemClock>() != null) return false;
            if (!Mode.SellPocketWatches && p.GetComponent<ModItemPocketWatch>() != null) return false;
            if (!Mode.SellQuintants && p.GetComponent<ModItemQuintant>() != null) return false;
            if (Mode.Level == Difficulty.Magellan && p.GetComponent<ModItemLoreScroll>() is ModItemLoreScroll book && book.book == "Modern") return false;
            return true;
        }

        [HarmonyPatch(typeof(Shopkeeper), "GetPrice")]
        [HarmonyPostfix]
        private static void Price(ShipItem item, ref int __result)
        {
            if (item != null && item.GetComponent<ShipItemClock>() != null) __result = Mathf.RoundToInt(__result * Mode.ChronometerPrice);
        }

        [HarmonyPatch(typeof(SaveablePrefab), "PrepareSaveData")]
        [HarmonyPostfix]
        private static void Save(SaveablePrefab __instance, SavePrefabData __result)
        {
            if (__result == null) return;
            ChronoState s = __instance.GetComponent<ChronoState>();
            if (s != null) s.Write(__result);
        }

        [HarmonyPatch(typeof(SaveablePrefab), "Load")]
        [HarmonyPostfix]
        private static void Load(SaveablePrefab __instance, SavePrefabData data)
        {
            if (data == null) return;
            ShipItemClock clock = __instance.GetComponent<ShipItemClock>();
            if (clock == null) return;
            ChronoState s = clock.GetComponent<ChronoState>() ?? clock.gameObject.AddComponent<ChronoState>();
            s.Read(data);
        }
    }
}
