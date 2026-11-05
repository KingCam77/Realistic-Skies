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
				MoonPatch.haloHome = MoonPatch.moonHalo.transform.localPosition;


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
            if (!Plugin.configMoonSizeToggle.Value || MoonPatch.moonHalo == null) return;

            // the glow follows the phase: full at full moon, none at new moon
            float lit = MoonPatch.LitFraction();
            float k = Plugin.configMoonHalo.Value * (lit < MinLit ? 0f : Mathf.Pow(lit, 1.5f));
            bool on = k > 0.005f;
            if (MoonPatch.moonHalo.activeSelf != on) MoonPatch.moonHalo.SetActive(on);
            if (!on) return;

            float intensity = MoonPatch.moonLightComp.intensity;
            MatSetup.SetAlpha(0.5f * intensity * k, MoonPatch.moonHalo);
            Renderer haloR = MoonPatch.moonHalo.GetComponent<Renderer>();
            if (haloR.material.HasProperty("_EmissionColor")) haloR.material.SetColor("_EmissionColor", MoonPatch.haloEmission * k);

            MoonPatch.ShiftToLitSide(lit);
            MoonPatch.moonHalo.transform.LookAt(SkyboxManager.Skybox.transform.position);
        }

        /// <summary>The lit part of the moon's disc, 0 (new) .. 1 (full), from its angle to the sun.</summary>
        private static float LitFraction()
        {
            if (Sun.sun == null) return 1f;
            double e = Lunar.Distance(Lunar.SunIndex, GameState.day, Sun.sun.globalTime);
            return (float)((1.0 - Math.Cos(e * Mathf.Deg2Rad)) / 2.0);
        }

        /// <summary>The glow comes from the lit limb: the halo is moved off the middle toward the sun, the more the thinner the moon.</summary>
        private static void ShiftToLitSide(float lit)
        {
            Transform moon = MoonPatch.moonHalo.transform.parent;
            if (moon == null || Sun.sun == null) return;
            Vector3 home = moon.TransformPoint(MoonPatch.haloHome);
            Vector3 toMoon = (moon.position - SkyboxManager.Skybox.transform.position).normalized;

            double hour = Sun.sun.globalTime;
            Lunar.BodyGHADec(Lunar.SunIndex, GameState.day, hour, out double gha, out double dec);
            Vector3 sun = Lunar.WorldDirection(gha, dec, hour);
            Vector3 side = Vector3.ProjectOnPlane(sun, toMoon);     // toward the sun, across the sky at the moon
            if (float.IsNaN(side.x) || side.sqrMagnitude < 1e-6f)
            {
                MoonPatch.moonHalo.transform.position = home;       // sky axes not known yet: stays centred
                return;
            }
            if (MoonPatch.moonRadius <= 0f)
            {
                Renderer r = moon.GetComponent<Renderer>();
                if (r != null) MoonPatch.moonRadius = r.bounds.extents.x;
            }
            MoonPatch.moonHalo.transform.position = home + side.normalized * MoonPatch.moonRadius * HaloShift * (1f - lit);
        }

        public static Renderer rendererMoon;

        public static Renderer rendererZodiac;

        public static GameObject Halo;

        public static GameObject moonHalo;
		
		private static Color haloEmission = Color.white;
		
		private static Vector3 haloHome;
		
        private static float moonRadius;
		
        private const float MinLit = 0.02f;     // a thinner crescent gets no halo
		
        private const float HaloShift = 0.6f;   // how far toward the lit limb, in moon radii, at its thinnest

        public static GameObject Skybox;

        public static GameObject moonLight;

        private static Light moonLightComp;

        public static GameObject ZodiacBox;

        public static GameObject Zodiac;

        public static GameObject SunP;
    }
}

