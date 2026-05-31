using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Collections.Generic;


namespace RealisticSkies
{
    public class Patches : MonoBehaviour
    {
        public static void LoadAssets()
        {
            string path = Paths.PluginPath + "\\RealisticSkies";
            Patches.assets = AssetBundle.LoadFromFile(path + "\\realisticskies");

            GameObject Skybox = GameObject.Find("stars skybox");
            Patches.starMat = Skybox.GetComponent<MeshRenderer>().material;

            foreach (GameObject gameObject in Patches.assets.LoadAllAssets<GameObject>())
            {
                if (gameObject.GetComponent<SaveablePrefab>() is SaveablePrefab saveable)
                {
                    Patches.itemPrefabs.Add(saveable.prefabIndex, gameObject);

                    Debug.Log($"RealisticSkies: Added {gameObject.name} to asset directory");

                    if (saveable.prefabIndex == 702 && gameObject.GetComponent<ShipItem>() is ShipItem)
                    {
                        Destroy(gameObject.GetComponent<ShipItem>());
                        gameObject.AddComponent<ModItemQuintant>();
                        Debug.Log($"RealisticSkies: replaced quintant ship item");
                    }
                    if (saveable.prefabIndex == 701 && gameObject.GetComponent<ShipItem>() is ShipItem)
                    {
                        Destroy(gameObject.GetComponent<ShipItem>());
                        gameObject.AddComponent<ModItemAlmanac>();
                        Debug.Log($"RealisticSkies: replaced almanac ship item");
                    }
                } 
                else
                {
                    if (gameObject.GetComponent<ShopInfo>() is ShopInfo info)
                    {
                        Patches.shopKeepers.Add(info.parentIslandIndex, info);

                        Debug.Log($"RealisticSkies: added {info.name} to directory");
                    }
                }
            }


        }

        public static AssetBundle assets;

        public static Material starMat;

        public static Dictionary<int, GameObject> itemPrefabs = new Dictionary<int, GameObject>();

        public static Dictionary<int, ShopInfo> shopKeepers = new Dictionary<int, ShopInfo>();





        [HarmonyPatch(typeof(PrefabsDirectory))]
        public class ItemPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch("PopulateShipItems")]
            public static void StartItemPatch()
            {
                foreach (KeyValuePair<int, GameObject> keyValuePair in Patches.itemPrefabs)
                {
                    if (keyValuePair.Key >= PrefabsDirectory.instance.directory.Length)
                    {
                        Array.Resize<GameObject>(ref PrefabsDirectory.instance.directory, keyValuePair.Key + 5);
                        Debug.Log("RealisticSkies: Resized directory to " + PrefabsDirectory.instance.directory.Length.ToString() + " to accommodate " + keyValuePair.Value.name);
                    }
                    if (PrefabsDirectory.instance.directory[keyValuePair.Key] == null)
                    {
                        PrefabsDirectory.instance.directory[keyValuePair.Key] = keyValuePair.Value;
                    }
                    else
                    {
                        Debug.LogWarning(string.Format("RealisticSkies: Prefab at index {0} already exists in directory, skipping {1}", keyValuePair.Key, keyValuePair.Value.name));
                    }
                }
            }
        }



        [HarmonyPatch(typeof(Stars))]
        public static class InitPatch
        {

            [HarmonyPostfix]
            [HarmonyPatch("Start")]
            public static void Initialization()
            {
                Patches.LoadAssets();

                Patches.Zenv = GameObject.Find("Z environment");

                Patches.yearLen = Plugin.configYearLen.Value;

                SkyboxManager.StartSkybox();

                MoonPatch.StartMoonPatch();
                StarPatch.StartStarPatch();

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

                Patches.year = dayAct / (float) Patches.yearLen;

                SkyboxManager.UpdateSkybox();

                StarPatch.UpdateStarPatch();
                MoonPatch.UpdateMoonPatch();

                if (Plugin.configPlanetsToggle.Value)
                {
                    PlanetPatch.UpdatePlanetsPatch();
                }
            }
        }

        public static int yearLen;

        public static float time;

        public static float year;

        public static float lerp;


        public static GameObject Zenv;

        public static GameObject SunP;
    }
}

