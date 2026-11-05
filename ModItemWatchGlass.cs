using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Watch glass (prefab 709): an hour glass in a frame that turns itself on a stand, standing in
    /// for the ship's boy who turned the glass and struck the bell. Right click starts it: the glass
    /// is turned at once, and from then on it turns itself whenever the sand has run out and the
    /// bell strikes the hours of the watch, 2, 4, 6 and 8 bells, then 2 again. Right click again
    /// stops it: the sand runs out and the glass stays as it is.
    ///
    /// Every run of the sand lasts an hour give or take a minute, at random, so the glass wanders off
    /// the true time like a random walk (about 5 minutes a day, 8-9 over three days) and has to be
    /// set again now and then from a lunar distance. Big item: stands on the deck, can be nailed.
    ///
    /// State is kept in the two floats the game saves for any item:
    /// health = hours of sand left in the upper bulb, +10 while it turns itself;
    /// amount = run length in hours + 10 x bell (0..4) + 50 if the frame is turned over.
    /// </summary>
    public class ModItemWatchGlass : ShipItem
    {
        public const int PrefabIndex = 709;
        private const int SourceIndex = 91;
        private const string ItemName = "watch glass";
        private const float RunHours = 1f;
        private const float RunSigma = 1f / 60f;        // one minute of game time per run, 1 sigma
        private const float TurnSeconds = 1.6f;         // real seconds the frame takes to turn over

        private static float Lift => WatchGlassMesh.Top / 2f;  // origin = middle of the item's box

        // state, read from and written back to health/amount every frame
        private float sandLeft, runLength = RunHours;
        private bool auto, flipped;
        private int bell;

        // the ship's time it keeps, once set by a lunar distance (saved in the spare item values):
        // the hour (absolute, by its own reckoning) the current run began, and a start still to come
        internal double glassHour;                      // 0 = keeps no reckoned time
        internal double pendingAt;                      // the true time the next set start is due (0 = none)
        internal double pendingHour;                    // the hour it will strike then

        private Transform frame;
        private Mesh sandMesh;
        private double lastTime = -1;
        private float shownUpper = -1f;
        private bool shownFlipped, shownRunning;
        private float turnLeft;                         // degrees of the turn still to go
        private AudioSource hiss, bellSource;

        // ---------------- life ----------------

        public override void OnLoad()
        {
            base.name = ItemName;
            this.frame = base.transform.Find("frame");
            Transform t = this.frame != null ? this.frame.Find("sand") : null;
            if (t != null)
            {
                this.sandMesh = new Mesh { name = "watch glass sand" };
                this.sandMesh.MarkDynamic();
                t.GetComponent<MeshFilter>().sharedMesh = this.sandMesh;
            }
            this.lastTime = -1;
            this.hiss = ModItemHourglass.MakeSource(base.gameObject, ModItemHourglass.SandClip, true, 0.13f, 3.5f);     // the sand a third of what it was
            if (this.hiss != null) this.hiss.pitch = UnityEngine.Random.Range(0.85f, 0.92f);
            this.bellSource = ModItemHourglass.MakeSource(base.gameObject, ModItemHourglass.Bell, false, 1f, 40f);
            this.Decode();
            this.ShowFrame(true);
        }

        /// <summary>Right click (looking at it or holding it) starts or stops the glass. Unsold, it buys.</summary>
        public override void OnAltActivate()
        {
            if (!base.sold)
            {
                base.OnAltActivate();
                return;
            }
            if (this.turnLeft > 0f) return;
            this.Decode();
            this.Advance(false);
            this.glassHour = 0;
            this.pendingAt = 0;
            if (this.auto)
            {
                this.auto = false;
                Notify("The watch glass is stopped. The sand runs out and it stays.");
            }
            else
            {
                // turned at once: what was in the lower bulb is now on top
                float upper = this.Upper();
                this.flipped = !this.flipped;
                this.runLength = NewRun();
                this.sandLeft = (1f - upper) * this.runLength;
                this.bell = 0;
                this.auto = true;
                this.turnLeft = 180f;
                Notify("The watch glass is running. It turns itself every hour and strikes the bells of the watch.");
            }
            UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.5f, 0.8f);
            this.Encode();
        }

        public override void ExtraLateUpdate()
        {
            base.ExtraLateUpdate();
            this.Decode();
            this.Advance(true);
            this.Encode();
            this.ShowFrame(false);
        }

        // ---------------- time ----------------

        private static double GameHours()
        {
            if (Sun.sun == null) return -1;
            return GameState.day * 24.0 + Sun.sun.globalTime;
        }

        private static float NewRun()
        {
            // Box-Muller: a normal error on every run
            float u1 = Mathf.Max(1e-6f, UnityEngine.Random.value), u2 = UnityEngine.Random.value;
            float g = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
            return RunHours + Mathf.Clamp(g * RunSigma, -4f * RunSigma, 4f * RunSigma);
        }

        private float Upper() => this.runLength > 0f ? Mathf.Clamp01(this.sandLeft / this.runLength) : 0f;

        /// <summary>Runs the sand up to now, turning the glass as often as it ran out.</summary>
        private void Advance(bool live)
        {
            double now = GameHours();
            double dt = (this.lastTime >= 0 && now >= 0) ? now - this.lastTime : 0;
            if (dt < 0) dt = 0;                             // clock set back
            if (now >= 0) this.lastTime = now;
            if (!base.sold) return;

            int turns = 0;
            bool watchEnd = false;
            int guard = 0;
            if (this.pendingAt > 0 && now >= this.pendingAt)
            {
                // the reckoned hour has come: turned over full, and the bells of that hour struck
                double late = now - this.pendingAt;
                this.flipped = !this.flipped;
                this.runLength = NewRun();
                this.sandLeft = this.runLength;
                this.auto = true;
                this.glassHour = this.pendingHour;
                this.bell = BellOf(this.glassHour);
                this.pendingAt = 0;
                dt = late;
                turns++;
                if (this.bell == 4) watchEnd = true;
            }
            while (dt > 0 && this.sandLeft > 0f && guard++ < 100000)
            {
                if (this.sandLeft > dt)
                {
                    this.sandLeft -= (float)dt;
                    break;
                }
                dt -= this.sandLeft;
                this.sandLeft = 0f;
                if (!this.auto) break;
                this.flipped = !this.flipped;
                this.bell = this.bell % 4 + 1;
                if (this.glassHour > 0) this.glassHour += 1.0;
                this.runLength = NewRun();
                this.sandLeft = this.runLength;
                turns++;
                if (this.bell == 4) watchEnd = true;
            }
            // heard only when it happens before your ears: a turn or two in a frame (sleep speeds time
            // up), not the dozens caught up after loading or coming back to the ship
            if (turns > 0 && live && turns <= 3)
            {
                if (this.turnLeft <= 0f) this.turnLeft = 180f;
                this.StartCoroutine(this.Strike(2 * this.bell));
                if (watchEnd) WakeCaptain(base.transform.position);
            }
        }

        private IEnumerator Strike(int strikes)
        {
            if (this.bellSource == null || this.bellSource.clip == null) yield break;
            // the first stroke is the moment itself: the sand ran out and the new run began
            for (int i = 0; i < strikes; i++)
            {
                this.bellSource.PlayOneShot(this.bellSource.clip);
                // in pairs: ding-ding ... ding-ding
                yield return new WaitForSecondsRealtime(i % 2 == 0 ? 0.45f : 1.15f);
            }
        }

        private static void WakeCaptain(Vector3 at)
        {
            if (!Plugin.configWatchWake.Value || !GameState.sleeping || Sleep.instance == null) return;
            Camera cam = Camera.main;
            if (cam != null && Vector3.Distance(cam.transform.position, at) > 40f) return;     // not on this ship
            Debug.Log("RealisticSkies: eight bells, waking the captain");
            Sleep.instance.WakeUp();
        }

        private static void Notify(string msg)
        {
            try
            {
                NotificationUi.instance.ShowNotification(msg);
            }
            catch (Exception)
            {
                // no notification UI: nothing to show
            }
        }

        // ---------------- the ship's time ----------------

        /// <summary>The bells of a watch for an hour of the day: 2 at 1, 4 at 2, 6 at 3, 8 at 4, then again (as bell 1..4).</summary>
        private static int BellOf(double hour)
        {
            int h = (int)Math.Round(Celestial.Repeat(hour, 24.0)) % 24;
            return (h + 3) % 4 + 1;
        }

        /// <summary>Whether it keeps a reckoned time now.</summary>
        internal bool Keeping => base.sold && this.auto && this.glassHour > 0 && this.pendingAt <= 0;

        /// <summary>
        /// The time it gives, absolute hours by its own reckoning: whole hours counted by its turns, and
        /// the part of the hour the sand has run (each run taken for an hour, whatever it really was).
        /// </summary>
        internal double ReckonedTime
        {
            get
            {
                this.Decode();
                this.Advance(false);
                this.Encode();
                if (!this.Keeping) return double.NaN;
                return this.glassHour + Mathf.Clamp01(1f - this.Upper());
            }
        }

        /// <summary>
        /// Set it by a worked-out time: the zero meridian's time is the true time plus this offset (by the
        /// navigator's reckoning). It stops, and turns itself over at the next whole hour of that time,
        /// striking that hour's bells; from then on it keeps the hours.
        /// </summary>
        internal void SetBy(double offset)
        {
            this.Decode();
            this.Advance(false);
            double now = GameHours();
            double hour = Math.Ceiling(now + offset + 1e-6);
            this.pendingHour = hour;
            this.pendingAt = hour - offset;
            this.glassHour = 0;
            this.auto = false;                  // the sand runs out meanwhile
            this.Encode();
        }

        internal void SaveTo(SavePrefabData d)
        {
            d.extraValue0 = (float)this.glassHour;
            d.extraValue1 = (float)this.pendingAt;
            d.extraValue2 = (float)this.pendingHour;
        }

        internal void LoadFrom(SavePrefabData d)
        {
            this.glassHour = d.extraValue0;
            this.pendingAt = d.extraValue1;
            this.pendingHour = d.extraValue2;
        }

        /// <summary>The ship's glasses within reach of a point (on your ship, by and large).</summary>
        internal static System.Collections.Generic.List<ModItemWatchGlass> Near(Vector3 at, float within)
        {
            var list = new System.Collections.Generic.List<ModItemWatchGlass>();
            foreach (ModItemWatchGlass g in UnityEngine.Object.FindObjectsOfType<ModItemWatchGlass>())
            {
                if (g.sold && Vector3.Distance(g.transform.position, at) <= within) list.Add(g);
            }
            list.Sort((a, b) => Vector3.Distance(a.transform.position, at).CompareTo(Vector3.Distance(b.transform.position, at)));
            return list;
        }

        // ---------------- state in health/amount ----------------

        private void Decode()
        {
            float h = base.health;
            this.auto = h >= 5f;
            this.sandLeft = Mathf.Max(0f, this.auto ? h - 10f : h);
            float a = base.amount;
            this.flipped = a >= 45f;
            if (this.flipped) a -= 50f;
            this.bell = Mathf.Clamp(Mathf.FloorToInt((a + 0.5f) / 10f), 0, 4);
            this.runLength = a - this.bell * 10f;
            if (this.runLength < 0.5f || this.runLength > 1.5f) this.runLength = RunHours;
        }

        private void Encode()
        {
            base.health = this.sandLeft + (this.auto ? 10f : 0f);
            base.amount = this.runLength + this.bell * 10f + (this.flipped ? 50f : 0f);
        }

        // ---------------- looks and sound ----------------

        private void ShowFrame(bool force)
        {
            if (this.frame == null) return;
            float target = this.flipped ? 180f : 0f;
            if (this.turnLeft > 0f)
            {
                this.turnLeft = Mathf.Max(0f, this.turnLeft - 180f * Time.unscaledDeltaTime / TurnSeconds);
                this.frame.localRotation = Quaternion.Euler(target - this.turnLeft, 0f, 0f);
            }
            else
            {
                this.frame.localRotation = Quaternion.Euler(target, 0f, 0f);
            }
            bool turning = this.turnLeft > 0f;

            // while it turns the sand lies still, in what is becoming the upper bulb
            float upper = this.Upper();
            bool running = !turning && base.sold && this.sandLeft > 0f;
            if (this.hiss != null)
            {
                if (running && !this.hiss.isPlaying) this.hiss.Play();
                else if (!running && this.hiss.isPlaying) this.hiss.Stop();
            }
            if (this.sandMesh == null) return;
            if (force || this.flipped != this.shownFlipped || running != this.shownRunning || Mathf.Abs(upper - this.shownUpper) > 0.003f)
            {
                // the frame's +y end is down when it is turned over: the upper sand is in the other bulb
                HourglassMesh.BuildSand(this.sandMesh, upper, running, this.flipped, WatchGlassMesh.GlassScale);
                this.shownUpper = upper;
                this.shownFlipped = this.flipped;
                this.shownRunning = running;
            }
        }

        // ---------------- shops ----------------

        /// <summary>Stands the watch glass on the shop floor beside the Star Chart's stall (or on its spot).</summary>
        internal static void AddToShop(Transform shop, int island)
        {
            if (shop == null) return;
            foreach (ShopItemSpawner sp in shop.GetComponentsInChildren<ShopItemSpawner>(true))
            {
                SaveablePrefab sv = sp.itemPrefab != null ? sp.itemPrefab.GetComponent<SaveablePrefab>() : null;
                if (sv == null || sv.prefabIndex != 703) continue;
                GameObject clone = UnityEngine.Object.Instantiate(sp.gameObject, sp.transform.parent);
                clone.name = ItemName;
                if (ShopSpots.TryGet(ShopSpots.WatchGlass, island, out ShopSpots.Spot spot))
                {
                    ShopSpots.Apply(clone.transform, shop, spot);
                }
                else
                {
                    float side = ShopSpots.TryGet(ShopSpots.Scroll, island, out _) ? -1f : 1f;
                    clone.transform.localPosition = sp.transform.localPosition + new Vector3(side * 1.05f, 0.02f, 0f);
                    clone.transform.rotation = shop.rotation;
                    clone.transform.position += shop.up * Lift;
                }
                ShopItemSpawner csp = clone.GetComponent<ShopItemSpawner>();
                AccessTools.Field(typeof(ShopItemSpawner), "item").SetValue(csp, null);
                csp.enabled = false;
                Renderer r = clone.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
                var hook = clone.AddComponent<LoreSpawnerHook>();
                hook.spawner = csp;
                hook.prefabIndex = PrefabIndex;
                ShopSpots.RegisterSpawner(ShopSpots.WatchGlass, island, clone.transform);
                break;
            }
        }

        // ---------------- prefab ----------------

        private static GameObject holder;

        /// <summary>Builds prefab 709 from the vanilla scroll and puts it in the prefab directory.</summary>
        internal static void Register(PrefabsDirectory dir)
        {
            if (holder != null || dir == null || dir.directory == null) return;
            if (dir.directory.Length <= SourceIndex || dir.directory[SourceIndex] == null)
            {
                Debug.LogWarning("RealisticSkies: vanilla scroll (prefab 91) not found, no watch glass");
                return;
            }
            if (dir.directory.Length > PrefabIndex && dir.directory[PrefabIndex] != null)
            {
                Debug.LogWarning("RealisticSkies: prefab " + PrefabIndex + " is already taken, no watch glass");
                return;
            }
            try
            {
                ModItemHourglass.MakeAssets();
                holder = new GameObject("RealisticSkies watch glass holder");
                holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(holder);
                GameObject go = UnityEngine.Object.Instantiate(dir.directory[SourceIndex], holder.transform);
                go.name = PrefabIndex + " " + ItemName;

                ShipItemScroll old = go.GetComponent<ShipItemScroll>();
                ModItemWatchGlass item = go.AddComponent<ModItemWatchGlass>();
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
                box.center = Vector3.zero;
                box.size = new Vector3(WatchGlassMesh.BaseR * 2f, WatchGlassMesh.Top, WatchGlassMesh.BaseR * 2f);
                go.GetComponent<MeshFilter>().sharedMesh = WatchGlassMesh.BuildStand(Lift);
                go.GetComponent<MeshRenderer>().sharedMaterials = new[] { ModItemHourglass.wood };

                var frameGo = new GameObject("frame");
                frameGo.transform.SetParent(go.transform, false);
                frameGo.transform.localPosition = new Vector3(0f, WatchGlassMesh.AxleY - Lift, 0f);
                frameGo.AddComponent<MeshFilter>().sharedMesh = HourglassMesh.BuildBody(WatchGlassMesh.GlassScale, 0f);
                frameGo.AddComponent<MeshRenderer>().sharedMaterials = new[] { ModItemHourglass.wood, ModItemHourglass.glass };

                var sandGo = new GameObject("sand");
                sandGo.transform.SetParent(frameGo.transform, false);
                sandGo.AddComponent<MeshFilter>();
                sandGo.AddComponent<MeshRenderer>().sharedMaterial = ModItemHourglass.sand;

                item.name = ItemName;
                item.big = true;                // stands on the deck, carried in both hands, can be nailed
                item.amount = RunHours;         // stopped, upright, all the sand settled below
                item.health = 0f;
                item.value = 300;
                if (Patches.itemPrefabs.TryGetValue(703, out GameObject chart) && chart != null
                    && chart.GetComponent<ShipItem>() is ShipItem chartItem && chartItem.value > 0)
                {
                    item.value = Mathf.RoundToInt(chartItem.value * 1.2f);     // about 300 crowns
                }
                item.mass = 3f;

                go.GetComponent<SaveablePrefab>().prefabIndex = PrefabIndex;
                if (dir.directory.Length <= PrefabIndex)
                {
                    Array.Resize(ref dir.directory, PrefabIndex + 5);
                }
                dir.directory[PrefabIndex] = go;
                Debug.Log("RealisticSkies: added the watch glass as prefab " + PrefabIndex);
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not build the watch glass: " + e);
            }
        }
    }

    /// <summary>
    /// A nailed item can't be looked at, except crates, bottles and beds (GoPointer.DoRaycast). The
    /// watch glass must stay clickable when nailed, so it can be started and stopped: if the pointer
    /// came up empty-handed and is on a nailed watch glass, point it there.
    /// </summary>
    [HarmonyPatch(typeof(GoPointer), "DoRaycast")]
    internal static class WatchGlassPointerPatch
    {
        private static readonly AccessTools.FieldRef<GoPointer, PickupableItem> heldItem = AccessTools.FieldRefAccess<GoPointer, PickupableItem>("heldItem");
        private static readonly AccessTools.FieldRef<GoPointer, GoPointerButton> pointedAt = AccessTools.FieldRefAccess<GoPointer, GoPointerButton>("pointedAtButton");
        private static readonly AccessTools.FieldRef<GoPointer, float> lookDistance = AccessTools.FieldRefAccess<GoPointer, float>("currentLookDistance");
        private static readonly AccessTools.FieldRef<GoPointer, Ray> ray = AccessTools.FieldRefAccess<GoPointer, Ray>("raycastRay");

        private static void Postfix(GoPointer __instance)
        {
            if (heldItem(__instance) != null || pointedAt(__instance) != null) return;
            if (GameState.sleeping || GameState.inBed || BoatCamera.on || __instance.debugEditorPointer) return;
            if (!Physics.Raycast(ray(__instance), out RaycastHit hit, 1.8f, -604165)) return;
            Collider col = hit.collider;
            if (col.CompareTag("ItemSubcollider") && col.transform.parent != null) col = col.transform.parent.GetComponent<Collider>();
            ModItemWatchGlass glass = col != null ? col.GetComponent<ModItemWatchGlass>() : null;
            if (glass == null || !glass.nailed || glass.unclickable) return;
            pointedAt(__instance) = glass;
            glass.Look(__instance);
            lookDistance(__instance) = hit.distance;
        }
    }

    [HarmonyPatch]
    internal static class WatchGlassSavePatches
    {
        [HarmonyPatch(typeof(SaveablePrefab), "PrepareSaveData")]
        [HarmonyPostfix]
        private static void Save(SaveablePrefab __instance, SavePrefabData __result)
        {
            ModItemWatchGlass g = __instance.GetComponent<ModItemWatchGlass>();
            if (g != null && __result != null) g.SaveTo(__result);
        }

        [HarmonyPatch(typeof(SaveablePrefab), "Load")]
        [HarmonyPostfix]
        private static void Load(SaveablePrefab __instance, SavePrefabData data)
        {
            ModItemWatchGlass g = __instance.GetComponent<ModItemWatchGlass>();
            if (g != null && data != null) g.LoadFrom(data);
        }
    }
}
