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
    [BepInDependency(CompatClimate.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(CompatRadRefinements.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            Plugin.instance = this;
            Plugin.Log = base.Logger;
            //Plugin.logSource = base.Logger;
            Harmony harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), "com.kingcam16.realisticskies");

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
                                         new ConfigDescription("How many days a year takes: 92 (the islands' year, four seasons of 23 days) or 365 (the Earth's). The nocturnal's date ring is made for one or the other.",
                                                               new AcceptableValueList<int>(92, 365)));

            configAimSensitivity = Config.Bind("Instruments", "Aim Sensitivity", 0.5f,
                new ConfigDescription("How fast the look turns while an instrument is up to the eye, as a part of the normal speed at a 60 degree view (it slows further as the instrument's view narrows).", new AcceptableValueRange<float>(0.1f, 1f)));
            configAimSmoothing = Config.Bind("Instruments", "Aim Smoothing", 0.06f,
                new ConfigDescription("Seconds over which the mouse's movement is smoothed while aiming an instrument. 0 = none.", new AcceptableValueRange<float>(0f, 0.3f)));
            configStudyZoom = Config.Bind("Instruments", "Study Zoom", 250f,
                new ConfigDescription("How close Shift + right click brings the Star Map, the Almanac or the Celestial Atlas to the eye, in percent of the usual size.", new AcceptableValueRange<float>(150f, 400f)));

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
										 
			configMoonHalo = Config.Bind("Moon",
										 "Moon Halo",
										 1f,
										 new ConfigDescription("How bright the glow around the moon is (with Moon Size on): 1 = as made, 0.5 = half, 0 = none.",
											 new AcceptableValueRange<float>(0f, 1f)));

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

            configDifficulty = Config.Bind("Difficulty",
                                         "Difficulty",
                                         Difficulty.Chronometers,
                                         "The difficulty a new game begins with; each saved game keeps its own. Changed while you are playing, it changes the game you are in. " +
                                         "Magellan: no chronometers or pocket watches in the shops, only sand glasses; an old chronometer you already have runs a few days and stops for good, a pocket watch stops at once; a lunar distance sets the ship's watch glass to your reckoning and the Navigator's Journal takes the time of each sight from it. " +
                                         "Chronometers: chronometers and pocket watches gain or lose a little each day; chronometers cost twice as much and only the watchmaker in the capitals can regulate them; pocket watches must be wound. " +
                                         "Vanilla: clocks and pocket watches are always right and need no winding; the sun as in the base game. " +
                                         "Magellan and Chronometers tilt the planet's axis as the Earth's and put the sun on its elliptical orbit. Custom: the options of the Custom section decide.");

            // the difficulty lives in the save: the setting is what a new game begins with, and changing it mid-game changes that game
            configDifficulty.SettingChanged += (s, e) => ModeInSave.SettingChanged();

            configCabinBoy = Config.Bind("Difficulty",
                                         "Cabin Boy Winds Chronometers",
                                         true,
                                         "Chronometers that gain or lose are wound for you. Off: wind each one yourself with the scroll wheel while you hold it; one runs about two and a half days on a winding, and once it stops its error is anyone's guess.");

            configAxialTilt = Config.Bind("Custom",
                                         "Axial Tilt",
                                         Mode.EarthTilt,
                                         "Custom difficulty only. Tilt of the planet's axis in degrees (Earth: 23.44). Gives seasons: the sun moves north and south over the year, days get longer and shorter, and the planets' path tilts against the stars. 0 = vanilla sun on the equator.");

            configEquationOfTime = Config.Bind("Custom",
                                         "Equation of Time",
                                         true,
                                         "Custom difficulty only. Place the sun where the elliptical orbit and the axial tilt really put it. Noon then drifts up to about 20 minutes before or after 12:00 over the year; the Almanac sun table includes it.");

            configQuintantClock = Config.Bind("Custom",
                                         "Quintant Clock",
                                         false,
                                         "Custom difficulty only. Whether the quintant shows the time with each reading. Off in the other difficulties: the Navigator's Journal notes the time with each sight.");

            configChronometers = Config.Bind("Custom",
                                         "Chronometers",
                                         Keeping.Imperfect,
                                         "Custom difficulty only. Perfect: always right, never wound. Imperfect: each gains or loses at its own rate. Relic: none in the shops, and one you have stops for good after a few days. (Dead works like Relic.)");

            configPocketWatches = Config.Bind("Custom",
                                         "Pocket Watches",
                                         Keeping.Imperfect,
                                         "Custom difficulty only. Perfect: always right, never wound. Imperfect: each gains or loses, and must be wound. Dead: none in the shops, and one you have stands still.");

            configRateJumps = Config.Bind("Custom",
                                         "Chronometer Rate Jumps",
                                         false,
                                         "Custom difficulty only. Now and then a chronometer changes its rate without warning. With two or three aboard you see it in the spread between them; with one you don't.");

            configJournalGlassTime = Config.Bind("Custom",
                                         "Journal Keeps Time By The Glass",
                                         false,
                                         "Custom difficulty only. As in the Magellan difficulty: a lunar distance sets the ship's watch glass, which then strikes the hours by your reckoning, and the Navigator's Journal takes the time of each sight from it instead of from the pocket watch.");
										 
			configInstrumentSway = Config.Bind("Instruments", "Instrument Sway", 1.0f,
										new ConfigDescription("How much ship roll and wave motion swings the quadrant plumb line and astrolabe: 1 = normal, 0.5 = halved, 0 = completely steady (no swing).", 
										new AcceptableValueRange<float>(0f, 1f)));

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


            configWatchmakerLook = Config.Bind("Watchmaker", "Watchmaker Look", "",
                                         "How the watchmaker looks, written as the Sailwind Player Model mod writes a character (copy the Character line from its config), e.g. gender=1;head=3;hair=2;eyebrow=4;facialhair=18;headgear=2;torso=8;hips=4;legs=20;c_skin=5;c_hair=4;c_primary=4;c_secondary=3;c_leather=2;c_metal=4. That mod isn't needed. Set, it dresses the watchmakers of all three capitals; empty, each has his own look.");

            configHints = Config.Bind("Training", "Instrument Hints", false,
                                         "Hints for charts, books and instruments: look at one (or hold it) and press the Hint Key to read what it is for and how to use it. The Lost at Sea start turns this on, and off again when you land at Happy Bay.");

            configHintKey = Config.Bind("Training", "Hint Key", new KeyboardShortcut(KeyCode.H),
                                         "With Instrument Hints on: look at a chart, a book or an instrument (or hold it) and press this key to read what it is for and how to use it.");


            configJournalKey = Config.Bind("General",
                                         "Journal Key",
                                         new KeyboardShortcut(KeyCode.J),
                                         "Opens and closes the navigator's journal you carry (right click with the journal in hand works too). Escape also closes it.");

            configWatchWake = Config.Bind("General",
                                         "Watch Glass Wakes Captain",
                                         false,
                                         "Wake up when a running watch glass on your ship strikes eight bells, at the end of every watch (every four hours from the moment you started it).");

            configQuintantHelp = Config.Bind("General",      // The section under which the option is shown
                                         "Quintant Help",  // The key of the configuration option in the configuration file
                                         "Arc", // The default value
                                         "How much of the readings from the quintant should be displayed as text: None, Arc, Sec, Full."); // Description of the option to show in the config file
			configModNotifications = Config.Bind("General",
                "Mod Notifications",
                true,
                "Pop-up messages from this mod (readings, hints). Off: none of them; the game's own still show. Readings still go into the Navigator's Journal.");

            configTraverseMode = Config.Bind("Traverse Board", "Mode", TraverseMode.Automatic,
                "Automatic: aboard a boat, the traverse board puts in the pegs for each two hours by itself, from the course steered and the speed, when the first hour of the two is over (you may still move them by hand). Manual: you keep it yourself with the compass, the log and a sand glass.");
            configPegCords = Config.Bind("Traverse Board", "Peg Cords", true,
                "The traverse board's pegs hang on cords and swing with the ship. Off: no cords; a peg out of play sits in its resting hole.");
            configPointNames = Config.Bind("Traverse Board", "Point Names", TraverseNaming.NativeAndEuropean,
                "How the look text and the journal name the directions on the Emerald and Al'Ankh traverse boards. Native: their own names (Chou, rising of Naghma). NativeAndEuropean: both. European: the familiar compass points.");

            CompatClimate.Init(harmony, Config);
            CompatRadRefinements.Init(harmony);
        }

        internal static ConfigEntry<TraverseNaming> configPointNames;
        internal static ConfigEntry<TraverseMode> configTraverseMode;
        internal static ConfigEntry<bool> configPegCords;
        
        public const string PLUGIN_ID = "com.kingcam16.realisticskies";

        public const string PLUGIN_NAME = "RealisticSkies";

        public const string PLUGIN_VERSION = "2.3.0.1";   // released as 2.3.0F-1 (fork); BepInEx needs a plain version number here

        internal static ConfigEntry<string> configStarType;

        internal static ConfigEntry<string> configQuintantHelp;

        internal static ConfigEntry<bool> configWatchWake;

        internal static ConfigEntry<KeyboardShortcut> configJournalKey;

        // The debug tools (placing the goods of the shops and the watchmaker by hand, the layout capture of the
        // Lost at Sea ship, the asset census) are switched off for release: their settings are fixed
        // values here, not entries of the config file, so nothing of them shows or answers a key. To
        // work with them again, bind these as ConfigEntry in Awake (backups/debug-removed-2026-11-11).
        internal sealed class Fixed<T>
        {
            public T Value;
            public Fixed(T value) { Value = value; }
        }

        internal static readonly Fixed<bool> configShopPlacement = new Fixed<bool>(false);

        internal static readonly Fixed<KeyboardShortcut> configShopPlaceKey = new Fixed<KeyboardShortcut>(KeyboardShortcut.Empty);

        internal static readonly Fixed<string> configShopSpots = new Fixed<string>("");

        internal static ConfigEntry<bool> configHints;
        internal static ConfigEntry<KeyboardShortcut> configHintKey;
        internal static readonly Fixed<bool> configLayoutCapture = new Fixed<bool>(false);
        internal static readonly Fixed<KeyboardShortcut> configLayoutKey = new Fixed<KeyboardShortcut>(KeyboardShortcut.Empty);
        internal static readonly Fixed<KeyboardShortcut> configWatchmakerPickKey = new Fixed<KeyboardShortcut>(KeyboardShortcut.Empty), configWatchmakerPlaceKey = new Fixed<KeyboardShortcut>(KeyboardShortcut.Empty),
            configWatchmakerStallKey = new Fixed<KeyboardShortcut>(KeyboardShortcut.Empty), configWatchmakerTurnKey = new Fixed<KeyboardShortcut>(KeyboardShortcut.Empty);
        internal static readonly Fixed<string> configWatchmakerModel = new Fixed<string>(""), configWatchmakerStall = new Fixed<string>(""), configWatchmakerSpots = new Fixed<string>("");
        internal static readonly Fixed<float> configWatchmakerStallScale = new Fixed<float>(0.4f);
        internal static ConfigEntry<string> configWatchmakerLook;

        private void Update()
        {
            NavJournal.Tick();
            ShopSpots.Tick();
            Watchmaker.Tick();
            LostAtSeaNotes.Tick();
        }

        private void OnGUI()
        {
            NavJournal.OnGUI();
            LostAtSeaNotes.OnGUI();
        }

        internal static ConfigEntry<int> configYearLen;
        internal static ConfigEntry<float> configAimSensitivity;
        internal static ConfigEntry<float> configAimSmoothing;
        internal static ConfigEntry<float> configStudyZoom;

        internal static ConfigEntry<bool> configSiderialToggle;

        internal static ConfigEntry<bool> configMoonDimToggle;

        internal static ConfigEntry<bool> configMoonSizeToggle;
		
		internal static ConfigEntry<float> configMoonHalo;

        internal static ConfigEntry<bool> configZodiacToggle;

        internal static ConfigEntry<bool> configPlanetsToggle;

        internal static ConfigEntry<bool> configDisable;
        internal static ConfigEntry<Difficulty> configDifficulty;
        internal static ConfigEntry<Keeping> configChronometers, configPocketWatches;
        internal static ConfigEntry<bool> configCabinBoy, configRateJumps, configJournalGlassTime;
		
		internal static ConfigEntry<float> configInstrumentSway;

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
		
		internal static ConfigEntry<bool> configModNotifications;

        internal static Plugin instance;

        internal static ManualLogSource Log;
    }
}