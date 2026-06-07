using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;


namespace RealisticSkies
{
    public struct Planet
    {
        public GameObject sprite;

        public float a;
        public float ecc;
        public float inc;       //Degrees
        public float omega;     //Degrees
        public float Omega;     //Degrees
        public float tau;
        public float n;

        public float absBright;
        public int type;
        public Vector3 pos;

        public Planet(float a, float ecc, float inc, float omega, float Omega, float tau, float n, float absBright, int type, int flag)
        {
            this.a = a;
            this.ecc = ecc;
            this.inc = inc * Mathf.Deg2Rad;
            this.omega = omega * Mathf.Deg2Rad;
            this.Omega = Omega * Mathf.Deg2Rad;
            this.tau = tau;
            this.n = n;

            this.absBright = absBright;
            this.type = type;

            this.sprite = UnityEngine.Object.Instantiate<GameObject>(PlanetPatch.planetSprite, SkyboxManager.eclipticSkybox.transform);

            if (flag == 1)
            {
                MatSetup.SetMat("Assets/Texture2D/PlanetTex/Bright.tif", this.sprite);
                MatSetup.SetMat("Assets/Texture2D/PlanetTex/Glare.tif", this.sprite.transform.GetChild(0).gameObject);
            }
            else if (flag == 2)
            {
                MatSetup.SetMat("Assets/Texture2D/PlanetTex/OrangeBright.tif", this.sprite);
                MatSetup.SetMat("Assets/Texture2D/PlanetTex/OrangeGlare.tif", this.sprite.transform.GetChild(0).gameObject);
            }
            else if (flag == 3)
            {
                MatSetup.SetMat("Assets/Texture2D/PlanetTex/BlueBright.tif", this.sprite);
                MatSetup.SetMat("Assets/Texture2D/PlanetTex/BlueGlare.tif", this.sprite.transform.GetChild(0).gameObject);
            }
            else if (flag == 4)
            {
                MatSetup.SetMat("Assets/Texture2D/PlanetTex/YellowBright.tif", this.sprite);
                MatSetup.SetMat("Assets/Texture2D/PlanetTex/YellowGlare.tif", this.sprite.transform.GetChild(0).gameObject);
            }
            else
            {
                this.sprite.SetActive(false);
                this.sprite.name = "homeplanetdebug";
            }

            this.pos = Vector3.one;

        }

    }

    public class PlanetPatch
    {

        

        public static GameObject planetSprite;

        
        public static Planet planet1;
        public static Planet planet2;
        public static Planet planetHome;
        public static Planet planet4;
        public static Planet planet5;
        public static Planet planet6;
        public static Planet planet7;
        public static Planet planet8;

        public static void StartPlanetsPatch()
        {
            PlanetPatch.planetSprite = (Patches.assets.LoadAsset("Assets/Planet.prefab") as GameObject);

            // Create all the planets

            //                            a,     ecc,     inc,     omega,     Omega,           tau,             n,            
            planet1 =    new Planet( 0.338f, 0.2551f, 7.2318f, 232.8555f, 190.2106f,  0.149723693f, 31.938026275f,  0.1900f, 1, 1);
            planet2 =    new Planet( 0.691f, 0.0481f, 3.7061f, 138.0542f, 181.0618f,  0.252671978f, 10.935911572f, -2.2856f, 2, 4);
            planetHome = new Planet( 1.000f, 0.0299f, 0.0000f,  82.1461f,   0.0000f,  0.781158692f,  6.282937626f,  0.0000f, 2, 0);
            planet4 =    new Planet( 1.913f, 0.0599f, 1.5124f,  83.9214f, 160.2411f,  3.277829459f,  2.373701518f, -2.1037f, 2, 2);
            planet5 =    new Planet( 3.161f, 0.0301f, 2.2562f, 180.2270f, 158.8375f,  1.330928882f,  1.117951638f,  1.6111f, 1, 1);
            planet6 =    new Planet( 5.265f, 0.0428f, 1.3859f, 109.6172f, 181.3787f,  3.652533004f,  0.520007917f, -9.3997f, 3, 3);
            planet7 =    new Planet(10.698f, 0.0378f, 1.3687f,   3.5402f, 214.7115f, 11.557890809f,  0.179568460f, -8.5668f, 3, 4);
            planet8 =    new Planet(17.223f, 0.0522f, 1.9646f, 205.7822f, 190.8910f, 38.382823613f,  0.087902710f, -6.5668f, 3, 1);
        }


        public static void UpdatePlanetsPatch()
        {
            PlanetPatch.planetHome.pos = calcPlanetPos(PlanetPatch.planetHome, Patches.year);
            CalculatePlanet(PlanetPatch.planet1);
            CalculatePlanet(PlanetPatch.planet2);
            CalculatePlanet(PlanetPatch.planet4);
            CalculatePlanet(PlanetPatch.planet5);
            CalculatePlanet(PlanetPatch.planet6);
            CalculatePlanet(PlanetPatch.planet7);
            CalculatePlanet(PlanetPatch.planet8);
        }

        public static Vector3 calcPlanetPos(Planet planetstruct, float year)
        {
            // This is all in the ecliptic plane, which is currently the same as the equatorial plane

            float ecc2 = planetstruct.ecc * planetstruct.ecc;

            float Ma = planetstruct.n * (year + planetstruct.tau);
            // first order approximation of true anomaly
            float v = Ma + (2f * planetstruct.ecc - 0.25f * ecc2) * Mathf.Sin(Ma);

            float r = (planetstruct.a * (1 - ecc2)) / (1 + planetstruct.ecc * Mathf.Cos(v));

            // Trig stuff
            float Co = Mathf.Cos(planetstruct.Omega);
            float So = Mathf.Sin(planetstruct.Omega);
            float Cv = Mathf.Cos(planetstruct.omega + v);
            float Sv = Mathf.Sin(planetstruct.omega + v);
            float Ci = Mathf.Cos(planetstruct.inc);

            float x = r * (Co * Cv - So * Sv * Ci);
            float y = r * (So * Cv + Co * Sv * Ci);
            float z = r * (Mathf.Sin(planetstruct.inc) * Sv);

            Vector3 pos = new Vector3(x, y, z);

            return pos;
        }

        public static void CalculatePlanet(Planet planetstruct)
        {

            planetstruct.pos = calcPlanetPos(planetstruct, Patches.year);

            //Debug.Log($"Home   Pos X: {PlanetPatch.planetHome.pos.x}, Y: {PlanetPatch.planetHome.pos.y}, Z: {PlanetPatch.planetHome.pos.z}");
            //Debug.Log($"Planet Pos X: {planetstruct.pos.x}, Y: {planetstruct.pos.y}, Z: {planetstruct.pos.z}");

            Vector3 D = planetstruct.pos - PlanetPatch.planetHome.pos;

            float longitude = Mathf.Atan2(D.y, D.x);
            float Dec = Mathf.Asin(planetstruct.pos.z / D.magnitude);

            //Debug.Log($"Long: {longitude}, Declination: {Dec}");

            float zd = -6500 * Mathf.Cos(longitude) * Mathf.Cos(Dec);
            float xd = 6500 * Mathf.Sin(longitude) * Mathf.Cos(Dec);
            float yd = 6500 * Mathf.Sin(Dec);

            planetstruct.sprite.transform.localPosition = new Vector3(xd, yd, zd);
            planetstruct.sprite.transform.LookAt(SkyboxManager.BaseSky.transform.position);



            float P = Vector3.Angle(-planetstruct.pos, -D);

            float Q = 0;
            if (planetstruct.type == 1)
            {
                Q = -2.5f * Mathf.Log(0.72f * Mathf.Exp(-3.332f * Mathf.Pow(Mathf.Tan(P / 2.0f * Mathf.Deg2Rad), 0.631f)) + 0.28f * Mathf.Exp(-1.862f * Mathf.Pow(Mathf.Tan(P / 2.0f * Mathf.Deg2Rad), 1.218f)), 10.0f);
            }
            else if (planetstruct.type == 2)
            {
                Q = -0.00106f * P + 0.0002054f * P * P;
            }
            else if (planetstruct.type == 3)
            {
                Q = -0.00037f * P + 0.000615f * P * P;
            }

            float RD = D.magnitude * planetstruct.pos.magnitude * 0.68f * 0.68f;
            float apparent = planetstruct.absBright + 5 * Mathf.Log(RD, 10.0f) + Q;

            GameObject glareobj = planetstruct.sprite.transform.GetChild(0).gameObject;

            float alpha = 0;
            if (Plugin.configMoonDimToggle.Value)
            {
                alpha = Patches.lerp * Patches.lerp * StarPatch.scale;
            }
            else
            {
                alpha = Patches.lerp * Patches.lerp;
            }

            if (apparent > 6.8f)
            {
                planetstruct.sprite.SetActive(false);
                glareobj.SetActive(false);
            }
            else if (apparent > 3.0f)
            {
                float alphaScale = 3.51092f * Mathf.Pow(0.657904f, apparent);
                float greyScale = 6.2128f * Mathf.Pow(0.54391f, apparent);

                planetstruct.sprite.SetActive(true);
                glareobj.SetActive(false);
                MatSetup.SetAlphaAndGrey(alpha, alphaScale, greyScale, planetstruct.sprite);
            }
            else
            {
                float kq = Mathf.Sqrt(Mathf.Pow(3.0f - apparent + 0.167f, 2.512f) - 1) / 250.0f / 0.001381f;
                planetstruct.sprite.SetActive(true);
                glareobj.SetActive(true);
                glareobj.transform.localScale = new Vector3(kq, kq, 1.0f);
                MatSetup.SetAlpha(alpha, glareobj);
                MatSetup.SetAlpha(alpha, planetstruct.sprite);
            }
        }
    }
}

