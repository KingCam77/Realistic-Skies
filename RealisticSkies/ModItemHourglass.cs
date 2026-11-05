using System;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Sand glasses: the half-hour glass (prefab 707) and the bigger hour glass (708), ship's watch
    /// glasses for carrying the time from a lunar distance to the next sight. Turn one (right click / R
    /// while holding it) and the sand runs for half an hour or an hour of game time; a ship's bell
    /// strikes when the upper bulb is empty. Both are the same model, the hour glass scaled up.
    ///
    /// The sand follows gravity: it runs into whichever bulb is lower, so a glass put down upside
    /// down keeps running and one lying on its side stops. The share of sand in the bulb at the
    /// model's +y end is saved in <c>amount</c>; the item's own rotation is saved by the game.
    /// Model and materials are built in code (<see cref="HourglassMesh"/>), the item itself is
    /// built at runtime from the vanilla scroll like the Sea Legends scroll.
    /// </summary>
    public class ModItemHourglass : ShipItem
    {
        private const int SourceIndex = 91;
        private const float FlipSeconds = 0.35f;         // real seconds for the turn in the hand

        /// <summary>The two glasses: prefab, name, run time, size, price against the Star Chart.</summary>
        private struct Variant
        {
            public int index;
            public string kind, name;
            public float hours, scale, price, mass;
        }

        private static readonly Variant[] Variants =
        {
            new Variant { index = 707, kind = ShopSpots.Glass30, name = "half-hour glass", hours = 0.5f, scale = 1f, price = 0.4f, mass = 0.6f },
            new Variant { index = 708, kind = ShopSpots.Glass60, name = "hour glass", hours = 1f, scale = 1.3f, price = 0.6f, mass = 0.9f },
        };

        // set per prefab (serialized, so copies and loaded items keep them)
        public string kind = ShopSpots.Glass30;
        public string itemName = "half-hour glass";
        public float runHours = 0.5f;                   // game hours for a full run
        public float scale = 1f;                        // model size against the half-hour glass

        private Mesh sandMesh;
        private double lastTime = -1;
        private float shownUpper = -1f;
        private bool shownMirror, shownRunning;
        private bool wasRunning;
        private float flipLeft;                         // degrees of the turn still to go
        private AudioSource hiss, bellSource;
        private int loadFrame;

        // ---------------- life ----------------

        public override void OnLoad()
        {
            base.name = this.itemName;
            base.amount = Mathf.Clamp01(base.amount);
            Transform t = base.transform.Find("sand");
            if (t != null)
            {
                this.sandMesh = new Mesh { name = "hourglass sand" };
                this.sandMesh.MarkDynamic();
                t.GetComponent<MeshFilter>().sharedMesh = this.sandMesh;
            }
            this.lastTime = -1;
            this.loadFrame = Time.frameCount;
            this.hiss = MakeSource(base.gameObject, SandClip, true, 0.12f, 3f);      // the sand a third of what it was: players found it loud
            // the bigger glass sounds a little deeper
            if (this.hiss != null) this.hiss.pitch = UnityEngine.Random.Range(0.95f, 1.05f) / Mathf.Sqrt(this.scale);
            this.bellSource = MakeSource(base.gameObject, Bell, false, 0.9f, 30f);
            this.UpdateSand(true);
        }

        public override void OnPickup()
        {
            bool fromPocket = this.InPocket;   // base.OnPickup takes it out of the slot
            base.OnPickup();
            // keep it the way up it was standing (out of the pocket: upright, see OnEnterInventory)
            base.heldRotationOffset = !fromPocket && Vector3.Dot(base.transform.up, Vector3.up) < 0f ? 180f : 0f;
            this.flipLeft = 0f;
        }

        private bool InPocket => base.itemRigidbodyC != null && base.itemRigidbodyC.GetCurrentInventorySlot() != null;

        /// <summary>
        /// Into the pocket: the glass keeps running the way it was held. In a slot it is turned to face
        /// the camera, so its own up means nothing there: the sand is swapped end for end (if it was
        /// upside down) so that the +y bulb is the upper one, and in the pocket it always runs from +y.
        /// Taken out again it comes upright, with the same sand on top. A glass put back into a slot
        /// on loading is already stored that way.
        /// </summary>
        public override void OnEnterInventory()
        {
            base.OnEnterInventory();
            this.UpdateSand(false);         // count the time up to now the old way
            bool loading = Time.frameCount - this.loadFrame < 30;
            if (!loading && Vector3.Dot(base.transform.up, Vector3.up) < 0f)
            {
                base.amount = 1f - base.amount;
            }
            this.flipLeft = 0f;
            this.UpdateSand(true);
        }

        /// <summary>Right click / R while holding it turns the glass over.</summary>
        public override void OnAltActivate()
        {
            if (!base.sold || base.held == null)
            {
                base.OnAltActivate();       // in a shop this is how it gets bought
                return;
            }
            if (this.flipLeft <= 0f)
            {
                this.flipLeft = 180f;
                UISoundPlayer.instance.PlayParchmentSound();
            }
        }

        public override void ExtraLateUpdate()
        {
            base.ExtraLateUpdate();
            if (this.flipLeft > 0f)
            {
                float step = Mathf.Min(this.flipLeft, 180f * Time.deltaTime / FlipSeconds);
                this.flipLeft -= step;
                if (base.held != null) base.heldRotationOffset += step;
                else this.flipLeft = 0f;
            }
            this.UpdateSand(false);
        }

        // ---------------- sand ----------------

        private static double GameHours()
        {
            if (Sun.sun == null) return -1;
            return GameState.day * 24.0 + Sun.sun.globalTime;
        }

        private void UpdateSand(bool force)
        {
            double now = GameHours();
            double dt = (this.lastTime >= 0 && now >= 0) ? now - this.lastTime : 0;
            if (dt < 0 || dt > 48) dt = 0;              // clock set back, or a long jump on loading
            if (now >= 0) this.lastTime = now;

            // in the pocket: always upright (see OnEnterInventory)
            float up = this.InPocket ? 1f : Vector3.Dot(base.transform.up, Vector3.up);
            bool upright = up > 0f;
            bool flowing = base.sold && Mathf.Abs(up) > 0.25f;
            float a = base.amount;                      // share of the sand in the +y bulb
            float upper = upright ? a : 1f - a;
            bool running = flowing && upper > 0f;

            if (running && dt > 0)
            {
                float d = (float)(dt / Mathf.Max(0.01f, this.runHours));
                a = upright ? Mathf.Max(0f, a - d) : Mathf.Min(1f, a + d);
                base.amount = a;
                upper = upright ? a : 1f - a;
                running = upper > 0f;
                if (!running && this.wasRunning) this.Strike();
            }
            this.wasRunning = running;
            if (this.hiss != null)
            {
                // the sand whispers while it runs, quiet enough to hear only beside the glass
                if (running && !this.hiss.isPlaying) this.hiss.Play();
                else if (!running && this.hiss.isPlaying) this.hiss.Stop();
            }

            if (this.sandMesh == null) return;
            if (!flowing && !force && Mathf.Abs(up) <= 0.25f) return;     // on its side: leave the sand be
            if (force || upright == this.shownMirror || running != this.shownRunning || Mathf.Abs(upper - this.shownUpper) > 0.003f)
            {
                HourglassMesh.BuildSand(this.sandMesh, upper, running, !upright, this.scale);
                this.shownUpper = upper;
                this.shownMirror = !upright;
                this.shownRunning = running;
            }
        }

        private void Strike()
        {
            if (this.bellSource != null) this.bellSource.Play();
        }

        /// <summary>A 3D sound on the glass, routed like the game's own sounds so the volume settings apply.</summary>
        internal static AudioSource MakeSource(GameObject go, AudioClip clip, bool loop, float volume, float range)
        {
            if (clip == null) return null;
            AudioSource src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = loop;
            src.volume = volume;
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = Mathf.Min(1f, range / 4f);
            src.maxDistance = range;
            src.dopplerLevel = 0f;
            AudioSource ui = UISoundPlayer.instance != null ? UISoundPlayer.instance.GetComponentInChildren<AudioSource>() : null;
            if (ui != null) src.outputAudioMixerGroup = ui.outputAudioMixerGroup;
            return src;
        }

        // ---------------- shared assets ----------------

        private static readonly System.Collections.Generic.Dictionary<float, Mesh> bodies = new System.Collections.Generic.Dictionary<float, Mesh>();
        internal static Material wood, glass, sand;
        private static AudioClip bell, sandLoop;
        private static bool bellTried, sandTried;

        internal static AudioClip SandClip
        {
            get
            {
                if (sandLoop == null && !sandTried)
                {
                    sandTried = true;
                    sandLoop = LoadWav("Sounds/sand.wav", "running sand");
                }
                return sandLoop;
            }
        }

        internal static AudioClip Bell
        {
            get
            {
                if (bell == null && !bellTried)
                {
                    bellTried = true;
                    bell = LoadWav("Sounds/bell.wav", "ship's bell");
                }
                return bell;
            }
        }

        /// <summary>16-bit PCM WAV (built into the DLL) -> AudioClip, no decoder needed.</summary>
        private static AudioClip LoadWav(string rel, string name)
        {
            try
            {
                AudioClip fromBundle = ModResources.Audio(rel);
                if (fromBundle != null) return fromBundle;
                byte[] d = ModResources.Read(rel, out string source);
                if (d == null) { Debug.LogWarning("RealisticSkies: no " + rel); return null; }
                int channels = 1, rate = 44100, bits = 16, p = 12;
                while (p + 8 <= d.Length)
                {
                    string id = System.Text.Encoding.ASCII.GetString(d, p, 4);
                    int len = BitConverter.ToInt32(d, p + 4);
                    if (id == "fmt ")
                    {
                        channels = BitConverter.ToInt16(d, p + 10);
                        rate = BitConverter.ToInt32(d, p + 12);
                        bits = BitConverter.ToInt16(d, p + 22);
                    }
                    else if (id == "data")
                    {
                        if (bits != 16) throw new Exception("only 16-bit PCM");
                        int n = Math.Min(len, d.Length - p - 8) / 2;
                        var samples = new float[n];
                        for (int i = 0; i < n; i++) samples[i] = BitConverter.ToInt16(d, p + 8 + i * 2) / 32768f;
                        AudioClip clip = AudioClip.Create(name, n / channels, channels, rate, false);
                        clip.SetData(samples, 0);
                        return clip;
                    }
                    p += 8 + len + (len & 1);
                }
                throw new Exception("no data chunk");
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not load " + rel + ": " + e.Message);
                return null;
            }
        }

        private static Material Standard(Color c, float smooth)
        {
            Shader sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            var m = new Material(sh) { color = c };
            m.SetFloat("_Glossiness", smooth);
            return m;
        }

        internal static void MakeAssets()
        {
            if (wood != null) return;
            wood = Standard(new Color(0.40f, 0.25f, 0.14f), 0.35f);
            wood.name = "hourglass wood";
            sand = Standard(new Color(0.85f, 0.72f, 0.48f), 0.05f);
            sand.name = "hourglass sand";
            glass = Standard(new Color(0.88f, 0.95f, 0.92f, 0.18f), 0.95f);
            glass.name = "hourglass glass";
            // Standard shader in Fade mode
            glass.SetFloat("_Mode", 2f);
            glass.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glass.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glass.SetInt("_ZWrite", 0);
            glass.DisableKeyword("_ALPHATEST_ON");
            glass.EnableKeyword("_ALPHABLEND_ON");
            glass.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            glass.renderQueue = 3000;
        }

        private static Mesh Body(float scale)
        {
            if (!bodies.TryGetValue(scale, out Mesh m) || m == null)
            {
                m = HourglassMesh.BuildBody(scale);
                bodies[scale] = m;
            }
            return m;
        }

        // ---------------- shops ----------------

        /// <summary>
        /// Puts both glasses in the mod's shops: on a hand-placed spot if the shop has one
        /// (<see cref="ShopSpots"/>), else standing beside the Star Chart, the half-hour glass next to
        /// it and the hour glass a step further, on the side the scroll leaves free.
        /// </summary>
        internal static void AddToShop(Transform shop, int island)
        {
            if (shop == null) return;
            foreach (ShopItemSpawner sp in shop.GetComponentsInChildren<ShopItemSpawner>(true))
            {
                SaveablePrefab sv = sp.itemPrefab != null ? sp.itemPrefab.GetComponent<SaveablePrefab>() : null;
                if (sv == null || sv.prefabIndex != 703) continue;
                float side = ShopSpots.TryGet(ShopSpots.Scroll, island, out _) ? -1f : 1f;
                for (int v = 0; v < Variants.Length; v++)
                {
                    Variant var = Variants[v];
                    GameObject clone = UnityEngine.Object.Instantiate(sp.gameObject, sp.transform.parent);
                    clone.name = var.name;
                    if (ShopSpots.TryGet(var.kind, island, out ShopSpots.Spot spot))
                    {
                        ShopSpots.Apply(clone.transform, shop, spot);
                    }
                    else
                    {
                        clone.transform.localPosition = sp.transform.localPosition + new Vector3(side * (0.4f + 0.25f * v), 0.02f, 0f);
                        clone.transform.rotation = shop.rotation;           // standing upright
                        clone.transform.position += shop.up * HourglassMesh.Centre * var.scale;     // the origin is the glass's middle
                    }
                    ShopItemSpawner csp = clone.GetComponent<ShopItemSpawner>();
                    AccessTools.Field(typeof(ShopItemSpawner), "item").SetValue(csp, null);
                    csp.enabled = false;
                    Renderer r = clone.GetComponent<Renderer>();
                    if (r != null) r.enabled = false;
                    var hook = clone.AddComponent<LoreSpawnerHook>();
                    hook.spawner = csp;
                    hook.prefabIndex = var.index;
                    ShopSpots.RegisterSpawner(var.kind, island, clone.transform);
                }
                break;
            }
        }

        // ---------------- prefabs ----------------

        private static GameObject holder;

        /// <summary>Builds prefabs 707 and 708 from the vanilla scroll and puts them in the prefab directory.</summary>
        internal static void Register(PrefabsDirectory dir)
        {
            if (holder != null || dir == null || dir.directory == null) return;
            if (dir.directory.Length <= SourceIndex || dir.directory[SourceIndex] == null)
            {
                Debug.LogWarning("RealisticSkies: vanilla scroll (prefab 91) not found, no sand glasses");
                return;
            }
            MakeAssets();
            holder = new GameObject("RealisticSkies hourglass holder");
            holder.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(holder);
            foreach (Variant v in Variants) Build(dir, v);
        }

        private static void Build(PrefabsDirectory dir, Variant v)
        {
            if (dir.directory.Length > v.index && dir.directory[v.index] != null)
            {
                Debug.LogWarning("RealisticSkies: prefab " + v.index + " is already taken, no " + v.name);
                return;
            }
            try
            {
                GameObject go = UnityEngine.Object.Instantiate(dir.directory[SourceIndex], holder.transform);
                go.name = v.index + " " + v.name;

                ShipItemScroll old = go.GetComponent<ShipItemScroll>();
                ModItemHourglass item = go.AddComponent<ModItemHourglass>();
                ModItemLoreScroll.CopyShipItemFields(old, item);
                UnityEngine.Object.DestroyImmediate(old);
                item.kind = v.kind;
                item.itemName = v.name;
                item.runHours = v.hours;
                item.scale = v.scale;
                item.inventoryRotationX = 0f;   // stands upright in the pocket slot
                item.inventoryRotation = 0f;

                // the scroll's page, arrows and colliders go; the glass gets its own
                for (int i = go.transform.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
                }
                // The item's physics body copies the item's BoxCollider, and ShipItem will not let it be
                // removed: resize the scroll's own box instead of adding a second one (a stray
                // scroll-sized box on a rescaled item once sank a ship).
                go.transform.localScale = Vector3.one;
                BoxCollider box = go.GetComponent<BoxCollider>() ?? go.AddComponent<BoxCollider>();
                foreach (Collider c in go.GetComponents<Collider>())
                {
                    if (c != box) UnityEngine.Object.DestroyImmediate(c);
                }
                float k = v.scale;
                box.center = new Vector3(0f, (HourglassMesh.Height / 2f - HourglassMesh.Centre) * k, 0f);
                box.size = new Vector3(HourglassMesh.Radius * 2f * k, HourglassMesh.Height * k, HourglassMesh.Radius * 2f * k);
                go.GetComponent<MeshFilter>().sharedMesh = Body(k);
                go.GetComponent<MeshRenderer>().sharedMaterials = new[] { wood, glass };

                var sandGo = new GameObject("sand");
                sandGo.transform.SetParent(go.transform, false);
                sandGo.AddComponent<MeshFilter>();
                sandGo.AddComponent<MeshRenderer>().sharedMaterial = sand;

                item.name = v.name;
                item.amount = 0f;           // all the sand settled in the lower bulb
                item.value = Mathf.RoundToInt(250 * v.price);
                if (Patches.itemPrefabs.TryGetValue(703, out GameObject chart) && chart != null
                    && chart.GetComponent<ShipItem>() is ShipItem chartItem && chartItem.value > 0)
                {
                    item.value = Mathf.RoundToInt(chartItem.value * v.price);     // about 100 and 150 crowns
                }
                item.mass = v.mass;

                go.GetComponent<SaveablePrefab>().prefabIndex = v.index;
                if (dir.directory.Length <= v.index)
                {
                    Array.Resize(ref dir.directory, v.index + 5);
                }
                dir.directory[v.index] = go;
                Debug.Log("RealisticSkies: added the " + v.name + " as prefab " + v.index);
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not build the " + v.name + ": " + e);
            }
        }
    }
}
