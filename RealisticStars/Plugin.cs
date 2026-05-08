using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;


namespace RealisticStars
{
    [BepInPlugin(Plugin.PLUGIN_ID, Plugin.PLUGIN_NAME, Plugin.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            Plugin.instance = this;
            //Plugin.logSource = base.Logger;
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), "com.kingcam16.realisticskies");

            configStarType = Config.Bind("General",      // The section under which the option is shown
                                         "Star Type",  // The key of the configuration option in the configuration file
                                         "New", // The default value
                                         "Night sky to load: New, Real, Debug."); // Description of the option to show in the config file

            configYearLen = Config.Bind("General",
                                         "Year Length",
                                         92f,
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
                                         "Whether the Zodiacal light is visible. \nStill WIP, needs a new texture");

            configPlanetsToggle = Config.Bind("Planets",
                                         "Planets",
                                         true,
                                         "Whether the planets are visible. \nStill WIP, needs some variation/color");

        }
        
        public const string PLUGIN_ID = "com.kingcam16.realisticskies";

        public const string PLUGIN_NAME = "RealisticSkies";

        public const string PLUGIN_VERSION = "1.3.0";

        internal static ConfigEntry<string> configStarType;

        internal static ConfigEntry<float> configYearLen;

        internal static ConfigEntry<bool> configSiderialToggle;

        internal static ConfigEntry<bool> configMoonDimToggle;

        internal static ConfigEntry<bool> configMoonSizeToggle;

        internal static ConfigEntry<bool> configZodiacToggle;

        internal static ConfigEntry<bool> configPlanetsToggle;

        internal static Plugin instance;
    }
}