using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Runtime.Remoting.Metadata.W3cXsd2001;


namespace RealisticSkies
{
    public class MatSetup
    {

        public static void SetMat(string texPath, GameObject obj)
        {
            obj.GetComponent<MeshRenderer>().material = Patches.starMat;
            Texture2D TexFile = (Patches.assets.LoadAsset(texPath) as Texture2D);

            Renderer objrender = obj.GetComponent<MeshRenderer>();
            objrender.material.SetTexture("_MainTex", TexFile);
            objrender.material.SetTexture("_EmissionMap", TexFile);
            objrender.material.renderQueue = 2800;
        }
        public static void SetAlpha(float alpha, GameObject obj)
        {
            Renderer objrender = obj.GetComponent<MeshRenderer>();

            Color white = Color.white;
            white.a = alpha;
            objrender.material.color = white;
            objrender.material.renderQueue = 2800;
        }

        public static void SetAlphaAndGrey(float alpha, float scale, float grey, GameObject obj)
        {
            Renderer objrender = obj.GetComponent<MeshRenderer>();

            Color white = Color.white;
            white.a = scale;
            objrender.material.SetColor("_EmissionColor", white);
            white.a = alpha * scale;
            objrender.material.color = white;
            objrender.material.renderQueue = 2800;
        }

    }
}

