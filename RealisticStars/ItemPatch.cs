using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;


namespace RealisticStars
{
    public class ItemPatch
    {
        public static void StartItemPatch()
        {
            ItemPatch.book = (Patches.assets.LoadAsset("Assets/Almanac.prefab") as GameObject);
            ItemPatch.almanac = UnityEngine.Object.Instantiate<GameObject>(ItemPatch.book, StarPatch.Skybox.transform);

        }

        public static GameObject book;
        public static GameObject almanac;
    }
}

