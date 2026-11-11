using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The journal's "ship's time": the time at the prime meridian as the ship keeps it, by the pocket
    /// watch (or, where the journal keeps time by the glass, the watch glass).
    ///
    /// The head of the journal shows it in one line, with what keeps it and when it was last set, in
    /// place of the old "watch error" field. After a lunar distance one button sets it: the watch is
    /// put right (or, with no watch, the glass is set), and the journal says in words what it found.
    /// Keeping an error by hand instead of setting the watch is still there, behind "by hand".
    /// </summary>
    internal static partial class NavJournal
    {
        private static double timeSetAt = double.NaN;       // when the ship's time was last set (absolute true hours), saved
        private static bool byHand;                         // the error field and the old buttons are shown
        private static string lunarSaid = "";               // what the last setting found, shown under the button
        private static double lunarSaidFor = double.NaN;

        // the keepers are looked for twice a second, not on every pass of the page
        private static float keepersAt = -10f;
        private static ModItemPocketWatch keptWatch;
        private static ModItemWatchGlass keptGlass;

        private static void Keepers()
        {
            if (Time.unscaledTime - keepersAt < 0.5f) return;
            keepersAt = Time.unscaledTime;
            keptWatch = Watch();
            keptGlass = KeptGlass();
        }

        private static string SetAge()
        {
            if (double.IsNaN(timeSetAt)) return "";
            double age = Math.Max(0.0, (NavMath.NowAbs - timeSetAt) / 24.0);
            int days = (int)Math.Floor(age);
            string s = days <= 0 ? ", set today" : days == 1 ? ", set yesterday" : ", set " + days + " days ago";

            // what the age costs: a keeper wanders, and every 4 minutes of time is a degree of longitude.
            // A pocket watch is out by about 1.2 minutes for each day; a watch glass walks at random,
            // about 5 minutes over a day and 8 or 9 over three. Perfect clocks cost nothing.
            double minutes;
            if (GlassTime) minutes = 5.0 * Math.Sqrt(age);
            else if (Mode.PocketWatches == Keeping.Imperfect) minutes = 1.2 * age;
            else return s;
            double degrees = minutes / 4.0;
            if (degrees < 0.15) return s;
            return s + ": longitude may be " + degrees.ToString(degrees < 1.0 ? "0.0" : "0", System.Globalization.CultureInfo.InvariantCulture) + "° out";
        }

        /// <summary>The head of the journal, right of the latitude: the ship's time in one line.</summary>
        private static void DrawShipTime()
        {
            Keepers();
            double err = useErr ? NavMath.ParseTime(errText) : 0.0;
            string text = null, missing = null;
            if (GlassTime)
            {
                if (keptGlass == null) missing = "Ship's time: not set. Take a lunar distance.";
                else text = NavMath.Clock(Celestial.Repeat(keptGlass.ReckonedTime - (double.IsNaN(err) ? 0.0 : err), 24.0)) + "</b>  by the watch glass";
            }
            else if (keptWatch == null || Mode.PocketWatches == Keeping.Dead) missing = "Ship's time: no pocket watch with you.";
            else if (Mode.PocketWatches == Keeping.Imperfect && !keptWatch.Running) missing = "Ship's time: your watch has stopped. Wind it.";
            else if (double.IsNaN(err) && Mode.PocketWatches != Keeping.Perfect) missing = "Ship's time: not set. Take a lunar distance.";
            else text = NavMath.Clock(Celestial.Repeat(keptWatch.ShownHours - (double.IsNaN(err) ? 0.0 : err), 24.0)) + "</b>  by your watch";

            if (missing != null) GUILayout.Label(missing, sWarn, GUILayout.ExpandWidth(true));
            else GUILayout.Label("Ship's time:  <b>" + text + SetAge(), sText, GUILayout.ExpandWidth(true));
            if (GUILayout.Button(byHand ? "by hand  [x]" : "by hand", sButton, GUILayout.Width(130))) byHand = !byHand;
        }

        /// <summary>Under the head, when "by hand" is on: the error kept on paper instead of setting the watch.</summary>
        private static void DrawErrorByHand()
        {
            if (!byHand) return;
            GUILayout.BeginHorizontal();
            GUILayout.Label(GlassTime ? "Glass error" : "Watch error", sText, GUILayout.Width(170));
            errText = GUILayout.TextField(errText, 12, sField, GUILayout.Width(140));
            GUILayout.Label("  + fast, - slow: taken off every time read from the " + (GlassTime ? "glass" : "watch"), sSmall);
            GUILayout.EndHorizontal();
        }

        private static string FastSlow(double err) => (err >= 0 ? "fast" : "slow") + " by " + NavMath.Span(Math.Abs(err)).Substring(1);

        private static void SetGlasses(List<ModItemWatchGlass> glasses, double gmt)
        {
            double taken = double.IsNaN(lunarAt) ? NavMath.NowAbs : lunarAt;
            double offset = gmt - taken;
            foreach (ModItemWatchGlass g in glasses) g.SetBy(offset);
            double next = Math.Ceiling(NavMath.NowAbs + offset + 1e-6);
            AddNote("Day " + (int)Math.Floor(gmt / 24.0) + ", " + NavMath.Clock(gmt) + " at the prime meridian by a lunar (" + Lunar.Name(lunarBody) +
                    "): the ship's glass set, to be turned at " + NavMath.Clock(next).Substring(0, 5));
            lunarSaid = "The ship's glass is set. It keeps the time from now, and will be turned at " + NavMath.Clock(next).Substring(0, 5) + ".";
        }

        /// <summary>
        /// The foot of the Lunar distance page, once the time at the prime meridian is found: what
        /// the keeper was out by, and the one button that sets the ship's time.
        /// </summary>
        private static void DrawLunarSet(double gmt, double watch, double dist)
        {
            double err = double.IsNaN(watch) ? double.NaN : Celestial.Repeat(watch - gmt + 12.0, 24.0) - 12.0;
            if (!double.IsNaN(err))
                GUILayout.Label((GlassTime ? "The ship's glass is " : "Your watch is ") + "<b>" + FastSlow(err) + "</b>", sResult);
            GUILayout.Space(8);

            Camera cam = Camera.main;
            List<ModItemWatchGlass> glasses = cam != null ? ModItemWatchGlass.Near(cam.transform.position, GlassReach) : new List<ModItemWatchGlass>();
            ModItemPocketWatch w = GlassTime ? null : Watch();
            bool setWatch = w != null && Mode.PocketWatches == Keeping.Imperfect && !double.IsNaN(err);
            bool setGlass = !setWatch && glasses.Count > 0 && (GlassTime || w == null);

            if (setWatch || setGlass)
            {
                if (GUILayout.Button("Set the ship's time by this lunar", sButton, GUILayout.Width(420), GUILayout.Height(40)))
                {
                    if (setWatch)
                    {
                        // the hands put back by the error found: from now on the watch tells the prime meridian's time
                        w.Correct(err);
                        lunarWatch = NavMath.Clock(Celestial.Repeat(watch - err, 24.0));
                        AddNote("Day " + (int)Math.Floor(gmt / 24.0) + ": watch set right by a lunar (" + Lunar.Name(lunarBody) + "; it was " + FastSlow(err) + ")");
                        lunarSaid = "Your watch was " + FastSlow(err) + ". It shows the time at the prime meridian now.";
                        if (!w.Running) lunarSaid += " But it has stopped: wind it (scroll while holding it), or it will not keep that time.";
                        UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.15f, 2.2f);
                    }
                    else
                    {
                        SetGlasses(glasses, gmt);
                        UISoundPlayer.instance.PlayParchmentSound();
                    }
                    errText = "+00:00:00";
                    timeSetAt = NavMath.NowAbs;
                    lunarSaidFor = gmt;
                    keepersAt = -10f;
                    Save();
                    Notify(lunarSaid);
                }
            }
            else if (!GlassTime && w != null && Mode.PocketWatches == Keeping.Perfect)
            {
                GUILayout.Label("Your watch keeps perfect time: there is nothing to set.", sSmall);
            }
            else
            {
                GUILayout.Label(GlassTime ? "No watch glass aboard to keep the time." : "Nothing to keep the time: carry a pocket watch, or have a watch glass aboard.", sWarn);
            }
            if (lunarSaid.Length > 0 && lunarSaidFor == gmt) GUILayout.Label(lunarSaid, sSmall);

            if (!byHand) return;
            // the old way: keep the watch as it is and carry its error on paper; and the glass beside a watch
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUI.enabled = !double.IsNaN(err);
            if (GUILayout.Button(GlassTime ? "Take this as the glass error" : "Take this as the watch error", sButton, GUILayout.Width(360)))
            {
                errText = (err < 0 ? "-" : "+") + NavMath.Clock(Math.Abs(err));
                timeSetAt = NavMath.NowAbs;
                AddNote("Day " + (int)Math.Floor(gmt / 24.0) + ", " + NavMath.Clock(gmt) + " at the prime meridian: lunar, " + Lunar.Name(lunarBody) + " " +
                        NavMath.Angle(dist) + (GlassTime ? ", glass " : ", watch ") + FastSlow(err));
                Save();
                UISoundPlayer.instance.PlayParchmentSound();
            }
            GUI.enabled = true;
            if (!GlassTime) SetGlassButton(gmt, false);
            GUILayout.EndHorizontal();
        }
    }
}
