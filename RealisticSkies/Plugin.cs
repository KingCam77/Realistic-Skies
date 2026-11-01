using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;


namespace RealisticSkies
{
    [BepInPlugin(Plugin.PLUGIN_ID, Plugin.PLUGIN_NAME, Plugin.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            Plugin.instance = this;
            Plugin.Log = base.Logger;
            //Plugin.logSource = base.Logger;
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), "com.kingcam16.realisticskies");

            configStarType = Config.Bind("General",      // The section under which the option is shown
                                         "Star Type",  // The key of the configuration option in the configuration file
                                         "New", // The default value
                                         "Night sky to load: New, Real, Debug."); // Description of the option to show in the config file

            configDisable = Config.Bind("General",      // The section under which the option is shown
                                         "Disable all sky stuff",  // The key of the configuration option in the configuration file
                                         false, // The default value
                                         "disables all the sky stuff, leaving the default sky. Dont turn this on it would make me sad :("); // Description of the option to show in the config file

            configYearLen = Config.Bind("General",
                                         "Year Length",
                                         92,
                                         "How many days a year takes.");

            configSiderialToggle = Config.Bind("Stars",
                                         "Sideral Rotation",
                                         true,
                                         "Whether the stars rotate over the year.");

            configMoonDimToggle = Config.Bind("Stars",
                                         "Star Dimming",
                                         true,
                                         "Whether the stars dim when the moon is bright.");

            configMoonSizeToggle = Config.Bind("Moon",
                                         "Moon Size",
                                         true,
                                         "Whether the moon is shrunk down to a realistic size.");

            configZodiacToggle = Config.Bind("Stars",
                                         "Zodiacal Light",
                                         true,
                                         "Whether the Zodiacal light is visible.");

            configPlanetsToggle = Config.Bind("Planets",
                                         "Planets",
                                         true,
                                         "Whether the planets are visible.");

            configMoonRework = Config.Bind("Moon",
                                         "Moon Orbit Rework",
                                         true,
                                         "Moon phase follows the game clock (no drift while in shops) and the moon follows an inclined orbit whose nodes slowly regress. Required for the moon tables in the Almanac.");

            configMoonInclination = Config.Bind("Moon",
                                         "Moon Inclination",
                                         12.0f,
                                         "Maximum declination of the moon in degrees (vanilla orbit is tilted about 12 degrees).");

            configMoonNodePeriod = Config.Bind("Moon",
                                         "Moon Node Period",
                                         184.0f,
                                         "Days for the moon's orbit nodes to go once around the sky. Changes which declination full moons happen at over time. 0 = nodes fixed to the stars.");

            configMoonEpoch = Config.Bind("Moon",
                                         "Moon Phase Offset",
                                         0.0f,
                                         "Moon phase at day 0, 00:00 (0 = full moon, 0.5 = new moon). Changing it moves the moon in every save.");

            configAxialTilt = Config.Bind("Sun",
                                         "Axial Tilt",
                                         0.0f,
                                         "Tilt of the planet's axis in degrees (Earth: 23.4). Gives seasons: the sun moves north and south over the year, days get longer and shorter, and the planets' path tilts against the stars. 0 = vanilla sun on the equator.");

            configEquationOfTime = Config.Bind("Sun",
                                         "Equation of Time",
                                         false,
                                         "Place the sun where the elliptical orbit and the axial tilt really put it. Noon then drifts up to about 20 minutes before or after 12:00 over the year; the Almanac sun table includes it.");

            configQuintantClock = Config.Bind("General",
                                         "Quintant Clock",
                                         true,
                                         "Whether the quintant shows the time with each reading. Turn off to make finding the time (and so longitude) part of navigation: use a chronometer or lunar distances.");

            configLunarKey = Config.Bind("General",
                                         "Lunar Mode Key",
                                         new KeyboardShortcut(KeyCode.L),
                                         "While holding the quintant, switches between measuring altitude and measuring lunar distances.");

            configStarLabels = Config.Bind("Training",
                                         "Star Labels",
                                         false,
                                         "Look at a bright star or a planet at night and its name (and the SHA / Dec the Almanac lists for it) appears next to it. For learning the sky.");

            configConstellationLines = Config.Bind("Training",
                                         "Constellation Lines",
                                         true,
                                         "With Star Labels on, looking at a star of a constellation also draws the constellation's figure on the sky, like on the star charts.");

            configConstellationChart = Config.Bind("Stars",
                                         "Constellation Star Chart",
                                         true,
                                         "Use the star chart with the constellation figures (StarChart.png next to the plugin) for the Star Chart item. Takes effect after restarting the game.");

            configPlanetNames = Config.Bind("Planets",
                                         "Planet Names",
                                         true,
                                         "Call the planets by name (Vesta, Lux, Ember, Sister, Leviathan, Pilgrim, Teardrop) instead of Planet 1-7 in the Almanac and star labels.");

            configStarNames = Config.Bind("Stars",
                                         "Star Names",
                                         true,
                                         "Call the Almanac's lettered stars by their sailors' names (A = Willow, W = Corvin, H = Lulu... see LORE.md) in the Almanac, star labels and lunar tables. The Almanac keeps the letter in brackets.");

            configQuintantHelp = Config.Bind("General",      // The section under which the option is shown
                                         "Quintant Help",  // The key of the configuration option in the configuration file
                                         "Arc", // The default value
                                         "How much of the readings from the quintant should be displayed as text: None, Arc, Sec, Full."); // Description of the option to show in the config file

        }
        
        public const string PLUGIN_ID = "com.kingcam16.realisticskies";

        public const string PLUGIN_NAME = "RealisticSkies";

        public const string PLUGIN_VERSION = "2.3.0";   // released as 2.3.0F (fork); BepInEx needs a plain version number here

        internal static ConfigEntry<string> configStarType;

        internal static ConfigEntry<string> configQuintantHelp;

        internal static ConfigEntry<int> configYearLen;

        internal static ConfigEntry<bool> configSiderialToggle;

        internal static ConfigEntry<bool> configMoonDimToggle;

        internal static ConfigEntry<bool> configMoonSizeToggle;

        internal static ConfigEntry<bool> configZodiacToggle;

        internal static ConfigEntry<bool> configPlanetsToggle;

        internal static ConfigEntry<bool> configDisable;

        internal static ConfigEntry<bool> configMoonRework;

        internal static ConfigEntry<float> configMoonInclination;

        internal static ConfigEntry<float> configMoonNodePeriod;

        internal static ConfigEntry<float> configMoonEpoch;

        internal static ConfigEntry<float> configAxialTilt;

        internal static ConfigEntry<bool> configEquationOfTime;

        internal static ConfigEntry<bool> configQuintantClock;

        internal static ConfigEntry<KeyboardShortcut> configLunarKey;

        internal static ConfigEntry<bool> configStarLabels;

        internal static ConfigEntry<bool> configConstellationLines;

        internal static ConfigEntry<bool> configConstellationChart;

        internal static ConfigEntry<bool> configPlanetNames;

        internal static ConfigEntry<bool> configStarNames;

        internal static Plugin instance;

        internal static ManualLogSource Log;
    }
}