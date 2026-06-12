using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;


namespace RealisticSkies
{
    public class StarPatch
    {
        public static void StartStarPatch()
        {
            //StarPatch.Skybox.transform.localEulerAngles = Vector3.zero;

            if(Plugin.configStarType.Value == "New")
                texpath = "Assets/Texture2D/ImprovedStars.tif";
            else if (Plugin.configStarType.Value == "Real")
                texpath = "Assets/Texture2D/RealStars.tif";
            else
                texpath = "Assets/Texture2D/DebugStars.tif";

            MatSetup.SetMat(texpath, SkyboxManager.starSkybox);

            StarPatch.moonLight = GameObject.Find("moon light water");
        }


        public static void UpdateStarPatch()
        {
            
            
            float intensity = StarPatch.moonLight.GetComponent<Light>().intensity;
            StarPatch.scale = (-0.75f * intensity * intensity + 1);

            if (Plugin.configMoonDimToggle.Value)
            {
                MatSetup.SetAlpha(Patches.lerp * Patches.lerp * Patches.lerp * StarPatch.scale, SkyboxManager.starSkybox);
            }
            else
            {
                MatSetup.SetAlpha(Patches.lerp * Patches.lerp * Patches.lerp, SkyboxManager.starSkybox);
            }


            if (Plugin.configZodiacToggle.Value)
            {
                MatSetup.SetAlpha(Patches.lerp * StarPatch.scale * 0.05f, MoonPatch.Zodiac);
            }

        }
        internal static string texpath;

        public static Vector3 SiderialRot;

        public static GameObject OldSkybox;

        public static GameObject moonLight;

        public static float scale;
    }
}

