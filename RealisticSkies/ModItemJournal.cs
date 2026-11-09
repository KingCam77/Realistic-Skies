using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The navigator's journal (prefab 711): a leather-bound book. Carry it (in hand or in a pocket)
    /// and quintant sights are written into it; open it with the Journal Key (J) or right click with
    /// it in hand. The pages themselves are <see cref="NavJournal"/>.
    /// The book is built in code: a page block between two board covers and a spine, a red ribbon
    /// marker, and a cover painted in code (<see cref="JournalCover"/>: worn leather, a gilt compass star).
    /// </summary>
    public class ModItemJournal : ShipItem
    {
        public const int PrefabIndex = 711;
        private const int SourceIndex = 91;
        private const string ItemName = "navigator's journal";

        // size, metres: x across, y thickness, z along the spine
        private const float BW = 0.20f, BH = 0.28f, BT = 0.04f, Board = 0.005f, Spine = 0.008f, Inset = 0.005f;

        public override void OnLoad()
        {
            base.name = ItemName;
        }

        /// <summary>Right click / R: open it (in a shop: buy it).</summary>
        public override void OnAltActivate()
        {
            if (!base.sold)
            {
                base.OnAltActivate();
                return;
            }
            NavJournal.Toggle();
        }

        /// <summary>With the journal in hand, a click on a traverse board copies it in (ModItemTraverseBoard.OnItemClick).</summary>
        public override bool AllowOnItemClick(GoPointerButton lookedAtButton)
        {
            return (base.sold && lookedAtButton.GetComponent<ModItemTraverseBoard>() != null) || base.AllowOnItemClick(lookedAtButton);
        }

        // ---------------- model ----------------

        /// <summary>
        /// An axis-aligned box. The top face (+y) is mapped onto 'top' of the cover texture; every
        /// other face onto 'rest' (a bit of plain, rubbed leather at the board's edge).
        /// </summary>
        private static void Box(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tri, Vector3 min, Vector3 max, Rect top, Rect rest)
        {
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            Vector3 c = (min + max) / 2f, half = (max - min) / 2f;
            foreach (Vector3 nn in normals)
            {
                Vector3 a = nn.x != 0f ? Vector3.up : Vector3.right;
                Vector3 b = Vector3.Cross(nn, a);
                b = new Vector3(Mathf.Abs(b.x), Mathf.Abs(b.y), Mathf.Abs(b.z));
                Vector3 center = c + Vector3.Scale(nn, half);
                Vector3 ea = Vector3.Scale(a, half), eb = Vector3.Scale(b, half);
                int i0 = v.Count;
                Vector3[] corners = { center - ea - eb, center + ea - eb, center + ea + eb, center - ea + eb };
                Rect rc = nn.y > 0f ? top : rest;
                foreach (Vector3 p in corners)
                {
                    v.Add(p); n.Add(nn);
                    // the top face: u across the board (x), v from foot to head (z)
                    float u = nn.y > 0f ? Mathf.InverseLerp(min.x, max.x, p.x) : (p == corners[1] || p == corners[2] ? 1f : 0f);
                    float w = nn.y > 0f ? Mathf.InverseLerp(min.z, max.z, p.z) : (p == corners[2] || p == corners[3] ? 1f : 0f);
                    uv.Add(new Vector2(rc.x + u * rc.width, rc.y + w * rc.height));
                }
                // Unity's front face: Cross(v1 - v0, v2 - v0) along the outward normal
                bool ok = Vector3.Dot(Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]), nn) > 0f;
                if (ok) tri.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                else tri.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            }
        }

        private static Mesh BuildBook()
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            var leather = new List<int>(); var paper = new List<int>(); var ribbon = new List<int>();
            float x0 = -BW / 2f, x1 = BW / 2f, z0 = -BH / 2f, z1 = BH / 2f, y0 = -BT / 2f, y1 = BT / 2f;
            var cover = new Rect(0f, 0f, 1f, 1f);
            var plain = new Rect(0.004f, 0.25f, 0.02f, 0.5f);         // the rubbed edge of the leather, no gilding
            var none = new Rect(0f, 0f, 0f, 0f);
            // the boards meet the spine edge to edge: no two faces in the same place (they flickered)
            Box(v, n, uv, leather, new Vector3(x0 + Spine, y1 - Board, z0), new Vector3(x1, y1, z1), cover, plain);   // front board
            Box(v, n, uv, leather, new Vector3(x0 + Spine, y0, z0), new Vector3(x1, y0 + Board, z1), plain, plain);   // back board
            Box(v, n, uv, leather, new Vector3(x0, y0, z0 - 0.0005f), new Vector3(x0 + Spine, y1, z1 + 0.0005f), plain, plain);   // spine
            Box(v, n, uv, paper, new Vector3(x0 + Spine, y0 + Board, z0 + Inset),
                new Vector3(x1 - Inset, y1 - Board, z1 - Inset), none, none);                                   // pages
            // the ribbon marker, out of the foot of the pages and lying on the table
            float rx = x0 + BW * 0.62f, ry = y0 + Board + 0.012f;
            Box(v, n, uv, ribbon, new Vector3(rx, ry, z0 - 0.002f), new Vector3(rx + 0.009f, ry + 0.0008f, z0 + 0.02f), none, none);
            Box(v, n, uv, ribbon, new Vector3(rx + 0.001f, y0, z0 - 0.045f), new Vector3(rx + 0.010f, y0 + 0.0008f, z0 - 0.0028f), none, none);
            Box(v, n, uv, ribbon, new Vector3(rx + 0.0005f, y0, z0 - 0.0035f), new Vector3(rx + 0.0095f, ry + 0.0008f, z0 - 0.002f), none, none);
            var mesh = new Mesh { name = "navigator's journal" };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(leather, 0);
            mesh.SetTriangles(paper, 1);
            mesh.SetTriangles(ribbon, 2);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D CoverTexture()
        {
            // the painting is the top level only; Apply builds the smaller ones
            byte[] b = JournalCover.Paint();
            var px = new Color32[JournalCover.W * JournalCover.H];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(b[i * 4], b[i * 4 + 1], b[i * 4 + 2], b[i * 4 + 3]);
            var t = new Texture2D(JournalCover.W, JournalCover.H, TextureFormat.RGBA32, true) { name = "journal cover", anisoLevel = 4 };
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        // ---------------- shops ----------------

        /// <summary>Lays the journal on the stall beside the Star Chart.</summary>
        internal static void AddToShop(Transform shop, int island)
        {
            if (shop == null) return;
            foreach (ShopItemSpawner sp in shop.GetComponentsInChildren<ShopItemSpawner>(true))
            {
                SaveablePrefab sv = sp.itemPrefab != null ? sp.itemPrefab.GetComponent<SaveablePrefab>() : null;
                if (sv == null || sv.prefabIndex != 703) continue;
                GameObject clone = UnityEngine.Object.Instantiate(sp.gameObject, sp.transform.parent);
                clone.name = ItemName;
                if (ShopSpots.TryGet(ShopSpots.Journal, island, out ShopSpots.Spot spot))
                {
                    ShopSpots.Apply(clone.transform, shop, spot);
                }
                else
                {
                    float side = ShopSpots.TryGet(ShopSpots.Scroll, island, out _) ? -1f : 1f;
                    clone.transform.localPosition = sp.transform.localPosition + new Vector3(side * 0.22f, 0.02f, 0.22f);
                    clone.transform.rotation = shop.rotation;
                }
                ShopItemSpawner csp = clone.GetComponent<ShopItemSpawner>();
                AccessTools.Field(typeof(ShopItemSpawner), "item").SetValue(csp, null);
                csp.enabled = false;
                Renderer r = clone.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
                var hook = clone.AddComponent<LoreSpawnerHook>();
                hook.spawner = csp;
                hook.prefabIndex = PrefabIndex;
                ShopSpots.RegisterSpawner(ShopSpots.Journal, island, clone.transform);
                break;
            }
        }

        // ---------------- prefab ----------------

        private static GameObject holder;

        /// <summary>Builds prefab 711 from the vanilla scroll and puts it in the prefab directory.</summary>
        internal static void Register(PrefabsDirectory dir)
        {
            if (holder != null || dir == null || dir.directory == null) return;
            if (dir.directory.Length <= SourceIndex || dir.directory[SourceIndex] == null)
            {
                Debug.LogWarning("RealisticSkies: vanilla scroll (prefab 91) not found, no journal");
                return;
            }
            if (dir.directory.Length > PrefabIndex && dir.directory[PrefabIndex] != null)
            {
                Debug.LogWarning("RealisticSkies: prefab " + PrefabIndex + " is already taken, no journal");
                return;
            }
            try
            {
                holder = new GameObject("RealisticSkies journal holder");
                holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(holder);
                GameObject go = UnityEngine.Object.Instantiate(dir.directory[SourceIndex], holder.transform);
                go.name = PrefabIndex + " " + ItemName;

                ShipItemScroll old = go.GetComponent<ShipItemScroll>();
                ModItemJournal item = go.AddComponent<ModItemJournal>();
                ModItemLoreScroll.CopyShipItemFields(old, item);
                UnityEngine.Object.DestroyImmediate(old);
                for (int i = go.transform.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
                }
                go.transform.localScale = Vector3.one;
                BoxCollider box = go.GetComponent<BoxCollider>() ?? go.AddComponent<BoxCollider>();
                foreach (Collider c in go.GetComponents<Collider>())
                {
                    if (c != box) UnityEngine.Object.DestroyImmediate(c);
                }
                box.center = Vector3.zero;
                box.size = new Vector3(BW, BT, BH);         // the ribbon is left out: it lies flat

                Shader sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
                var leather = new Material(sh) { name = "journal leather", mainTexture = CoverTexture() };
                leather.SetFloat("_Glossiness", 0.28f);
                var paper = new Material(sh) { name = "journal pages", color = new Color(0.90f, 0.85f, 0.70f) };
                paper.SetFloat("_Glossiness", 0.05f);
                go.GetComponent<MeshFilter>().sharedMesh = BuildBook();
                var ribbon = new Material(sh) { name = "journal ribbon", color = new Color(0.62f, 0.07f, 0.06f) };
                ribbon.SetFloat("_Glossiness", 0.45f);
                go.GetComponent<MeshRenderer>().sharedMaterials = new[] { leather, paper, ribbon };

                item.name = ItemName;
                item.amount = 0f;
                item.health = 0f;
                item.holdDistance = 0.5f;
                item.inventoryRotationX = 0f;
                item.inventoryRotation = 0f;
                item.value = 120;
                if (Patches.itemPrefabs.TryGetValue(703, out GameObject chart) && chart != null
                    && chart.GetComponent<ShipItem>() is ShipItem chartItem && chartItem.value > 0)
                {
                    item.value = Mathf.RoundToInt(chartItem.value * 0.5f);     // about 130 crowns
                }
                item.mass = 0.5f;

                go.GetComponent<SaveablePrefab>().prefabIndex = PrefabIndex;
                if (dir.directory.Length <= PrefabIndex)
                {
                    Array.Resize(ref dir.directory, PrefabIndex + 5);
                }
                dir.directory[PrefabIndex] = go;
                Debug.Log("RealisticSkies: added the navigator's journal as prefab " + PrefabIndex);
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not build the journal: " + e);
            }
        }
    }
}
