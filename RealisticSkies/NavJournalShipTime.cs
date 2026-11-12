using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The journal's "ship's time": the time at the prime meridian as the ship keeps it. It is not a
    /// clock of its own: it is what a keeper shows, the pocket watch (or, where the journal keeps time
    /// by the glass, the watch glass), and it is as wrong as that keeper is.
    ///
    /// A lunar distance gives the time without any watch: the day is known, the table gives the hour.
    /// One button then sets the ship's time by it: the watch is put right, or with no watch the glass
    /// is set. With no keeper at all the journal itself holds the reckoning, but only for
    /// <see cref="HoldHours"/> from the sight (a navigator counting the minutes): time enough to
    /// fetch a watch, wind it and press the button again. After that the sight is stale and another
    /// must be taken. The same limit keeps an old sight from setting a keeper that did not time it.
    ///
    /// The head of the journal shows the ship's time in one line, with what keeps it and when it was
    /// last set. Keeping an error by hand instead of setting the watch is behind "by hand".
    /// </summary>
    internal static partial class NavJournal
    {
        private const double HoldHours = 2.0;

        private static double timeSetAt = double.NaN;       // when the ship's time was last set (absolute true hours), saved
        private static double heldAt = double.NaN;          // the sight the journal holds a reckoning from (absolute true hours), saved
        private static double heldOffset;                   // prime meridian time less true time, by that sight, saved
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

        /// <summary>The journal still holds a reckoning from a fresh lunar (no keeper has taken it over yet).</summary>
        private static bool HeldValid
        {
            get
            {
                if (double.IsNaN(heldAt)) return false;
                double age = NavMath.NowAbs - heldAt;
                return age >= 0.0 && age <= HoldHours;
            }
        }

        /// <summary>The time at the prime meridian by the journal's held reckoning (hours of the day).</summary>
        private static double HeldTime => Celestial.Repeat(NavMath.NowAbs + heldOffset, 24.0);

        private static string HeldLeft()
        {
            int m = Mathf.Max(1, Mathf.CeilToInt((float)((HoldHours - (NavMath.NowAbs - heldAt)) * 60.0)));
            return m >= 60 ? (m / 60) + " h " + (m % 60) + " min" : m + " min";
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
            if (HeldValid)
            {
                // a fresh lunar, and no keeper set by it yet: the journal's own reckoning, while it lasts
                bool watch = !GlassTime && keptWatch != null && Mode.PocketWatches != Keeping.Dead;
                text = NavMath.Clock(HeldTime) + "</b>  by your reckoning from the lunar, for " + HeldLeft() + " more. " +
                       (watch ? "Set your watch by it." : GlassTime ? "No watch glass aboard keeps it." : "No pocket watch with you.");
            }
            else if (GlassTime)
            {
                if (keptGlass == null) missing = "Ship's time: not set. Take a lunar distance.";
                else text = NavMath.Clock(Celestial.Repeat(keptGlass.ReckonedTime - (double.IsNaN(err) ? 0.0 : err), 24.0)) + "</b>  by the watch glass" + SetAge();
            }
            else if (keptWatch == null || Mode.PocketWatches == Keeping.Dead) missing = "Ship's time: no pocket watch with you.";
            else if (Mode.PocketWatches == Keeping.Imperfect && !keptWatch.Running) missing = "Ship's time: your watch has stopped. Wind it.";
            else if (double.IsNaN(err) && Mode.PocketWatches != Keeping.Perfect) missing = "Ship's time: not set. Take a lunar distance.";
            else text = NavMath.Clock(Celestial.Repeat(keptWatch.ShownHours - (double.IsNaN(err) ? 0.0 : err), 24.0)) + "</b>  by your watch" + SetAge();

            if (missing != null) GUILayout.Label(missing, sWarn, GUILayout.ExpandWidth(true));
            else GUILayout.Label("Ship's time:  <b>" + text, sText, GUILayout.ExpandWidth(true));
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

        private static double Wrap12(double h) => Celestial.Repeat(h + 12.0, 24.0) - 12.0;

        /// <summary>
        /// The time at the prime meridian now, as hours of the day, for a keeper that had no part in the
        /// sight: only while the sight is fresh (the journal's held reckoning). NaN otherwise.
        /// </summary>
        private static double HeldGmt() => HeldValid ? HeldTime : double.NaN;

        /// <summary>
        /// The foot of the Lunar distance page, once the time at the prime meridian is found: what
        /// the keeper was out by, and the one button that sets the ship's time.
        /// </summary>
        private static double lunarKeptFor = double.NaN;       // the mean a watch or glass was last set by

        private static void DrawLunarSet(double gmt, double watch, double dist, double taken)
        {
            // how the time found is carried from the sight to this moment: by counting, while the sight
            // is fresh; or by the keeper that timed the sight (its reading then, in the field above).
            // With a set of sights these are the mean's: the time at the last sight, the keeper's mean error.
            double now = NavMath.NowAbs;
            bool fresh = !double.IsNaN(taken) && now >= taken && now - taken <= HoldHours;
            double err = double.IsNaN(watch) ? double.NaN : Wrap12(watch - gmt);        // the keeper's error at the sight
            if (!double.IsNaN(err))
                GUILayout.Label((GlassTime ? "The ship's glass was " : "Your watch was ") + "<b>" + FastSlow(err) + "</b>", sResult);
            GUILayout.Space(8);

            Camera cam = Camera.main;
            List<ModItemWatchGlass> glasses = cam != null ? ModItemWatchGlass.Near(cam.transform.position, GlassReach) : new List<ModItemWatchGlass>();
            ModItemPocketWatch w = GlassTime ? null : Watch();
            bool perfect = !GlassTime && w != null && Mode.PocketWatches == Keeping.Perfect;
            bool setWatch = w != null && Mode.PocketWatches == Keeping.Imperfect;
            bool setGlass = !setWatch && !perfect && glasses.Count > 0;

            if (!fresh && double.IsNaN(err))
            {
                GUILayout.Label("This sight is more than two hours old, and no " + (GlassTime ? "glass" : "watch") +
                                " timed it: the time it gives cannot be carried to now. Take another.", sWarn);
            }
            else if (perfect)
            {
                GUILayout.Label("Your watch keeps perfect time: there is nothing to set.", sSmall);
            }
            else if (lunarKeptFor == gmt)
            {
                // the keeper is already set by these very sights: nothing to offer until there is a new one
                GUILayout.Label("The ship's time is set by these sights. A new sight gives a new mean to set it by.", sSmall);
            }
            else if (GUILayout.Button("Set the ship's time by this lunar", sButton, GUILayout.Width(420), GUILayout.Height(40)))
            {
                if (setWatch || setGlass) lunarKeptFor = gmt;
                double offset = gmt - taken;                // prime meridian time less true time, by this sight
                if (setWatch)
                {
                    // the hands put right: from now on the watch tells the prime meridian's time
                    double wrong = fresh ? Wrap12(w.ShownHours - Celestial.Repeat(now + offset, 24.0)) : err;
                    w.Correct(wrong);
                    if (!double.IsNaN(wrong)) LunarWatchesCorrected(wrong);
                    AddNote(DayName((int)Math.Floor(gmt / 24.0)) + ": watch set right by a lunar (" + Lunar.Name(lunarBody) + "; it was " + FastSlow(wrong) + ")");
                    lunarSaid = "Your watch was " + FastSlow(wrong) + ". It shows the time at the prime meridian now.";
                    if (!w.Running) lunarSaid += " But it has stopped: wind it (scroll while holding it), or it will not keep that time.";
                    heldAt = double.NaN;
                    timeSetAt = now;
                    UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.15f, 2.2f);
                }
                else if (setGlass)
                {
                    // fresh: by the count from the sight; else by the glass's own reading, less the error found
                    if (!fresh && keptGlass != null) offset = (keptGlass.ReckonedTime - err) - now;
                    foreach (ModItemWatchGlass g in glasses) g.SetBy(offset);
                    double next = Math.Ceiling(now + offset + 1e-6);
                    AddNote(DayName((int)Math.Floor(gmt / 24.0)) + ", " + NavMath.Clock(gmt) + " at the prime meridian by a lunar (" + Lunar.Name(lunarBody) +
                            "): the ship's glass set, to be turned at " + NavMath.Clock(next).Substring(0, 5));
                    lunarSaid = "The ship's glass is set. It keeps the time from now, and will be turned at " + NavMath.Clock(next).Substring(0, 5) + ".";
                    heldAt = double.NaN;
                    timeSetAt = now;
                    UISoundPlayer.instance.PlayParchmentSound();
                }
                else
                {
                    // nothing aboard to keep it: the journal holds the reckoning while the sight is fresh
                    heldAt = taken;
                    heldOffset = offset;
                    lunarSaid = GlassTime
                        ? "No watch glass aboard to keep the time. The journal keeps it for two hours from the sight: bring a glass aboard and press this button again."
                        : "No pocket watch with you. The journal keeps the time for two hours from the sight: take your watch, wind it and press this button again.";
                    UISoundPlayer.instance.PlayParchmentSound();
                }
                errText = "+00:00:00";
                lunarSaidFor = gmt;
                keepersAt = -10f;
                Save();
                Notify(lunarSaid);
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
                AddNote(DayName((int)Math.Floor(gmt / 24.0)) + ", " + NavMath.Clock(gmt) + " at the prime meridian: lunar, " + Lunar.Name(lunarBody) + " " +
                        NavMath.Angle(dist) + (GlassTime ? ", glass " : ", watch ") + FastSlow(err));
                Save();
                UISoundPlayer.instance.PlayParchmentSound();
            }
            GUI.enabled = true;
            if (!GlassTime && fresh) SetGlassButton(gmt, false, taken);
            GUILayout.EndHorizontal();
        }
    }
}
