using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;


namespace RealisticSkies
{
    public class MoonPatch
    {
        public static void StartMoonPatch()
        {
            // Moon Size Change Setup
            if (Plugin.configMoonSizeToggle.Value)
            {

                GameObject Moon = GameObject.Find("moon");
                Moon.transform.localScale = new Vector3(60.0f, 60.0f, 60.0f);

                MoonPatch.Halo = (Patches.assets.LoadAsset("Assets/moon halo.prefab") as GameObject);
                MoonPatch.moonHalo = UnityEngine.Object.Instantiate<GameObject>(MoonPatch.Halo, Moon.transform);

                MatSetup.SetMat("Assets/Texture2D/MoonHalo.tif", MoonPatch.moonHalo);

                MoonPatch.moonLight = GameObject.Find("moon light water");
            }

            // Enable Zodiac
            if (Plugin.configZodiacToggle.Value)
            {
                Patches.SunP = GameObject.Find("sun parent");

                MoonPatch.ZodiacBox = (Patches.assets.LoadAsset("Assets/Zodiac.prefab") as GameObject);
                MoonPatch.Zodiac = UnityEngine.Object.Instantiate<GameObject>(MoonPatch.ZodiacBox, Patches.SunP.transform);

                MatSetup.SetMat("Assets/Texture2D/ZodiacTex.tif", MoonPatch.Zodiac);
            }

        }

        public static void UpdateMoonPatch()
        {
            if (Plugin.configMoonSizeToggle.Value)
            {
                
                float intensity = MoonPatch.moonLight.GetComponent<Light>().intensity;
                MatSetup.SetAlpha(0.5f * intensity, MoonPatch.moonHalo);

                MoonPatch.moonHalo.transform.LookAt(SkyboxManager.Skybox.transform.position);

            }
        }

        public static Renderer rendererMoon;

        public static Renderer rendererZodiac;

        public static GameObject Halo;

        public static GameObject moonHalo;

        public static GameObject Skybox;

        public static GameObject moonLight;

        public static GameObject ZodiacBox;

        public static GameObject Zodiac;

        public static GameObject SunP;
    }
}

