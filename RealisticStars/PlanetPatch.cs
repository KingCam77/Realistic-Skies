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

    public struct Planet
    {
        public GameObject sprite;
        public float semiMajor;
        public float meanMotion;
        public float tau;
        public float absBright;
        public int type;
        public Vector2 pos;
    }

    public struct HPlanet
    {
        public float semiMajor;
        public float meanMotion;
        public float tau;
        public Vector2 pos;
    }


    public class PlanetPatch
    {

        public static void StartPlanetsPatch()
        {
            PlanetPatch.planetSprite = (Patches.assets.LoadAsset("Assets/Planet.prefab") as GameObject);

            PlanetPatch.planetHome.semiMajor = 1.0f;
            PlanetPatch.planetHome.meanMotion = 6.2831890627f;
            PlanetPatch.planetHome.tau = 0.2281f;
            PlanetPatch.planetHome.pos = Vector2.zero;

            PlanetPatch.planet1.semiMajor = 0.338f;
            PlanetPatch.planet1.meanMotion = 31.974570618f;
            PlanetPatch.planet1.tau = 0.12413f;
            PlanetPatch.planet1.absBright = -0.5497f;
            PlanetPatch.planet1.type = 1;
            PlanetPatch.planet1.pos = Vector2.zero;
            PlanetPatch.planet1.sprite = UnityEngine.Object.Instantiate<GameObject>(PlanetPatch.planetSprite, Patches.SunP.transform);
            MatSetup.SetMat("Assets/Texture2D/Bright.tif", PlanetPatch.planet1.sprite);
            MatSetup.SetMat("Assets/Texture2D/Glare.tif", PlanetPatch.planet1.sprite.transform.GetChild(0).gameObject);
            PlanetPatch.planet1.sprite.name = "Planet 1";

            PlanetPatch.planet2.semiMajor = 0.691f;
            PlanetPatch.planet2.meanMotion = 10.9386241548f;
            PlanetPatch.planet2.tau = 0.21625f;
            PlanetPatch.planet2.absBright = -3.02521f;
            PlanetPatch.planet2.type = 2;
            PlanetPatch.planet2.pos = Vector2.zero;
            PlanetPatch.planet2.sprite = UnityEngine.Object.Instantiate<GameObject>(PlanetPatch.planetSprite, Patches.SunP.transform);
            MatSetup.SetMat("Assets/Texture2D/Bright.tif", PlanetPatch.planet2.sprite);
            MatSetup.SetMat("Assets/Texture2D/Glare.tif", PlanetPatch.planet2.sprite.transform.GetChild(0).gameObject);
            PlanetPatch.planet2.sprite.name = "Planet 2";

            PlanetPatch.planet3.semiMajor = 1.923f;
            PlanetPatch.planet3.meanMotion = 2.35619291187f;
            PlanetPatch.planet3.tau = 1.62493f;
            PlanetPatch.planet3.absBright = -2.8434f;
            PlanetPatch.planet3.type = 2;
            PlanetPatch.planet3.pos = Vector2.zero;
            PlanetPatch.planet3.sprite = UnityEngine.Object.Instantiate<GameObject>(PlanetPatch.planetSprite, Patches.SunP.transform);
            MatSetup.SetMat("Assets/Texture2D/Bright.tif", PlanetPatch.planet3.sprite);
            MatSetup.SetMat("Assets/Texture2D/Glare.tif", PlanetPatch.planet3.sprite.transform.GetChild(0).gameObject);
            PlanetPatch.planet3.sprite.name = "Planet 3";

            PlanetPatch.planet4.semiMajor = 3.162f;
            PlanetPatch.planet4.meanMotion = 1.1174728314f;
            PlanetPatch.planet4.tau = 0.12822f;
            PlanetPatch.planet4.absBright = 0.87155f;
            PlanetPatch.planet4.type = 1;
            PlanetPatch.planet4.pos = Vector2.zero;
            PlanetPatch.planet4.sprite = UnityEngine.Object.Instantiate<GameObject>(PlanetPatch.planetSprite, Patches.SunP.transform);
            MatSetup.SetMat("Assets/Texture2D/Bright.tif", PlanetPatch.planet4.sprite);
            MatSetup.SetMat("Assets/Texture2D/Glare.tif", PlanetPatch.planet4.sprite.transform.GetChild(0).gameObject);
            PlanetPatch.planet4.sprite.name = "Planet 4";

            PlanetPatch.planet5.semiMajor = 5.265f;
            PlanetPatch.planet5.meanMotion = 0.520094436661f;
            PlanetPatch.planet5.tau = 9.81109f;
            PlanetPatch.planet5.absBright = -10.13936f;
            PlanetPatch.planet5.type = 3;
            PlanetPatch.planet5.pos = Vector2.zero;
            PlanetPatch.planet5.sprite = UnityEngine.Object.Instantiate<GameObject>(PlanetPatch.planetSprite, Patches.SunP.transform);
            MatSetup.SetMat("Assets/Texture2D/Bright.tif", PlanetPatch.planet5.sprite);
            MatSetup.SetMat("Assets/Texture2D/Glare.tif", PlanetPatch.planet5.sprite.transform.GetChild(0).gameObject);
            PlanetPatch.planet5.sprite.name = "Planet 5";

            PlanetPatch.planet6.semiMajor = 10.698f;
            PlanetPatch.planet6.meanMotion = 0.179566732971f;
            PlanetPatch.planet6.tau = 23.8827f;
            PlanetPatch.planet6.absBright = -9.28114f;
            PlanetPatch.planet6.type = 3;
            PlanetPatch.planet6.pos = Vector2.zero;
            PlanetPatch.planet6.sprite = UnityEngine.Object.Instantiate<GameObject>(PlanetPatch.planetSprite, Patches.SunP.transform);
            MatSetup.SetMat("Assets/Texture2D/Bright.tif", PlanetPatch.planet6.sprite);
            MatSetup.SetMat("Assets/Texture2D/Glare.tif", PlanetPatch.planet6.sprite.transform.GetChild(0).gameObject);
            PlanetPatch.planet6.sprite.name = "Planet 6";

            PlanetPatch.planet7.semiMajor = 17.223f;
            PlanetPatch.planet7.meanMotion = 0.0879056053822f;
            PlanetPatch.planet7.tau = 12.896f;
            PlanetPatch.planet7.absBright = -7.3065f;
            PlanetPatch.planet7.type = 3;
            PlanetPatch.planet7.pos = Vector2.zero;
            PlanetPatch.planet7.sprite = UnityEngine.Object.Instantiate<GameObject>(PlanetPatch.planetSprite, Patches.SunP.transform);
            MatSetup.SetMat("Assets/Texture2D/Bright.tif", PlanetPatch.planet7.sprite);
            MatSetup.SetMat("Assets/Texture2D/Glare.tif", PlanetPatch.planet7.sprite.transform.GetChild(0).gameObject);
            PlanetPatch.planet7.sprite.name = "Planet 7";
        }

        public static GameObject planetSprite;

        public static HPlanet planetHome;

        public static Planet planet1;

        public static Planet planet2;

        public static Planet planet3;

        public static Planet planet4;

        public static Planet planet5;

        public static Planet planet6;

        public static Planet planet7;



        public static void UpdatePlanetsPatch()
        {

            CalculateHome();
            CalculatePlanet(PlanetPatch.planet1);
            CalculatePlanet(PlanetPatch.planet2);
            CalculatePlanet(PlanetPatch.planet3);
            CalculatePlanet(PlanetPatch.planet4);
            CalculatePlanet(PlanetPatch.planet5);
            CalculatePlanet(PlanetPatch.planet6);
            CalculatePlanet(PlanetPatch.planet7);
        }


        public static void CalculateHome()
        {
            float Anom = 6.28318390627f * (Patches.year - PlanetPatch.planetHome.tau);
            float x = Mathf.Cos(Anom);
            float y = Mathf.Sin(Anom);
            PlanetPatch.planetHome.pos.Set(x,y);
        }


        public static void CalculatePlanet(Planet planetstruct)
        {
            float Anom = planetstruct.meanMotion * (Patches.year - planetstruct.tau);
            float x = planetstruct.semiMajor * Mathf.Cos(Anom);
            float y = planetstruct.semiMajor * Mathf.Sin(Anom);
            planetstruct.pos.Set(x, y);

            Vector2 D = PlanetPatch.planetHome.pos - planetstruct.pos;
            float E = Vector2.SignedAngle(D, PlanetPatch.planetHome.pos);

            float zd = -6500 * Mathf.Cos(E * Mathf.Deg2Rad);
            float yd = -6500 * Mathf.Sin(E * Mathf.Deg2Rad);

            planetstruct.sprite.transform.localPosition = new Vector3(0.0f, yd, zd);
            planetstruct.sprite.transform.LookAt(StarPatch.Skybox.transform.position);

            float P = Vector2.Angle(-planetstruct.pos, D);

            float Q = 0;
            if (planetstruct.type == 1)
            {
                Q = 0.72f * Mathf.Exp(-3.332f * Mathf.Pow(Mathf.Tan(P / 2.0f * Mathf.Deg2Rad), 0.631f)) + 0.28f * Mathf.Exp(-1.862f * Mathf.Pow(Mathf.Tan(P / 2.0f * Mathf.Deg2Rad), 1.218f));
            }
            else if (planetstruct.type == 2)
            {
                Q = -0.00106f * P + 0.0002054f * P * P;
            }
            else if (planetstruct.type == 3)
            {
                Q = -0.00037f * P + 0.000615f * P * P;
            }

            float RD = D.magnitude * planetstruct.pos.magnitude;
            float apparent = planetstruct.absBright + 5 * Mathf.Log(RD, 10) + Q;

            GameObject glareobj = planetstruct.sprite.transform.GetChild(0).gameObject;

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
                MatSetup.SetAlphaAndGrey(Patches.lerp * Patches.lerp * StarPatch.scale, alphaScale, greyScale, planetstruct.sprite);
            }
            else
            {
                float kq = Mathf.Sqrt(Mathf.Pow(3.0f - apparent + 0.167f, 2.512f) - 1) / 250.0f / 0.001381f;
                planetstruct.sprite.SetActive(true);
                glareobj.SetActive(true);
                glareobj.transform.localScale = new Vector3(kq, kq, 1.0f);
                MatSetup.SetAlpha(Patches.lerp * Patches.lerp * StarPatch.scale, glareobj);
                MatSetup.SetAlpha(Patches.lerp * Patches.lerp * StarPatch.scale, planetstruct.sprite);
            }
        }
    }
}

