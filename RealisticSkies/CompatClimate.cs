using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    internal enum StartSeason { Autumn, Winter, Spring, Summer }

    /// <summary>
    /// The calendar both mods share. Day 0 of our year is the autumn equinox. A new game may start
    /// later in the year (only with the Climate mod, by its config); the day it started on is kept in
    /// the save, so the journal's "day of the voyage" still counts from the start.
    /// </summary>
    internal static class Calendar
    {
        internal const string StartKey = "RealisticSkies.StartDay";

        /// <summary>The game day the voyage began on (0 for saves made before this was kept).</summary>
        internal static int StartDay
        {
            get
            {
                if (GameState.modData != null && GameState.modData.TryGetValue(StartKey, out string s) && int.TryParse(s, out int d)) return d;
                return 0;
            }
        }

        internal static int VoyageDay => GameState.day - StartDay;

        /// <summary>
        /// The game's day was set by the mod (a new game in a later season, the Lost at Sea start). The
        /// log book's first day sheet and the Climate mod's date line were made when the game started
        /// up, on day 0, and nothing tells them: they are put right here.
        /// </summary>
        internal static void DayWasSet()
        {
            try
            {
                if (DayLogs.instance != null && DayLogs.instance.dayLogs != null)
                {
                    foreach (DayLog log in DayLogs.instance.dayLogs)
                    {
                        if (log != null && log.daySheets != null && log.daySheets.Length > 0 && log.daySheets[0] != null) log.daySheets[0].day = GameState.day;
                    }
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("RealisticSkies: log book day: " + e.Message);
            }
            CompatClimate.RefreshDate();
        }

        /// <summary>First day of a season in our calendar.</summary>
        internal static int SeasonStart(StartSeason s)
        {
            if (ModItemNocturnal.YearLen == 92) return 23 * (int)s;
            switch (s)                                  // 365: from 22 September
            {
                case StartSeason.Winter: return 90;     // 21 December
                case StartSeason.Spring: return 179;    // 20 March
                case StartSeason.Summer: return 272;    // 21 June
                default: return 0;
            }
        }

        /// <summary>Season, day in it (from 1) and its length, for a day of the year.</summary>
        internal static void Season(int doy, out StartSeason season, out int day, out int length)
        {
            int len = ModItemNocturnal.YearLen;
            season = StartSeason.Summer;
            for (int i = 3; i >= 0; i--)
            {
                if (doy >= SeasonStart((StartSeason)i)) { season = (StartSeason)i; break; }
            }
            int start = SeasonStart(season);
            int end = (int)season < 3 ? SeasonStart(season + 1) : len;
            day = doy - start + 1;
            length = end - start;
        }
    }

    /// <summary>
    /// Compatibility with Climate (bryon82, com.raddude.climate).
    ///
    /// Climate counts its year from its own day 0 = the start of winter, and writes that season on the
    /// logbook. This mod's sky counts from the autumn equinox, so the two disagreed on the season.
    /// With Climate installed:
    ///  - Climate's seasonal weather (temperatures, dew points, the big pressure systems that drive the
    ///    trade winds) is moved to follow our sun: its warmest day falls on our summer solstice;
    ///  - its date line on the logbook names our date;
    ///  - its year length follows ours;
    ///  - a new game starts in the season chosen in our config (Autumn by default). A game already
    ///    begun keeps its date: GameState.day is not touched, so it reads as our calendar.
    /// </summary>
    [HarmonyPatch]
    internal static class CompatClimate
    {
        internal const string Guid = "com.raddude.climate";
        internal static bool Loaded { get; private set; }

        internal static ConfigEntry<StartSeason> configStartSeason;

        private static ConfigEntry<int> climateYearLen;
        private static FieldInfo dateTextMesh;

        /// <summary>
        /// Days to add to our day of the year to get Climate's. Climate's seasonal curves peak (warmest,
        /// summer pressure systems) on its day 43 of 92 / 172 of 365; our summer solstice falls on day
        /// 68.3 / 270.9 (elliptical orbit, see Celestial.SunLongitudeTrue).
        /// </summary>
        private static int Shift => ModItemNocturnal.YearLen == 92 ? 67 : 266;

        internal static void Init(Harmony harmony, ConfigFile config)
        {
            configStartSeason = config.Bind("Compatibility", "Start Season (Climate mod)", StartSeason.Autumn,
                "Only with the Climate mod installed. The season a new game begins in. Autumn (the autumn equinox) is this mod's own start; " +
                "Winter is where Climate's own calendar begins. A game already begun keeps its date, counted from the autumn equinox by the days gone by. " +
                "Either way Climate's seasons, winds and logbook date follow this mod's sun and calendar.");

            Loaded = Chainloader.PluginInfos.ContainsKey(Guid);
            if (!Loaded) return;

            try
            {
                Type zones = AccessTools.TypeByName("Climate.ClimateZones");
                Type systems = AccessTools.TypeByName("Climate.PressureSystem");
                Type dateUi = AccessTools.TypeByName("Climate.DateTextUI");
                Type configs = AccessTools.TypeByName("Climate.Configs");

                var shift = new HarmonyMethod(AccessTools.Method(typeof(CompatClimate), nameof(ShiftDay)));
                MethodInfo seasonal = zones != null ? AccessTools.Method(zones, "GetSeasonalFactor") : null;
                MethodInfo state = systems != null ? AccessTools.Method(systems, "ComputeState") : null;
                MethodInfo update = dateUi != null ? AccessTools.Method(dateUi, "UpdateDateText") : null;
                if (seasonal != null) harmony.Patch(seasonal, prefix: shift);
                if (state != null) harmony.Patch(state, prefix: shift);
                if (update != null)
                {
                    dateTextMesh = AccessTools.Field(dateUi, "textMesh");
                    harmony.Patch(update, prefix: new HarmonyMethod(AccessTools.Method(typeof(CompatClimate), nameof(DateText))));
                }

                climateYearLen = configs != null ? AccessTools.Field(configs, "yearLength")?.GetValue(null) as ConfigEntry<int> : null;
                SyncYearLength();
                Plugin.configYearLen.SettingChanged += (s, e) => SyncYearLength();
                if (climateYearLen != null) climateYearLen.SettingChanged += (s, e) => SyncYearLength();

                Plugin.Log.LogInfo("Climate found: its seasons and logbook date follow this mod's calendar" +
                    (seasonal == null || state == null || update == null ? " (some of its parts were not found: Climate changed?)" : "") + ".");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Climate compatibility failed: " + e);
            }
        }

        private static void SyncYearLength()
        {
            if (climateYearLen == null) return;
            int ours = ModItemNocturnal.YearLen;
            if (climateYearLen.Value != ours)
            {
                climateYearLen.Value = ours;
                Plugin.Log.LogInfo("Climate's \"Days In A Year\" set to " + ours + " to match this mod's year.");
            }
        }

        /// <summary>Prefix for Climate's seasonal functions: our day, read on Climate's calendar.</summary>
        private static void ShiftDay(ref int day)
        {
            int len = ModItemNocturnal.YearLen;
            day = ModItemNocturnal.DayOfYear(day) + Shift;
            if (day >= len) day -= len;
        }

        /// <summary>Climate's date line on the logbook, in our calendar (same layout as Climate's).</summary>
        private static bool DateText()
        {
            TextMesh tm = dateTextMesh?.GetValue(null) as TextMesh;
            if (tm == null) return false;
            int len = ModItemNocturnal.YearLen;
            int doy = ModItemNocturnal.DayOfYear(GameState.day);
            Calendar.Season(doy, out StartSeason season, out int sd, out int slen);
            string date = len == 92 ? season.ToString() : ModItemNocturnal.DateName(doy) + ", " + season;
            tm.text = "Year: " + (GameState.day / len) + "  Day: " + (doy + 1) + "    " + date + " (" + sd + "/" + slen + ")";
            return false;
        }

        /// <summary>A new game: remember the day it began on; with Climate, begin in the chosen season.</summary>
        [HarmonyPatch(typeof(StartMenu), "StartNewGame")]
        [HarmonyPostfix]
        private static void NewGame()
        {
            if (Loaded && configStartSeason != null)
            {
                GameState.day = Calendar.SeasonStart(configStartSeason.Value);
                if (GameState.day != 0) Plugin.Log.LogInfo("New game begins on " + ModItemNocturnal.DateName(GameState.day) + " (Start Season: " + configStartSeason.Value + ").");
            }
            if (GameState.modData != null) GameState.modData[Calendar.StartKey] = GameState.day.ToString();
            Calendar.DayWasSet();
        }

        /// <summary>Climate's date line on the log book, written again (it is otherwise written only at midnight and on loading).</summary>
        internal static void RefreshDate()
        {
            if (!Loaded) return;
            try { DateText(); }
            catch (Exception e) { Plugin.Log.LogWarning("Climate date line: " + e.Message); }
        }
    }
}
