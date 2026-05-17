using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Runtime.Remoting.Metadata.W3cXsd2001;


namespace RealisticStars
{
    public class Patches
    {
        private static void LoadAssets()
        {
            string path = Paths.PluginPath + "\\RealisticStars";
            Patches.assets = AssetBundle.LoadFromFile(path + "\\newstars");

            GameObject Skybox = GameObject.Find("stars skybox");
            Patches.starMat = Skybox.GetComponent<MeshRenderer>().material;
        }

        public static AssetBundle assets;

        public static Material starMat;



        [HarmonyPatch(typeof(Stars))]
        public static class InitPatch
        {

            [HarmonyPostfix]
            [HarmonyPatch("Start")]
            public static void Initialization()
            {
                Patches.LoadAssets();

                Patches.yearLen = Plugin.configYearLen.Value;

                MoonPatch.StartMoonPatch();
                StarPatch.StartStarPatch();
                //ItemPatch.StartItemPatch();

                if (Plugin.configPlanetsToggle.Value)
                {
                    PlanetPatch.StartPlanetsPatch();
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch("Update")]
            public static void GlobalUpdates(ref Sun ___sunTime, ref float ___lerp)
            {
                Patches.lerp = ___lerp;

                int day = GameState.day;
                Patches.time = ___sunTime.globalTime;
                float dayAct = (float)day + Patches.time / 24;

                Patches.year = dayAct / Patches.yearLen;

                StarPatch.UpdateStarPatch();
                MoonPatch.UpdateMoonPatch();

                if (Plugin.configPlanetsToggle.Value)
                {
                    PlanetPatch.UpdatePlanetsPatch();
                }
            }
        }

        public static float yearLen;

        public static float time;

        public static float year;

        public static float lerp;



        public static GameObject SunP;
    }
}

