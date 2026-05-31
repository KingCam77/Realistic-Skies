using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;


namespace RealisticSkies
{
    public class SkyboxManager : MonoBehaviour
    {
        public static void StartSkybox()
        {
            // Get rid of old skybox
            SkyboxManager.OldSkybox = GameObject.Find("stars skybox");
            Destroy(SkyboxManager.OldSkybox.GetComponent<MeshFilter>());
    
            SkyboxManager.NewSkybox = (Patches.assets.LoadAsset("Assets/RealisticSkies.prefab") as GameObject);
            SkyboxManager.BaseSky = UnityEngine.Object.Instantiate<GameObject>(SkyboxManager.NewSkybox, Patches.Zenv.transform);

            SkyboxManager.Skybox = SkyboxManager.BaseSky.transform.GetChild(0).gameObject;
            SkyboxManager.starSkybox = SkyboxManager.BaseSky.transform.GetChild(0).GetChild(0).gameObject;
            SkyboxManager.eclipticSkybox = SkyboxManager.BaseSky.transform.GetChild(0).GetChild(1).gameObject;

            SkyboxManager.player = GameObject.FindWithTag("Player").transform;

            SkyboxManager.initialRot = SkyboxManager.BaseSky.transform.rotation;
        }


        public static void UpdateSkybox()
        {

            // Need to add longitude adjustment, rise earlier in EA, set later in AA

            Vector3 globeCoords = FloatingOriginManager.instance.GetGlobeCoords(SkyboxManager.player);
            float x = globeCoords.x;
            float z = globeCoords.z;

            // Rotate so north star is vertical
            SkyboxManager.Skybox.transform.rotation = SkyboxManager.initialRot;
            SkyboxManager.Skybox.transform.Rotate(Vector3.right, 90.0f-z, Space.World);

            SkyboxManager.Skybox.transform.Rotate(Vector3.up, x, Space.Self);


            float rotateVal = Patches.time * 15.0f;

            if (Plugin.configSiderialToggle.Value)
            {
                rotateVal = rotateVal + 360.0f * (Patches.year % 1);                
            }

            SkyboxManager.Skybox.transform.Rotate(Vector3.up, rotateVal, Space.Self);
        }

        public static GameObject BaseSky;

        public static GameObject OldSkybox;

        public static GameObject starSkybox;

        public static GameObject eclipticSkybox;

        public static GameObject NewSkybox;

        public static GameObject Skybox;

        public static Transform player;

        private static Quaternion initialRot;
    }
}

