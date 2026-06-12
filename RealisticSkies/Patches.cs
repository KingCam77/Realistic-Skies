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

            var mats = assets.LoadAllAssets(typeof(Material));
            foreach (Material m in mats)
            {
                var shaderName = m.shader.name;
                //Debug.Log("RealisticSkies: trying to refresh shader: " + shaderName + " in material " + m.name);
                var newShader = Shader.Find(shaderName);
                if (newShader != null)
                {
                    m.shader = newShader;
                    //Debug.Log("RealisticSkies: refreshed shader: " + shaderName + " in material " + m.name);

                }
                else
                {
                    Debug.LogWarning("RealisticSkies: unable to refresh shader: " + shaderName + " in material " + m.name);
                }
            }

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
                    if (saveable.prefabIndex == 705 && gameObject.GetComponent<ShipItem>() is ShipItem)
                    {
                        Destroy(gameObject.GetComponent<ShipItem>());
                        gameObject.AddComponent<ModItemPlanisphere>();
                        Debug.Log($"RealisticSkies: replaced planisphere ship item");
                    }
                } 
                else
                {
                    if (gameObject.GetComponent<ShopInfo>() is ShopInfo info)
                    {
                        Patches.shopKeepers.Add(info.parentIslandIndex, info);

                        Debug.Log($"RealisticSkies: added {info.name} to directory");

                        if (info.parentIslandIndex == 27)
                        {
                            //Load Observatory stuff
                            gameObject.transform.GetChild(2).GetChild(2).gameObject.AddComponent<ObsTelescope>();

                            gameObject.transform.GetChild(2).GetChild(6).gameObject.AddComponent<Ladder>();
                            //var streetlight = GameObject.Find("east_street_lamp (1)").GetComponent<IslandStreetlight>().streetlightManager;
                            //gameObject.transform.GetChild(2).GetChild(5).GetChild(1).gameObject.GetComponent<IslandStreetlight>().streetlightManager = streetlight;

                        }
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
                if (Patches.assets == null)
                {
                    Patches.LoadAssets();
                }

                Patches.Zenv = GameObject.Find("Z environment");

                Patches.yearLen = Plugin.configYearLen.Value;
                if (!Plugin.configDisable.Value)
                {
                    SkyboxManager.StartSkybox();

                    MoonPatch.StartMoonPatch();
                    StarPatch.StartStarPatch();

                    if (Plugin.configPlanetsToggle.Value)
                    {
                        PlanetPatch.StartPlanetsPatch();
                    }
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

                if (!Plugin.configDisable.Value)
                {
                    SkyboxManager.UpdateSkybox();

                    StarPatch.UpdateStarPatch();
                    MoonPatch.UpdateMoonPatch();

                    if (Plugin.configPlanetsToggle.Value)
                    {
                        PlanetPatch.UpdatePlanetsPatch();
                    }
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

