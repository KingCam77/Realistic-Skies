using System;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    [HarmonyPatch(typeof(IslandStreetlightsManager))]
    internal static class SpawnPatch
    {
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        internal static void Postfix(IslandStreetlightsManager __instance)
        {
            if (Patches.assets == null)
            {
                Patches.LoadAssets();
            }
            Debug.Log("RealisticSkies: patching shopkeeper");
            int parentIslandIndex = __instance.gameObject.GetComponent<IslandSceneryScene>().parentIslandIndex;
            if (Patches.shopKeepers.ContainsKey(parentIslandIndex))
            {
                ShopInfo info = UnityEngine.Object.Instantiate<ShopInfo>(Patches.shopKeepers[parentIslandIndex], __instance.transform);
                info.shopPrefab.transform.parent = __instance.transform;
                ShopSpots.Shops[parentIslandIndex] = info.shopPrefab.transform;
                ModItemLoreScroll.AddToShop(info.shopPrefab.transform, parentIslandIndex);
                ModItemHourglass.AddToShop(info.shopPrefab.transform, parentIslandIndex);
                ModItemWatchGlass.AddToShop(info.shopPrefab.transform, parentIslandIndex);
                ModItemPocketWatch.AddToShop(info.shopPrefab.transform, parentIslandIndex);
                ModItemJournal.AddToShop(info.shopPrefab.transform, parentIslandIndex);
                ModItemCrossStaff.AddToShop(info.shopPrefab.transform, parentIslandIndex);
                ModItemAstrolabe.AddToShop(info.shopPrefab.transform, parentIslandIndex);
                ModItemNocturnal.AddToShop(info.shopPrefab.transform, parentIslandIndex);
                Watchmaker.AddToShop(info.shopPrefab.transform, parentIslandIndex);
            }
        }

    }


    //[HarmonyPatch(typeof(PlayerCrouching))]
    //internal static class DebugSpawnPatch
    //{
    //    [HarmonyPatch("Update")]
    //    [HarmonyPostfix]
    //    public static void UpdatePatch()
    //    {
    //        if (Input.GetKey((KeyCode)308))
    //        {
    //            if (Input.GetKeyDown((KeyCode)257))
    //            {
    //                DebugSpawnPatch.SpawnItem(701, Refs.ovrCameraRig.position + Refs.ovrCameraRig.forward, Quaternion.identity);
    //                return;
    //            }
    //            if (Input.GetKeyDown((KeyCode)258))
    //            {
    //                DebugSpawnPatch.SpawnItem(702, Refs.ovrCameraRig.position + Refs.ovrCameraRig.forward, Quaternion.identity);
    //                return;
    //            }
    //        }
    //    }

    //    public static void SpawnItem(int itemIndex, Vector3 position, Quaternion rotation)
    //    {
    //        if (SaveLoadManager.instance.GetComponent<PrefabsDirectory>().directory != null)
    //        {
    //            GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(SaveLoadManager.instance.GetComponent<PrefabsDirectory>().directory[itemIndex], position, rotation);
    //            gameObject.GetComponent<ShipItem>().sold = true;
    //            gameObject.GetComponent<SaveablePrefab>().RegisterToSave();
    //        }
    //    }
    //}
}
