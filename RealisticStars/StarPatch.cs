using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;


namespace RealisticStars
{
    public class StarPatch
    {
        public static void StartStarPatch()
        {
            // Skybox Setup
            StarPatch.Skybox = GameObject.Find("stars skybox");
            StarPatch.Skybox.transform.localEulerAngles = Vector3.zero;

            if(Plugin.configStarType.Value == "New")
                texpath = "Assets/Texture2D/ImprovedStars.tif";
            else if (Plugin.configStarType.Value == "Real")
                texpath = "Assets/Texture2D/RealStars.tif";
            else
                texpath = "Assets/Texture2D/DebugStars.tif";

            MatSetup.SetMat(texpath, StarPatch.Skybox);

            StarPatch.moonLight = GameObject.Find("moon light water");

            // Sideral Rot Setup
            SiderialRot.Set(0.0f, 0.0f, 0.0f);
        }


        public static void UpdateStarPatch()
        {
            if(Plugin.configSiderialToggle.Value)
            {
                float rotateVal = -360 * (Patches.year % 1);

                SiderialRot.Set(rotateVal, 0.0f, 0.0f);

                StarPatch.Skybox.transform.localEulerAngles = SiderialRot;
            }

            float intensity = StarPatch.moonLight.GetComponent<Light>().intensity;
            StarPatch.scale = (-0.75f * intensity * intensity + 1);

            if (Plugin.configMoonDimToggle.Value)
            {
                MatSetup.SetAlpha(Patches.lerp * Patches.lerp * Patches.lerp * StarPatch.scale, StarPatch.Skybox);
            }

            if (Plugin.configZodiacToggle.Value)
            {
                MatSetup.SetAlpha(Patches.lerp * StarPatch.scale * 0.05f, MoonPatch.Zodiac);
            }

        }
        internal static string texpath;

        public static Vector3 SiderialRot;

        public static GameObject Skybox;

        public static GameObject moonLight;

        public static float scale;
    }
}

