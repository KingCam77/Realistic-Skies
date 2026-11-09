using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The maker's number engraved on each chronometer, as on real ones ("No. 1736").
    ///  - Box chronometer (ShipItemClock): on the dial, between the centre and VI, in the dial
    ///    numerals' own material and brass colour. The dial's plane and size come from the numerals'
    ///    mesh, and which way XII lies from the hour hand at the time it shows, so it fits all three
    ///    models without measuring each.
    ///  - Chrono-compass: on the bottom plate (rim_002, flat at y = -0.0399 in all three models,
    ///    measured from the game's meshes), read from below with the far edge up: held, the item's +Z
    ///    points the way the player looks (GoPointer), so looking up at it the top of the view is -Z.
    /// The letters are a mesh of our own over a snapshot of the font atlas (white, alpha = ink), drawn
    /// with a lit Standard cutout material, so they take the scene's light like the dial does; a
    /// TextMesh's font material is unlit and glowed.
    /// </summary>
    internal class ChronoEngraving : MonoBehaviour
    {
        private const int FontSize = 64;
        private const string Charset = "No.0123456789 ";
        private static readonly Color Brass = new Color(156f / 255f, 109f / 255f, 16f / 255f);    // the dial numerals' palette colour

        private int serial = -1, built = -1;
        private GameObject plate;
        private Transform layerFrom;

        private static Font font;
        private static Texture2D atlas;
        private static int atlasVersion;
        private static float em = FontSize;            // glyph pixels per em
        private static bool reported, placedOnce, meshFailed;
        private static readonly Dictionary<char, CharacterInfo> glyphs = new Dictionary<char, CharacterInfo>();
        private static Material compassMat;

        internal static string Text(int serial) => "No. " + serial;

        private static ChronoEngraving On(Component item)
        {
            ChronoEngraving e = item.GetComponent<ChronoEngraving>();
            return e != null ? e : item.gameObject.AddComponent<ChronoEngraving>();
        }

        /// <summary>True when the engraving is up to date (then only its layer is kept).</summary>
        private bool Fresh(int s)
        {
            if (serial == s && built == atlasVersion && (meshFailed || atlas != null))
            {
                KeepLayer();
                return true;
            }
            if (plate != null)
            {
                MeshFilter mf = plate.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
                Destroy(plate);
            }
            plate = null;
            serial = s;
            return false;
        }

        private void KeepLayer()
        {
            if (plate == null || layerFrom == null) return;
            int l = layerFrom.gameObject.layer;
            if (plate.layer != l) plate.layer = l;
        }

        // ---------------- letters ----------------

        private static readonly AccessTools.FieldRef<LookUI, TextMesh> ControlsText = AccessTools.FieldRefAccess<LookUI, TextMesh>("controlsText");

        /// <summary>The game's own lettering: the font of its look hints ("pick up").</summary>
        private static bool EnsureFont()
        {
            if (font != null) return true;
            LookUI look = UnityEngine.Object.FindObjectOfType<LookUI>();
            TextMesh tm = look != null ? ControlsText(look) : null;
            if (tm != null) font = tm.font;
            if (font == null && Patches.itemPrefabs.TryGetValue(701, out GameObject almanac) && almanac != null)
                font = almanac.GetComponentInChildren<TextMesh>(true)?.font;
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font != null;
        }

        /// <summary>Lit letters ready? If they can't be made, engravings fall back to a plain TextMesh.</summary>
        private static bool Prepare()
        {
            if (meshFailed) return false;
            if (atlas != null && glyphs.Count > 0) return true;
            if (Snapshot()) return true;
            meshFailed = true;
            Plugin.Log.LogWarning("Chronometer engraving: lit letters could not be made; using plain text.");
            return false;
        }

        private static bool Snapshot()
        {
            if (!EnsureFont()) return false;

            // a dynamic font renders the glyphs at our size; a fixed one (the Almanac's may be) only
            // answers at its own size (0 = default)
            glyphs.Clear();
            em = FontSize;
            if (font.dynamic)
            {
                font.RequestCharactersInTexture(Charset, FontSize);
                foreach (char c in Charset)
                    if (font.GetCharacterInfo(c, out CharacterInfo ci, FontSize)) glyphs[c] = ci;
            }
            if (glyphs.Count == 0)
            {
                foreach (char c in Charset)
                    if (font.GetCharacterInfo(c, out CharacterInfo ci)) glyphs[c] = ci;
                em = font.fontSize > 0 ? font.fontSize : 0f;
                if (em <= 0f) foreach (CharacterInfo ci in glyphs.Values) em = Mathf.Max(em, ci.maxY - ci.minY);
            }
            Texture src = font.material != null ? font.material.mainTexture : null;
            if (!reported)
            {
                reported = true;
                Plugin.Log.LogInfo("Chronometer engraving: font " + font.name + (font.dynamic ? " (dynamic)" : " (fixed)") + ", " + glyphs.Count + " glyphs, em " + em +
                                   ", atlas " + (src != null ? src.width + "x" + src.height + " " + (src is Texture2D st ? st.format.ToString() : src.GetType().Name) : "none"));
            }
            if (src == null || glyphs.Count == 0 || em <= 0f) return false;

            // the atlas as it is now, copied to the CPU and made white with the glyphs in alpha
            int w = src.width, h = src.height;
            Color32[] px;
            if (src is Texture2D t2 && t2.isReadable)
            {
                px = t2.GetPixels32();
            }
            else
            {
                RenderTexture tmp = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                RenderTexture prev = RenderTexture.active;
                Graphics.Blit(src, tmp);                // plain copy, no blending
                RenderTexture.active = tmp;
                var read = new Texture2D(w, h, TextureFormat.RGBA32, false);
                read.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                read.Apply(false);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(tmp);
                px = read.GetPixels32();
                Destroy(read);
            }
            int inA = 0, inR = 0;
            foreach (Color32 c in px) { if (c.a > 128) inA++; if (c.r > 128) inR++; }
            // an Alpha8 font texture may come through in alpha or in red; ink is the rarer one
            bool useAlpha = inA > 0 && (inR == 0 || inA <= inR || inR == px.Length);
            int ink = useAlpha ? inA : inR;
            Plugin.Log.LogInfo("Chronometer engraving: atlas copy " + w + "x" + h + ", ink in " + (useAlpha ? "alpha" : "red") + " (" + inA + " / " + inR + " px)");
            if (ink == 0 || ink == px.Length) return false;
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, useAlpha ? px[i].a : px[i].r);
            if (atlas != null) Destroy(atlas);
            atlas = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = "RealisticSkies engraving letters",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 4,
            };
            atlas.SetPixels32(px);
            atlas.Apply(true, true);
            atlasVersion++;
            if (clockMat != null) clockMat.mainTexture = atlas;
            if (compassMat != null) compassMat.mainTexture = atlas;
            return true;
        }

        /// <summary>A flat line of letters centred on the origin in XY, h = em height; seen from -Z.</summary>
        private static Mesh Letters(string s, float h)
        {
            float k = h / em, pen = 0f, y0 = float.MaxValue, y1 = float.MinValue;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            foreach (char c in s)
            {
                if (!glyphs.TryGetValue(c, out CharacterInfo ci)) { pen += 0.3f * em; continue; }
                if (ci.maxX > ci.minX && ci.maxY > ci.minY)
                {
                    int b = v.Count;
                    v.Add(new Vector3(pen + ci.minX, ci.minY)); uv.Add(ci.uvBottomLeft);
                    v.Add(new Vector3(pen + ci.minX, ci.maxY)); uv.Add(ci.uvTopLeft);
                    v.Add(new Vector3(pen + ci.maxX, ci.maxY)); uv.Add(ci.uvTopRight);
                    v.Add(new Vector3(pen + ci.maxX, ci.minY)); uv.Add(ci.uvBottomRight);
                    tri.Add(b); tri.Add(b + 1); tri.Add(b + 2);
                    tri.Add(b); tri.Add(b + 2); tri.Add(b + 3);
                    y0 = Mathf.Min(y0, ci.minY); y1 = Mathf.Max(y1, ci.maxY);
                }
                pen += ci.advance;
            }
            Vector3 shift = new Vector3(-pen / 2f, -(y0 + y1) / 2f, 0f);
            var n = new List<Vector3>();
            for (int i = 0; i < v.Count; i++) { v[i] = (v[i] + shift) * k; n.Add(Vector3.back); }
            var mesh = new Mesh { name = "engraving " + s };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Cutout(Material m)
        {
            m.SetFloat("_Mode", 1f);
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            m.SetInt("_ZWrite", 1);
            m.EnableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_NORMALMAP");
            m.DisableKeyword("_EMISSION");
            m.DisableKeyword("_METALLICGLOSSMAP");
            m.SetFloat("_Cutoff", 0.3f);                // the snapshot's alpha came out squared by the blend
            m.renderQueue = 2450;
            m.color = Brass;
            m.mainTexture = atlas;
            m.mainTextureScale = Vector2.one;
            m.mainTextureOffset = Vector2.zero;
        }

        /// <summary>The numerals' own material, with our letters and brass in it: lit just like them.</summary>
        private static Material clockMat;

        /// <summary>Like the numerals' paint (clock_paint: Standard, metallic 0, smoothness 0), made fresh.</summary>
        private static Material ClockMaterial(Renderer numerals)
        {
            if (clockMat != null) return clockMat;
            clockMat = new Material(Shader.Find("Standard")) { name = "RealisticSkies chronometer engraving" };
            clockMat.SetFloat("_Metallic", 0f);
            clockMat.SetFloat("_Glossiness", 0f);
            Cutout(clockMat);
            return clockMat;
        }

        private static Material CompassMaterial()
        {
            if (compassMat != null) return compassMat;
            compassMat = new Material(Shader.Find("Standard")) { name = "RealisticSkies chrono-compass engraving" };
            compassMat.SetFloat("_Metallic", 0.6f);     // cut into the plate: bright metal
            compassMat.SetFloat("_Glossiness", 0.45f);
            Cutout(compassMat);
            return compassMat;
        }

        /// <summary>The fallback: a TextMesh in the game's font, brass, unlit (as the font draws).</summary>
        private GameObject Plain(Transform parent, string text, float h, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("engraving");
            go.layer = layerFrom != null ? layerFrom.gameObject.layer : parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = rot;
            float scale = Mathf.Max(1e-4f, go.transform.lossyScale.x);
            var t = go.AddComponent<TextMesh>();
            t.font = font;
            float size = font.dynamic ? FontSize : (font.fontSize > 0 ? font.fontSize : FontSize);
            if (font.dynamic) t.fontSize = FontSize;
            t.text = text;
            t.characterSize = h * 10f / size / scale;      // a TextMesh draws 10 font pixels per unit at size 1
            t.anchor = TextAnchor.MiddleCenter;
            t.alignment = TextAlignment.Center;
            t.color = Brass;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = font.material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            built = atlasVersion;
            return go;
        }

        private GameObject Place(Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("engraving");
            go.layer = layerFrom != null ? layerFrom.gameObject.layer : parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = rot;
            Vector3 ls = go.transform.lossyScale;
            go.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, ls.x), 1f / Mathf.Max(1e-4f, ls.y), 1f / Mathf.Max(1e-4f, ls.z));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
            built = atlasVersion;
            if (!placedOnce)
            {
                placedOnce = true;
                Plugin.Log.LogInfo("Chronometer engraving: first one on " + parent.name + ", " + mesh.vertexCount / 4 + " letters, " + mesh.bounds.size.x.ToString("0.000") + " m long");
            }
            return go;
        }

        // ---------------- the box chronometer ----------------

        /// <summary>
        /// The dial of each model, in the item's space, measured from the game's meshes by casting rays
        /// through them: z of the dial's face (the first surface behind the hands) and the minute
        /// hand's length. E has no separate numerals mesh (they are part of its body).
        /// </summary>
        private const float DialLift = 0.0018f;

        private static bool DialOf(string model, out float dialZ, out float radius)
        {
            switch (model)
            {
                case "clock_A": dialZ = -0.1695f; radius = 0.152f; return true;
                case "clock_E": dialZ = -0.1839f; radius = 0.166f; return true;
                case "clock_M": dialZ = -0.0775f; radius = 0.132f; return true;
                default: dialZ = radius = 0f; return false;
            }
        }

        internal static void Clock(ShipItemClock clock, Transform hourHand)
        {
            if (hourHand == null || Sun.sun == null) return;
            ChronoState s = ChronoState.Of(clock);
            ChronoEngraving e = On(clock);
            if (e.Fresh(s.serial)) return;
            bool lit = Prepare();
            if (!lit && !EnsureFont()) return;
            e.layerFrom = hourHand;

            // which of the three models: the active one of clock_A / clock_E / clock_M (the prefab
            // carries all three); the hour hand sits on it
            Transform body = hourHand.parent;
            if (body == null || !DialOf(body.name, out float dialZ, out float radius))
            {
                e.built = atlasVersion;
                Plugin.Log.LogWarning("Chronometer engraving: unknown chronometer model " + (body != null ? body.name : "?") + " on " + clock.name);
                return;
            }

            // in the item's space: face towards -Z, XII up (+Y), hands on the axis; between the
            // centre and VI. Lifted 1.8 mm off the dial: far from the world's origin positions round
            // to a millimetre or two and a thinner gap lost the letters behind the dial. The hands'
            // faces stay in front (hour hand's face 3.4 mm above the dial in all three models).
            Transform tr = clock.transform;
            Vector3 pos = tr.TransformPoint(new Vector3(0f, -0.42f * radius, dialZ - DialLift));
            Quaternion rot = Quaternion.LookRotation(tr.forward, tr.up);
            float hgt = 0.11f * radius;
            e.plate = lit ? e.Place(tr, Letters(Text(s.serial), hgt), ClockMaterial(body.GetComponent<Renderer>()), pos, rot)
                          : e.Plain(tr, Text(s.serial), hgt, pos, rot);
        }

        // ---------------- the chrono-compass ----------------

        private const float BottomY = -0.0399f, BottomLift = 0.0015f, CompassH = 0.02f;

        internal static void Compass(ShipItemCompass compass)
        {
            ChronoState s = ChronoState.Of(compass);
            ChronoEngraving e = On(compass);
            if (e.Fresh(s.serial)) return;
            bool lit = Prepare();
            if (!lit && !EnsureFont()) return;
            e.layerFrom = compass.transform;

            bool known = false;
            foreach (Transform c in compass.transform)
                if (c.gameObject.activeSelf && c.name.StartsWith("chronometer complex ")) { known = true; break; }
            if (!known) { e.built = atlasVersion; Plugin.Log.LogWarning("Chronometer engraving: unknown chrono-compass model " + compass.name); return; }

            Transform tr = compass.transform;
            Vector3 pos = tr.TransformPoint(new Vector3(0f, BottomY - BottomLift, 0f));
            // seen from below: the viewer looks along +Y, the top of the view towards -Z
            Quaternion rot = Quaternion.LookRotation(tr.up, -tr.forward);
            e.plate = lit ? e.Place(tr, Letters(Text(s.serial), CompassH), CompassMaterial(), pos, rot)
                          : e.Plain(tr, Text(s.serial), CompassH, pos, rot);
        }
    }

    [HarmonyPatch]
    internal static class ChronoEngravingPatches
    {
        [HarmonyPatch(typeof(ShipItemClock), "ExtraLateUpdate")]
        [HarmonyPostfix]
        private static void Clock(ShipItemClock __instance, Transform ___hourHand)
        {
            try { ChronoEngraving.Clock(__instance, ___hourHand); }
            catch (Exception e) { Fail(e); }
        }

        [HarmonyPatch(typeof(ShipItemCompass), "ExtraLateUpdate")]
        [HarmonyPostfix]
        private static void Compass(ShipItemCompass __instance)
        {
            if (__instance.chronoLatitude == null) return;
            try { ChronoEngraving.Compass(__instance); }
            catch (Exception e) { Fail(e); }
        }

        private static bool failed;
        private static void Fail(Exception e)
        {
            if (failed) return;
            failed = true;
            Plugin.Log.LogWarning("Chronometer engraving failed: " + e);
        }
    }
}
