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
				// the halo's own glow, kept to be scaled by the Moon Halo option
                Renderer haloR = MoonPatch.moonHalo.GetComponent<Renderer>();
                if (haloR.material.HasProperty("_EmissionColor")) MoonPatch.haloEmission = haloR.material.GetColor("_EmissionColor");


                MoonPatch.moonLight = GameObject.Find("moon light water");
                MoonPatch.moonLightComp = MoonPatch.moonLight.GetComponent<Light>();
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
            if (Plugin.configMoonSizeToggle.Value && MoonPatch.moonHalo != null)
            {
                float k = Plugin.configMoonHalo.Value;
                bool on = k > 0f;
                if (MoonPatch.moonHalo.activeSelf != on) MoonPatch.moonHalo.SetActive(on);
                if (on)
                {
                    float intensity = MoonPatch.moonLightComp.intensity;
                    MatSetup.SetAlpha(0.5f * intensity * k, MoonPatch.moonHalo);
                    Renderer haloR = MoonPatch.moonHalo.GetComponent<Renderer>();
                    if (haloR.material.HasProperty("_EmissionColor")) haloR.material.SetColor("_EmissionColor", MoonPatch.haloEmission * k);
                    MoonPatch.moonHalo.transform.LookAt(SkyboxManager.Skybox.transform.position);
                }
            }
        }

        public static Renderer rendererMoon;

        public static Renderer rendererZodiac;

        public static GameObject Halo;

        public static GameObject moonHalo;
		
		private static Color haloEmission = Color.white;

        public static GameObject Skybox;

        public static GameObject moonLight;

        private static Light moonLightComp;

        public static GameObject ZodiacBox;

        public static GameObject Zodiac;

        public static GameObject SunP;
    }
}

