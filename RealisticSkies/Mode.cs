namespace RealisticSkies
{
    internal enum Difficulty { Magellan, Chronometers, Vanilla, Custom }

    /// <summary>How a timekeeper behaves.</summary>
    internal enum Keeping
    {
        Perfect,        // as in the base game: always right, never wound
        Imperfect,      // a rate of its own and a little wander; must be wound
        Dead,           // pocket watches in Magellan's time: stopped for good
        Relic,          // chronometers in Magellan's time: run a few days, then stop for good
    }

    /// <summary>
    /// The difficulty, and what it sets. Magellan: no chronometers or pocket watches, sand glasses only;
    /// a lunar distance sets the ship's glass, and the journal takes the time of each sight from it. Chronometers: chronometers and pocket watches gain
    /// or lose; chronometers cost twice as much and only a watchmaker can regulate them. Vanilla: perfect
    /// clocks, pocket watches need no winding, the sun as in the base game. The sun's tilt and the
    /// equation of time are on in the first two. Custom: the separate options below decide.
    /// </summary>
    internal static class Mode
    {
        internal const float EarthTilt = 23.44f;

        /// <summary>The difficulty of the game being played: kept in its save (ModeInSave), the setting before a game begins.</summary>
        internal static Difficulty Level => ModeInSave.Level;

        internal static float AxialTilt =>
            Level == Difficulty.Vanilla ? 0f : Level == Difficulty.Custom ? Plugin.configAxialTilt.Value : EarthTilt;

        internal static bool EquationOfTime =>
            Level == Difficulty.Vanilla ? false : Level == Difficulty.Custom ? Plugin.configEquationOfTime.Value : true;

        internal static bool QuintantClock => Level == Difficulty.Custom && Plugin.configQuintantClock.Value;

        internal static Keeping Chronometers
        {
            get
            {
                switch (Level)
                {
                    case Difficulty.Magellan: return Keeping.Relic;
                    case Difficulty.Chronometers: return Keeping.Imperfect;
                    case Difficulty.Vanilla: return Keeping.Perfect;
                    default: return Plugin.configChronometers.Value;
                }
            }
        }

        internal static Keeping PocketWatches
        {
            get
            {
                switch (Level)
                {
                    case Difficulty.Magellan: return Keeping.Dead;
                    case Difficulty.Chronometers: return Keeping.Imperfect;
                    case Difficulty.Vanilla: return Keeping.Perfect;
                    default:
                        Keeping k = Plugin.configPocketWatches.Value;
                        return k == Keeping.Relic ? Keeping.Dead : k;
                }
            }
        }

        /// <summary>Chronometers that now and then change their rate (Custom only).</summary>
        internal static bool RateJumps => Level == Difficulty.Custom && Plugin.configRateJumps.Value;

        /// <summary>The journal takes the time of each sight from the ship's glass, once a lunar has set it (no watch to give it).</summary>
        internal static bool GlassTimeInJournal =>
            Level == Difficulty.Magellan || (Level == Difficulty.Custom && Plugin.configJournalGlassTime.Value);

        internal static bool CabinBoyWinds => Plugin.configCabinBoy.Value;

        /// <summary>Chronometers and pocket watches in the shops.</summary>
        internal static bool SellChronometers => Chronometers != Keeping.Relic;
        internal static bool SellPocketWatches => PocketWatches != Keeping.Dead;

        /// <summary>No quintants (Hadley's octant, 1731) in Magellan's time: the cross-staff and the quadrant.</summary>
        internal static bool SellQuintants => Level != Difficulty.Magellan;

        internal static float ChronometerPrice => Chronometers == Keeping.Imperfect ? 2f : 1f;
    }
}
