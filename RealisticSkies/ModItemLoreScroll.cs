using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// "Sea Legends" (prefab 704): a readable scroll with the constellations, the sailors' star and
    /// planet names and the legends. Built at runtime from the vanilla scroll (prefab 91); the pages
    /// are built into the DLL (Lore/page01.png...), a Lore folder next to the plugin overrides them.
    /// </summary>
    public class ModItemLoreScroll : ShipItem
    {
        public const int PrefabIndex = 704;
        private const int SourceIndex = 91;
        private const string ItemName = "Sea Legends";

        // copied from the vanilla scroll when the prefab is built
        public Renderer page;
        public GameObject arrowUp;
        public GameObject arrowDown;
        public Mesh openMesh;
        public Mesh closedMesh;

        private MeshFilter filter;
        private int currentPage;
        private bool rolled;        // held, but rolled up with the alt button (right click / R)
        private bool pageWaiting;   // the page is still loading: show it as soon as it is ready

        public override void OnLoad()
        {
            this.filter = base.GetComponent<MeshFilter>();
            if (this.closedMesh == null && this.filter != null)
            {
                this.closedMesh = this.filter.sharedMesh;
            }
            base.name = ItemName;
            this.currentPage = Mathf.Clamp((int)base.amount, 0, Math.Max(0, PageCount - 1));
            this.ShowPage();
            if (this.page != null) this.page.enabled = false;
            this.HideArrows();
        }

        public override void OnScroll(float input)
        {
            if (this.rolled) return;
            if (input > 0f && this.currentPage > 0)
            {
                this.Flip(-1);
            }
            else if (input < 0f && this.currentPage < PageCount - 1)
            {
                this.Flip(1);
            }
        }

        public override void OnPickup()
        {
            base.OnPickup();
            this.Unroll();
            UISoundPlayer.instance.PlayOpenSound();
        }

        public override void OnDrop()
        {
            base.OnDrop();
            bool wasOpen = !this.rolled;
            this.RollUp();
            if (wasOpen) UISoundPlayer.instance.PlayCloseSound();
        }

        /// <summary>Right click / R while holding it rolls the scroll up or opens it again, like a map.</summary>
        public override void OnAltActivate()
        {
            if (!base.sold || base.held == null)
            {
                base.OnAltActivate();       // in a shop this is how it gets bought
                return;
            }
            if (this.rolled)
            {
                this.Unroll();
                UISoundPlayer.instance.PlayOpenSound();
            }
            else
            {
                this.RollUp();
                UISoundPlayer.instance.PlayCloseSound();
            }
        }

        private void Unroll()
        {
            this.rolled = false;
            if (this.page != null) this.page.enabled = true;
            if (this.filter != null && this.openMesh != null) this.filter.sharedMesh = this.openMesh;
            this.ShowPage();
            this.UpdateArrows();
        }

        private void RollUp()
        {
            this.rolled = true;
            if (this.page != null) this.page.enabled = false;
            if (this.filter != null && this.closedMesh != null) this.filter.sharedMesh = this.closedMesh;
            this.HideArrows();
        }

        public override void ExtraLateUpdate()
        {
            base.ExtraLateUpdate();
            if (this.pageWaiting) this.ShowPage();
        }

        private void Flip(int dir)
        {
            this.currentPage += dir;
            base.amount = this.currentPage;
            this.ShowPage();
            this.UpdateArrows();
            UISoundPlayer.instance.PlayParchmentSound();
        }

        private void ShowPage()
        {
            Texture2D tex = LorePages.Get(this.currentPage);
            this.pageWaiting = tex == null && this.currentPage < PageCount;
            if (tex != null && this.page != null && this.page.material.mainTexture != tex)
            {
                this.page.material.mainTexture = tex;
            }
            // fetch the neighbours ahead, so the next flip is instant
            LorePages.Request(this.currentPage + 1);
            LorePages.Request(this.currentPage - 1);
        }

        private void UpdateArrows()
        {
            if (this.arrowDown != null) this.arrowDown.SetActive(this.currentPage < PageCount - 1);
            if (this.arrowUp != null) this.arrowUp.SetActive(this.currentPage > 0);
        }

        private void HideArrows()
        {
            if (this.arrowDown != null) this.arrowDown.SetActive(false);
            if (this.arrowUp != null) this.arrowUp.SetActive(false);
        }

        // ---------------- pages ----------------

        private static int PageCount => LorePages.Count;

        // ---------------- prefab ----------------

        private static GameObject holder;
        private static GameObject prefab;

        /// <summary>Builds prefab 704 from the vanilla scroll and puts it in the prefab directory.</summary>
        internal static void Register(PrefabsDirectory dir)
        {
            if (prefab != null || dir == null || dir.directory == null) return;
            if (dir.directory.Length <= SourceIndex || dir.directory[SourceIndex] == null)
            {
                Debug.LogWarning("RealisticSkies: vanilla scroll (prefab 91) not found, no Sea Legends scroll");
                return;
            }
            if (dir.directory.Length > PrefabIndex && dir.directory[PrefabIndex] != null)
            {
                Debug.LogWarning("RealisticSkies: prefab 704 is already taken, no Sea Legends scroll");
                return;
            }
            try
            {
                // an inactive parent keeps the template from waking up; copies made from it are active
                holder = new GameObject("RealisticSkies prefab holder");
                holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(holder);
                GameObject go = UnityEngine.Object.Instantiate(dir.directory[SourceIndex], holder.transform);
                go.name = "704 sea legends";

                ShipItemScroll old = go.GetComponent<ShipItemScroll>();
                ModItemLoreScroll item = go.AddComponent<ModItemLoreScroll>();
                CopyShipItemFields(old, item);
                item.page = (Renderer)AccessTools.Field(typeof(ShipItemScroll), "page").GetValue(old);
                item.arrowUp = (GameObject)AccessTools.Field(typeof(ShipItemScroll), "arrowUp").GetValue(old);
                item.arrowDown = (GameObject)AccessTools.Field(typeof(ShipItemScroll), "arrowDown").GetValue(old);
                item.openMesh = (Mesh)AccessTools.Field(typeof(ShipItemScroll), "openMesh").GetValue(old);
                UnityEngine.Object.DestroyImmediate(old);
                item.name = ItemName;
                // a little dearer than the Star Chart (more paper, and the pictures), about 280 crowns
                item.value = 250;
                if (Patches.itemPrefabs.TryGetValue(703, out GameObject chart) && chart != null
                    && chart.GetComponent<ShipItem>() is ShipItem chartItem && chartItem.value > 0)
                {
                    item.value = Mathf.RoundToInt(chartItem.value * 1.1f);
                }
                item.amount = 0f;

                go.GetComponent<SaveablePrefab>().prefabIndex = PrefabIndex;

                if (dir.directory.Length <= PrefabIndex)
                {
                    Array.Resize(ref dir.directory, PrefabIndex + 5);
                }
                dir.directory[PrefabIndex] = go;
                prefab = go;
                Debug.Log("RealisticSkies: added the Sea Legends scroll as prefab 704");
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not build the Sea Legends scroll: " + e);
            }
        }

        private static void CopyShipItemFields(ShipItem from, ShipItem to)
        {
            for (Type t = typeof(ShipItem); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    f.SetValue(to, f.GetValue(from));
                }
            }
        }

        // ---------------- shops ----------------

        /// <summary>
        /// Puts the scroll in one of the mod's shops: on its own spot if one is set for this island
        /// (see <see cref="LoreShopSpots"/>), else on the stall next to the Star Chart.
        /// </summary>
        internal static void AddToShop(Transform shop, int island)
        {
            if (shop == null) return;
            bool hasSpot = LoreShopSpots.TryGet(island, out LoreShopSpots.Spot spot);
            ShopItemSpawner source = null;
            foreach (ShopItemSpawner sp in shop.GetComponentsInChildren<ShopItemSpawner>(true))
            {
                SaveablePrefab sv = sp.itemPrefab != null ? sp.itemPrefab.GetComponent<SaveablePrefab>() : null;
                if (sv != null && sv.prefabIndex == 703) { source = sp; break; }
                if (source == null && hasSpot) source = sp;    // any stall will do as a template
            }
            if (source == null) return;

            GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent);
            clone.name = "sea legends";
            if (hasSpot)
            {
                LoreShopSpots.Apply(clone.transform, shop, spot);
            }
            else
            {
                clone.transform.localPosition = source.transform.localPosition + new Vector3(-0.4f, 0.02f, 0f);
                clone.transform.rotation = shop.rotation * Quaternion.Euler(-90f, 0f, 0f);
            }
            ShopItemSpawner csp = clone.GetComponent<ShopItemSpawner>();
            AccessTools.Field(typeof(ShopItemSpawner), "item").SetValue(csp, null);
            csp.enabled = false;
            Renderer r = clone.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            clone.AddComponent<LoreSpawnerHook>().spawner = csp;
        }
    }

    /// <summary>Hands the shop spawner the scroll prefab once the prefab directory has it.</summary>
    public class LoreSpawnerHook : MonoBehaviour
    {
        public ShopItemSpawner spawner;

        private void Update()
        {
            PrefabsDirectory dir = PrefabsDirectory.instance;
            if (spawner == null || dir == null || dir.directory == null) return;
            if (dir.directory.Length > ModItemLoreScroll.PrefabIndex && dir.directory[ModItemLoreScroll.PrefabIndex] != null)
            {
                spawner.itemPrefab = dir.directory[ModItemLoreScroll.PrefabIndex];
                spawner.enabled = true;
                Destroy(this);
            }
        }
    }
}
