using System;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Pocket watch (prefab 710): the lunarian's deck watch. It reads to the minute and second, but
    /// it is no chronometer: every watch gains or loses at its own rate (a few minutes a day), with a
    /// little random wander on top, and it stops if not wound. Set it from a lunar distance, carry the
    /// time to the next sight with it, and set it again.
    ///
    /// Controls while held: scroll winds it (the crown); hold right click / R to bring it close, and
    /// while holding, scroll sets the minute hand (with Shift, the hour hand).
    ///
    /// Saved in the two floats the game keeps for any item:
    /// amount = hours the watch is ahead of the true time (its error);
    /// health = 100 x rate code (1..99, the watch's own rate) + hours of run left in the spring;
    /// a new watch has health -1 and gets its rate when first seen.
    ///
    /// Model: "Pocket Watch" by PierreB3D, Poly Haven (CC0), converted by docs/export_pocket_watch.py
    /// and built into the DLL.
    /// </summary>
    public class ModItemPocketWatch : ShipItem
    {
        public const int PrefabIndex = 710;
        private const int SourceIndex = 91;
        private const string ItemName = "pocket watch";
        private const float MaxRun = 36f;               // game hours on a full winding
        private const float WindPerNotch = 3f;          // game hours per scroll notch
        private const float RateSigma = 1.5f;           // minutes a day, spread of the watches' rates
        private const float RateMax = 4.5f;
        private const float WanderSigma = 0.5f;         // minutes per sqrt(day), random wander
        private const float HoldFar = 0.36f, HoldNear = 0.2f;
        private const float Scale = 1.5f;               // a big watch: the model is 5.8 cm across, this makes it near 9

        // state, read from and written back to amount/health
        private float error;            // hours ahead of the true time
        private float rate;             // minutes gained per day
        private float runLeft;          // hours left in the spring

        private Transform hour, minute;     // no seconds hand: at game speed it only flickers; the journal gives seconds
        private double lastTime = -1;
        private int lookCloseFrame = -100;

        /// <summary>The watch's time of day in hours (0..24), what the hands show (on a 12 hour dial).</summary>
        internal double ShownHours
        {
            get
            {
                double t = TrueHours() + this.error;
                t %= 24.0;
                return t < 0 ? t + 24.0 : t;
            }
        }

        internal bool Running => this.runLeft > 0f;

        // ---------------- life ----------------

        public override void OnLoad()
        {
            base.name = ItemName;
            this.hour = base.transform.Find("hour");
            this.minute = base.transform.Find("minute");
            this.lastTime = -1;
            this.Decode();
            this.ShowHands();
        }

        public override void OnAltActivate()
        {
            if (!base.sold) base.OnAltActivate();     // in a shop: buy it
        }

        /// <summary>Held right click: bring the watch up to the eye.</summary>
        public override void OnAltHeld()
        {
            if (base.sold && base.held != null) this.lookCloseFrame = Time.frameCount;
        }

        /// <summary>Scroll: wind it, or, while it is held up close, set the hands.</summary>
        public override void OnScroll(float input)
        {
            if (!base.sold || base.held == null || input == 0f) return;
            this.Decode();
            this.Advance();
            int notches = input > 0f ? 1 : -1;
            if (this.LookingClose)
            {
                bool hours = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                this.error += notches * (hours ? 1f : 1f / 60f);
                if (this.error > 12f) this.error -= 24f;
                if (this.error < -12f) this.error += 24f;
                UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.12f, 2.6f);
            }
            else if (notches > 0 && Mode.PocketWatches == Keeping.Dead)
            {
                NotificationUi.instance.ShowNotification("The watch has stopped for good: it won't wind.");
            }
            else if (notches > 0 && Mode.PocketWatches == Keeping.Perfect)
            {
                NotificationUi.instance.ShowNotification("The watch needs no winding.");
            }
            else if (notches > 0 && this.runLeft < MaxRun)
            {
                this.runLeft = Mathf.Min(MaxRun, this.runLeft + WindPerNotch);
                UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.15f, 2.2f);
                if (this.runLeft >= MaxRun) NotificationUi.instance.ShowNotification("The watch is fully wound.");
            }
            this.Encode();
            this.ShowHands();
        }

        private bool LookingClose => Time.frameCount - this.lookCloseFrame <= 2;

        public override void ExtraLateUpdate()
        {
            base.ExtraLateUpdate();
            if (base.held != null)
            {
                float target = this.LookingClose ? HoldNear : HoldFar;
                base.holdDistance = Mathf.Lerp(base.holdDistance, target, Time.deltaTime * 6f);
                base.heldRotationOffset = 0f;           // face to the eye
            }
            this.Decode();
            this.Advance();
            this.Encode();
            this.ShowHands();
        }

        // ---------------- time ----------------

        private static double TrueHours()
        {
            if (Sun.sun == null) return 0;
            return GameState.day * 24.0 + Sun.sun.globalTime;
        }

        private static float Gauss()
        {
            float u1 = Mathf.Max(1e-6f, UnityEngine.Random.value), u2 = UnityEngine.Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }

        /// <summary>Runs the watch up to now: its own rate and wander while the spring lasts, standing still after.</summary>
        private void Advance()
        {
            double now = TrueHours();
            double dt = (this.lastTime >= 0 && Sun.sun != null) ? now - this.lastTime : 0;
            if (Sun.sun != null) this.lastTime = now;
            if (dt <= 0) return;                            // (a watch in a shop is unwound: it stands)
            if (dt > 24 * 365) dt = 24 * 365;
            Keeping k = Mode.PocketWatches;
            if (k == Keeping.Perfect)
            {
                // the base game's way: always going, never off (what you set it to, it keeps)
                this.runLeft = MaxRun;
                return;
            }
            if (k == Keeping.Dead) this.runLeft = 0f;

            float run = Mathf.Min((float)dt, this.runLeft);
            if (run > 0f)
            {
                float days = run / 24f;
                this.error += (this.rate * days + WanderSigma * Mathf.Sqrt(days) * Gauss()) / 60f;
                this.runLeft -= run;
            }
            // stopped: the hands stand, so the watch falls behind the true time
            this.error -= (float)dt - run;
            this.error = Mathf.Repeat(this.error + 12f, 24f) - 12f;
        }

        /// <summary>Puts the hands right by a known error (hours fast): the navigator setting his watch.</summary>
        internal void Correct(double fastBy)
        {
            this.Decode();
            this.Advance();
            this.error = Mathf.Repeat(this.error - (float)fastBy + 12f, 24f) - 12f;
            this.Encode();
            this.ShowHands();
        }

        /// <summary>
        /// The watchmaker's work: set to the true time, wound, and regulated, so the watch keeps a
        /// rate of its own again but a much smaller one.
        /// </summary>
        internal void Regulate()
        {
            this.Decode();
            this.error = 0f;
            this.rate = Mathf.Clamp(Gauss() * RateSigma * 0.35f, -RateMax, RateMax);
            this.runLeft = MaxRun;
            this.Encode();
            this.lastTime = -1;
        }

        // ---------------- state in amount/health ----------------

        private void Decode()
        {
            float h = base.health;
            if (h < 0f)
            {
                // a new watch: its own rate, unwound, hands at twelve
                this.rate = Mathf.Clamp(Gauss() * RateSigma, -RateMax, RateMax);
                this.runLeft = 0f;
                double t = TrueHours() % 12.0;
                this.error = -(float)t;
                this.Encode();
                return;
            }
            int code = Mathf.Clamp(Mathf.FloorToInt(h / 100f), 1, 99);
            this.rate = code / 10f - 5f;
            this.runLeft = Mathf.Clamp(h - code * 100f, 0f, MaxRun);
            this.error = base.amount;
        }

        private void Encode()
        {
            int code = Mathf.Clamp(Mathf.RoundToInt((this.rate + 5f) * 10f), 1, 99);
            this.rate = code / 10f - 5f;
            base.health = code * 100f + this.runLeft;
            base.amount = this.error;
        }

        private void ShowHands()
        {
            if (this.hour == null) return;
            double t = this.ShownHours;
            // the dial faces the item's -z; clockwise as seen from the front is negative about z
            // the hour hand glides, the minute hand jumps a minute at a time (a game minute passes in
            // about two real seconds); the seconds hand is left off the model
            double minutes = Math.Floor(t * 60.0);
            float h = (float)((t % 12.0) * 30.0);
            float m = (float)((minutes % 60.0) * 6.0);
            this.hour.localRotation = Quaternion.Euler(0f, 0f, -h);
            this.minute.localRotation = Quaternion.Euler(0f, 0f, -m);
        }

        // ---------------- model ----------------

        private static Mesh[] meshes;           // body, hour, minute, second
        private static Material metal, crystal;

        private static bool LoadModel()
        {
            if (meshes != null) return true;
            byte[] bin = ModResources.Read("PocketWatch/watch.bin", out _);
            if (bin == null) return false;
            var parts = new System.Collections.Generic.Dictionary<string, Mesh>();
            using (var r = new BinaryReader(new MemoryStream(bin)))
            {
                if (new string(r.ReadChars(4)) != "PWAT") throw new Exception("bad watch.bin");
                int np = r.ReadInt32();
                for (int p = 0; p < np; p++)
                {
                    string name = new string(r.ReadChars(r.ReadInt32()));
                    int n = r.ReadInt32();
                    var v = new Vector3[n]; var nn = new Vector3[n]; var uv = new Vector2[n];
                    for (int i = 0; i < n; i++) v[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()) * Scale;
                    for (int i = 0; i < n; i++) nn[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    for (int i = 0; i < n; i++) uv[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    var mesh = new Mesh { name = "pocket watch " + name };
                    mesh.vertices = v; mesh.normals = nn; mesh.uv = uv;
                    int subs = r.ReadInt32();
                    var tris = new int[subs][];
                    for (int s = 0; s < subs; s++)
                    {
                        tris[s] = new int[r.ReadInt32()];
                        for (int i = 0; i < tris[s].Length; i++) tris[s][i] = r.ReadInt32();
                    }
                    // only the body has crystal (submesh 1)
                    int used = tris.Length > 1 && tris[1].Length > 0 ? 2 : 1;
                    mesh.subMeshCount = used;
                    for (int s = 0; s < used; s++) mesh.SetTriangles(tris[s], s);
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();
                    parts[name] = mesh;
                }
            }
            meshes = new[] { parts["body"], parts["hour"], parts["minute"], parts["second"] };

            Shader sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            metal = new Material(sh) { name = "pocket watch" };
            Texture2D alb = Tex("albedo.jpg", false), mg = Tex("metalgloss.png", true), nm = Tex("normal.png", true);
            if (alb != null) metal.SetTexture("_MainTex", alb);
            if (mg != null)
            {
                metal.SetTexture("_MetallicGlossMap", mg);
                metal.EnableKeyword("_METALLICGLOSSMAP");
                metal.SetFloat("_GlossMapScale", 1f);
            }
            if (nm != null)
            {
                metal.SetTexture("_BumpMap", nm);
                metal.EnableKeyword("_NORMALMAP");
                metal.SetFloat("_BumpScale", 1f);
            }

            crystal = new Material(sh) { name = "pocket watch crystal", color = new Color(1f, 1f, 1f, 0.1f) };
            crystal.SetFloat("_Glossiness", 0.96f);
            crystal.SetFloat("_Mode", 2f);
            crystal.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            crystal.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            crystal.SetInt("_ZWrite", 0);
            crystal.DisableKeyword("_ALPHATEST_ON");
            crystal.EnableKeyword("_ALPHABLEND_ON");
            crystal.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            crystal.renderQueue = 3000;
            return true;
        }

        private static Texture2D Tex(string file, bool linear)
        {
            Texture2D t = ModResources.Texture("PocketWatch/" + file, linear, out bool shared);
            if (t == null) return null;
            if (!shared)
            {
                t.name = "pocket watch " + file;
                t.Apply(false, true);       // no longer readable: frees the CPU copy
            }
            t.anisoLevel = 4;
            return t;
        }

        // ---------------- shops ----------------

        /// <summary>Lays the watch face up on the stall beside the Star Chart.</summary>
        internal static void AddToShop(Transform shop, int island)
        {
            if (shop == null) return;
            foreach (ShopItemSpawner sp in shop.GetComponentsInChildren<ShopItemSpawner>(true))
            {
                SaveablePrefab sv = sp.itemPrefab != null ? sp.itemPrefab.GetComponent<SaveablePrefab>() : null;
                if (sv == null || sv.prefabIndex != 703) continue;
                GameObject clone = UnityEngine.Object.Instantiate(sp.gameObject, sp.transform.parent);
                clone.name = ItemName;
                if (ShopSpots.TryGet(ShopSpots.PocketWatch, island, out ShopSpots.Spot spot))
                {
                    ShopSpots.Apply(clone.transform, shop, spot);
                }
                else
                {
                    float side = ShopSpots.TryGet(ShopSpots.Scroll, island, out _) ? -1f : 1f;
                    clone.transform.localPosition = sp.transform.localPosition + new Vector3(side * 0.22f, 0.012f, 0f);
                    clone.transform.rotation = shop.rotation * Quaternion.Euler(90f, 0f, 0f);    // face up
                }
                ShopItemSpawner csp = clone.GetComponent<ShopItemSpawner>();
                AccessTools.Field(typeof(ShopItemSpawner), "item").SetValue(csp, null);
                csp.enabled = false;
                Renderer r = clone.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
                var hook = clone.AddComponent<LoreSpawnerHook>();
                hook.spawner = csp;
                hook.prefabIndex = PrefabIndex;
                ShopSpots.RegisterSpawner(ShopSpots.PocketWatch, island, clone.transform);
                break;
            }
        }

        // ---------------- prefab ----------------

        private static GameObject holder;

        /// <summary>Builds prefab 710 from the vanilla scroll and puts it in the prefab directory.</summary>
        internal static void Register(PrefabsDirectory dir)
        {
            if (holder != null || dir == null || dir.directory == null) return;
            if (dir.directory.Length <= SourceIndex || dir.directory[SourceIndex] == null)
            {
                Debug.LogWarning("RealisticSkies: vanilla scroll (prefab 91) not found, no pocket watch");
                return;
            }
            if (dir.directory.Length > PrefabIndex && dir.directory[PrefabIndex] != null)
            {
                Debug.LogWarning("RealisticSkies: prefab " + PrefabIndex + " is already taken, no pocket watch");
                return;
            }
            try
            {
                if (!LoadModel())
                {
                    Debug.LogWarning("RealisticSkies: pocket watch model missing, no pocket watch");
                    return;
                }
                holder = new GameObject("RealisticSkies pocket watch holder");
                holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(holder);
                GameObject go = UnityEngine.Object.Instantiate(dir.directory[SourceIndex], holder.transform);
                go.name = PrefabIndex + " " + ItemName;

                ShipItemScroll old = go.GetComponent<ShipItemScroll>();
                ModItemPocketWatch item = go.AddComponent<ModItemPocketWatch>();
                ModItemLoreScroll.CopyShipItemFields(old, item);
                UnityEngine.Object.DestroyImmediate(old);

                for (int i = go.transform.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
                }
                // resize the scroll's own box (ShipItem will not let it go; see ModItemHourglass)
                go.transform.localScale = Vector3.one;
                BoxCollider box = go.GetComponent<BoxCollider>() ?? go.AddComponent<BoxCollider>();
                foreach (Collider c in go.GetComponents<Collider>())
                {
                    if (c != box) UnityEngine.Object.DestroyImmediate(c);
                }
                Bounds b = meshes[0].bounds;
                box.center = b.center;
                box.size = b.size;
                go.GetComponent<MeshFilter>().sharedMesh = meshes[0];
                go.GetComponent<MeshRenderer>().sharedMaterials = new[] { metal, crystal };

                string[] names = { "hour", "minute" };
                for (int k = 0; k < names.Length; k++)
                {
                    var hand = new GameObject(names[k]);
                    hand.transform.SetParent(go.transform, false);
                    hand.AddComponent<MeshFilter>().sharedMesh = meshes[k + 1];
                    MeshRenderer hr = hand.AddComponent<MeshRenderer>();
                    hr.sharedMaterial = metal;
                    hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // no moving shadows on the dial
                }

                item.name = ItemName;
                item.amount = 0f;
                item.health = -1f;              // gets its own rate when first seen
                item.holdDistance = HoldFar;
                item.holdHeight = 0f;
                item.inventoryRotationX = 0f;
                item.inventoryRotation = 180f;  // the dial to the player
                item.inventoryScale *= 4f;      // a small thing beside the scroll it is built from
                item.value = 220;
                if (Patches.itemPrefabs.TryGetValue(703, out GameObject chart) && chart != null
                    && chart.GetComponent<ShipItem>() is ShipItem chartItem && chartItem.value > 0)
                {
                    item.value = Mathf.RoundToInt(chartItem.value * 0.85f);     // about 220 crowns
                }
                item.mass = 0.15f;

                go.GetComponent<SaveablePrefab>().prefabIndex = PrefabIndex;
                if (dir.directory.Length <= PrefabIndex)
                {
                    Array.Resize(ref dir.directory, PrefabIndex + 5);
                }
                dir.directory[PrefabIndex] = go;
                Debug.Log("RealisticSkies: added the pocket watch as prefab " + PrefabIndex);
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not build the pocket watch: " + e);
            }
        }
    }
}
