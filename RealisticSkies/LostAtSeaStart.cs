using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// "Lost at Sea" (docs/LOSTATSEA_SPEC.md), step 3: the scenario's ship and its start.
    ///
    /// The scene: when the new-game carousel comes to the scenario, the medium dhow (boat 20) is
    /// taken from her shipyard to a random point of the start area, rigged as in the layout, and the
    /// menu is put beside her. Leaving that stop takes her back. (Boats are kinematic while the menu
    /// is up, so she can be moved at once.)
    ///
    /// The start: an era button starts an Aestrin new game with three things swapped: the starting
    /// boat is the dhow, the start position is her deck, and no region's starter set is given. The
    /// layout's items go into the boat's own cache of saved items (BoatLocalItems), so the game
    /// spawns them itself, as it does when a save is loaded. Once the game runs: needs, reputation,
    /// last port.
    /// </summary>
    internal static class LostAtSeaStart
    {
        private const int BoatIndex = 20;                       // BOAT dhow medium (20)
        private const string LayoutFile = "lostatsea_layout.txt";

        // the start area and Happy Bay (spec, section 2)
        private const float LonMin = 0.5f, LonMax = 1.5f, LatMin = 33f, LatMax = 35f;
        private const float HeadingMin = 30f, HeadingMax = 150f;
        private const float UnitsPerDegree = 9000f, LatAtZero = 36f;
        private const float ClearOfPorts = 4500f;               // half a degree from any port

        // "after the storm" (drafts: the user's to set)
        private const float StartFood = 60f, StartWater = 60f, StartSleep = 35f;
        private const int StartReputation = 300;                // level 1
        private const int StartLions = 500, StartDragons = 1000; // Al'Ankh lions, Emerald dragons

        private static readonly int[] Chronometers = { 170, 171, 172 };
        private static readonly int[] NotInMagellan = { 702, 710, 716 };    // quintant, pocket watch, The Modern Navigator
        private static readonly int[] Nailed = { 66, 67, 78, 107, 185 };    // tables, chairs, stove, wall shelves

        private const int JournalPrefab = 711, PocketWatchPrefab = 710;

        // Autumn 4 (day 0 is Autumn 1, the equinox): the Sun already has a declination to reckon with,
        // and the ring dial works. An hour and a quarter before sunset by the ship's own Sun.
        internal const int StartDay = 3;
        private const float StartLocalHour = 16.7f;
        internal const float CalmDays = 2.5f;                   // clear sky and a fair wind for so long

        private static int menuDay;
        private static float menuTime;

        /// <summary>The scenario's date and hour, at the ship's place.</summary>
        internal static void SetDate()
        {
            GameState.day = StartDay;
            if (Sun.sun != null) Sun.sun.globalTime = Mathf.Repeat(StartLocalHour - Lon / 15f, 24f);
            if (GameState.modData != null) GameState.modData[Calendar.StartKey] = StartDay.ToString();
            Calendar.DayWasSet();
        }

        internal static float Lat, Lon, Heading;

        // ---------------- the layout ----------------

        private class Item
        {
            public int id, prefab, days, crate, slot;
            public Vector3 pos;
            public Quaternion rot;
            public bool nailed;
            public float health, amount;
            public float[] extra = new float[5];
        }

        private class Layout
        {
            public readonly List<Item> items = new List<Item>();
            public readonly List<SaveSailData> sails = new List<SaveSailData>();
            public readonly List<string> parts = new List<string>();      // object names of the fitted options
        }

        private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        private static int I(string s) => int.Parse(s, CultureInfo.InvariantCulture);

        private static float[] Fs(string s)
        {
            string[] t = s.Split(',');
            var f = new float[t.Length];
            for (int i = 0; i < t.Length; i++) f[i] = F(t[i]);
            return f;
        }

        /// <summary>The captured layout: the file in the config folder while capturing (Debug), else the one shipped with the mod.</summary>
        private static Layout ReadLayout()
        {
            string text = null;
            if (Plugin.configLayoutCapture.Value && File.Exists(LostAtSeaLayout.FilePath)) text = File.ReadAllText(LostAtSeaLayout.FilePath);
            if (text == null)
            {
                byte[] data = ModResources.Read(LayoutFile, out string source);
                if (data == null) throw new FileNotFoundException("no " + LayoutFile + " in the plugin");
                text = Encoding.UTF8.GetString(data);
            }
            var lay = new Layout();
            foreach (string raw in text.Split('\n'))
            {
                string line = raw;
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash);
                string[] tok = line.Split(new[] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (tok.Length < 2) continue;
                var kv = new Dictionary<string, string>();
                for (int i = 1; i < tok.Length; i++)
                {
                    int eq = tok[i].IndexOf('=');
                    if (eq > 0) kv[tok[i].Substring(0, eq)] = tok[i].Substring(eq + 1);
                }
                try
                {
                    if (tok[0] == "part")
                    {
                        lay.parts.Add(kv["object"].Replace('_', ' '));
                        lay.parts.Add(kv["object"]);
                    }
                    else if (tok[0] == "sail")
                    {
                        lay.sails.Add(new SaveSailData
                        {
                            prefabIndex = I(kv["prefab"]), mastIndex = I(kv["mast"]), installHeight = F(kv["height"]),
                            minAngle = F(kv["min"]), maxAngle = F(kv["max"]), health = 100f, sailColor = I(kv["color"]),
                            scaleY = F(kv["sy"]), scaleZ = F(kv["sz"]),
                        });
                    }
                    else if (tok[0] == "item")
                    {
                        float[] p = Fs(kv["pos"]), q = Fs(kv["rot"]), e = Fs(kv["extra"]);
                        var it = new Item
                        {
                            id = I(kv["id"]), prefab = I(kv["prefab"]), pos = new Vector3(p[0], p[1], p[2]),
                            rot = new Quaternion(q[0], q[1], q[2], q[3]), nailed = kv["nailed"] == "1",
                            health = F(kv["health"]), amount = F(kv["amount"]), days = I(kv["days"]),
                            crate = I(kv["crate"]), slot = I(kv["slot"]),
                        };
                        for (int i = 0; i < 5 && i < e.Length; i++) it.extra[i] = e[i];
                        lay.items.Add(it);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("RealisticSkies: Lost at Sea: can't read layout line \"" + raw.Trim() + "\": " + ex.Message);
                }
            }
            return lay;
        }

        // ---------------- the scene behind the menu ----------------

        private static bool placed;
        private static Vector3 homeReal;
        private static Quaternion homeRot;
        private static SaveBoatCustomizationData homeLook;
        private static GameObject priceTag;
        private static bool priceTagWasOn;

        private static SaveableObject Boat => SaveLoadManager.instance.GetObject(BoatIndex);

        private static Vector3 RealPos(float lon, float lat, float y) => new Vector3(lon * UnitsPerDegree, y, (lat - LatAtZero) * UnitsPerDegree);

        private static void Roll()
        {
            FloatingOriginManager fom = FloatingOriginManager.instance;
            for (int tries = 0; tries < 40; tries++)
            {
                Lon = UnityEngine.Random.Range(LonMin, LonMax);
                Lat = UnityEngine.Random.Range(LatMin, LatMax);
                Vector3 p = RealPos(Lon, Lat, 0f);
                bool clear = true;
                if (Port.ports != null)
                {
                    foreach (Port port in Port.ports)
                    {
                        if (port == null) continue;
                        Vector3 d = fom.ShiftingPosToRealPos(port.transform.position) - p;
                        d.y = 0f;
                        if (d.magnitude < ClearOfPorts) { clear = false; break; }
                    }
                }
                if (clear) break;
            }
            Heading = UnityEngine.Random.Range(HeadingMin, HeadingMax);
        }

        /// <summary>The fitted parts by the options' object names, and the layout's sails.</summary>
        private static void Rig(SaveableObject so, Layout lay)
        {
            if (so.customization == null) return;
            var data = new SaveBoatCustomizationData();
            BoatCustomParts parts = so.GetComponent<BoatCustomParts>();
            if (parts != null)
            {
                foreach (BoatPart part in parts.availableParts)
                {
                    int option = part.activeOption;
                    for (int i = 0; i < part.partOptions.Count; i++)
                    {
                        if (part.partOptions[i] != null && lay.parts.Contains(part.partOptions[i].gameObject.name)) { option = i; break; }
                    }
                    data.partActiveOptions.Add(option);
                }
            }
            data.sails.AddRange(lay.sails);
            so.customization.LoadData(data);
        }

        /// <summary>The carousel came to the scenario: the ship goes to sea and the menu beside her.</summary>
        internal static void EnterScene(StartMenu menu)
        {
            SaveableObject so = Boat;
            if (so == null) { Debug.LogWarning("RealisticSkies: Lost at Sea: boat " + BoatIndex + " not found"); return; }
            Transform b = so.transform;
            FloatingOriginManager fom = FloatingOriginManager.instance;
            if (!placed)
            {
                homeReal = fom.ShiftingPosToRealPos(b.position);
                homeRot = b.rotation;
                menuDay = GameState.day;
                menuTime = Sun.sun != null ? Sun.sun.globalTime : 0f;
                Roll();
                SetDate();      // before anything is aboard: a watch would run down over the jump
                try
                {
                    if (so.customization != null) homeLook = so.customization.GetData();
                    Rig(so, ReadLayout());
                }
                catch (Exception e)
                {
                    Debug.LogWarning("RealisticSkies: Lost at Sea: could not rig the ship: " + e);
                }
                priceTag = Traverse.Create(so.GetComponent<PurchasableBoat>()).Field("purchaseUI").GetValue<GameObject>();
                if (priceTag != null)
                {
                    priceTagWasOn = priceTag.activeSelf;
                    priceTag.SetActive(false);
                }
                placed = true;
                Debug.Log(string.Format(CultureInfo.InvariantCulture, "RealisticSkies: Lost at Sea: ship at {0:F3} N, {1:F3} E, heading {2:F0}", Lat, Lon, Heading));
            }
            b.position = fom.RealPosToShiftingPos(RealPos(Lon, Lat, homeReal.y));
            b.rotation = Quaternion.Euler(0f, Heading, 0f);

            // the menu off her starboard quarter, the ship to the right of the buttons
            // far and high enough to take in her mastheads
            Vector3 eye = b.position + b.right * 27f - b.forward * 22f;
            eye.y = b.position.y + 4.5f;
            Vector3 look = b.position - eye;
            look.y = 0f;
            menu.transform.position = eye;
            menu.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up) * Quaternion.Euler(0f, -24f, 0f);
        }

        /// <summary>The carousel left the scenario: the ship goes back to her shipyard as she was.</summary>
        internal static void LeaveScene()
        {
            if (!placed) return;
            placed = false;
            SaveableObject so = Boat;
            if (so == null) return;
            so.transform.position = FloatingOriginManager.instance.RealPosToShiftingPos(homeReal);
            so.transform.rotation = homeRot;
            GameState.day = menuDay;
            if (Sun.sun != null) Sun.sun.globalTime = menuTime;
            Calendar.DayWasSet();
            try
            {
                if (homeLook != null && so.customization != null) so.customization.LoadData(homeLook);
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: Lost at Sea: could not restore the ship's rig: " + e);
            }
            if (priceTag != null) priceTag.SetActive(priceTagWasOn);
        }

        // ---------------- the start ----------------

        private static int NewId(HashSet<int> used)
        {
            int id;
            do { id = UnityEngine.Random.Range(1, int.MaxValue); }
            while (used.Contains(id) || (SaveablePrefab.existingInstanceIds != null && SaveablePrefab.existingInstanceIds.Contains(id)));
            used.Add(id);
            return id;
        }

        /// <summary>An era button was pressed: called before the game's own StartNewGame.</summary>
        internal static void Begin(StartMenu menu)
        {
            SaveableObject so = Boat;
            if (!placed) EnterScene(menu);
            Transform b = so.transform;

            BoatMooringRopes ropes = so.GetComponent<BoatMooringRopes>();
            if (ropes != null)
            {
                ropes.UnmoorAllRopes();
                ropes.GetAnchorController().ResetAnchor();
            }
            BoatDamage damage = so.GetComponent<BoatDamage>();
            if (damage != null) damage.waterLevel = 0f;

            // the items: into the boat's cache of saved items; the game spawns them as after a load
            Layout lay = ReadLayout();
            bool magellan = LostAtSeaMenu.Era == Difficulty.Magellan;
            var used = new HashSet<int>();
            var idOf = new Dictionary<int, int>();
            foreach (Item it in lay.items) idOf[it.id] = NewId(used);
            BoatLocalItems local = so.localItems;
            local.SetItemsLoaded(false);
            int n = 0;
            foreach (Item it in lay.items)
            {
                if (Array.IndexOf(Chronometers, it.prefab) >= 0) continue;
                if (magellan && Array.IndexOf(NotInMagellan, it.prefab) >= 0) continue;
                if (it.prefab <= 0 || it.prefab >= PrefabsDirectory.instance.directory.Length || PrefabsDirectory.instance.directory[it.prefab] == null)
                {
                    Debug.LogWarning("RealisticSkies: Lost at Sea: no prefab " + it.prefab + ", item left out");
                    continue;
                }
                int crate = 0;
                if (it.crate > 0) idOf.TryGetValue(it.crate, out crate);
                bool nailed = it.nailed || Array.IndexOf(Nailed, it.prefab) >= 0;
                int slot = it.prefab == JournalPrefab ? 0 : it.slot;       // the journal starts in the first pocket
                float health = it.health, amount = it.amount;
                if (it.prefab == PocketWatchPrefab)
                {
                    // wound and going, with a rate of its own, but up to half an hour out after the storm
                    // (ModItemPocketWatch: health = 100 x rate code + hours of run left, amount = hours fast)
                    health = UnityEngine.Random.Range(1, 100) * 100f + 36f;
                    amount = UnityEngine.Random.Range(-0.5f, 0.5f);
                }
                var d = new SavePrefabData(it.pos, b.rotation * it.rot, it.prefab, true, nailed, health, amount, slot, crate, -1, BoatIndex, it.days, idOf[it.id])
                {
                    extraValue0 = it.extra[0], extraValue1 = it.extra[1], extraValue2 = it.extra[2], extraValue3 = it.extra[3], extraValue4 = it.extra[4],
                };
                local.AddCachedItemFromSaveData(d);
                n++;
            }
            Debug.Log("RealisticSkies: Lost at Sea: " + n + " items put aboard");

            // the game's own start, with the dhow for the starting boat and her deck for the start position
            var spot = new GameObject("Lost at Sea start").transform;
            spot.SetParent(b, false);
            spot.localPosition = new Vector3(0.3f, 3.6f, -5.5f);
            spot.localRotation = Quaternion.identity;
            Traverse t = Traverse.Create(menu);
            t.Field("startMpos").SetValue(spot);
            PurchasableBoat[] starting = t.Field("startingBoats").GetValue<PurchasableBoat[]>();
            starting[2] = so.GetComponent<PurchasableBoat>();

            Plugin.instance.StartCoroutine(Settle(so, spot));
        }

        /// <summary>
        /// Stands the player on the start spot. The camera flying in touches the ship, so the game has
        /// already embarked the player by the time it places him, and it places him by world position:
        /// but an embarked player's controller lives on the boat's walk collider, a copy of the boat
        /// elsewhere. So: the same place in the boat's own space, for the controller and the observer.
        /// </summary>
        private static void StandOnDeck(SaveableObject so, Transform spot)
        {
            Transform ctrl = Refs.charController.transform, obs = Refs.observerMirror.transform;   // the controller carries both
            Transform boat = GameState.currentBoat;
            CharacterController cc = Refs.charController;
            bool on = cc.enabled;
            cc.enabled = false;
            if (boat != null && boat.IsChildOf(so.transform) && ctrl.parent != null && obs.parent == boat)
            {
                Vector3 lp = boat.InverseTransformPoint(spot.position);
                Quaternion lr = Quaternion.Inverse(boat.rotation) * spot.rotation;
                ctrl.localPosition = lp;
                ctrl.localRotation = lr;
                obs.localPosition = lp;
                obs.localRotation = lr;
                Debug.Log("RealisticSkies: Lost at Sea: player stood on deck (embarked), boat-local " + lp);
            }
            else
            {
                ctrl.position = spot.position;
                ctrl.rotation = spot.rotation;
                obs.position = spot.position;
                obs.rotation = spot.rotation;
                Debug.Log("RealisticSkies: Lost at Sea: player stood on deck (not embarked yet)");
            }
            cc.enabled = on;
        }

        private static IEnumerator Settle(SaveableObject so, Transform spot)
        {
            // the game's start has just placed the player and switched his controller on
            while (!Refs.charController.enabled && !GameState.playing) yield return null;
            StandOnDeck(so, spot);

            while (!GameState.playing) yield return null;
            LostAtSeaNotes.Begin(LostAtSeaMenu.Era, Lat, Lon);
            for (int i = 0; i < 5; i++) yield return null;

            GameState.lastBoat = so.transform;
            GameState.lastOwnedBoat = so.transform;

            PlayerNeeds.food = StartFood;
            PlayerNeeds.water = StartWater;
            PlayerNeeds.sleep = StartSleep;

            // some money of the other two archipelagos, besides Aestrin's 150 crowns
            PlayerGold.currency[0] += StartLions;
            PlayerGold.currency[1] += StartDragons;

            foreach (PortRegion r in new[] { PortRegion.alankh, PortRegion.emerald, PortRegion.medi })
            {
                int rep = PlayerReputation.GetRep(r);
                if (rep < StartReputation) PlayerReputation.ChangeReputation(StartReputation - rep, r);
            }

            Port oasis = null;
            if (Port.ports != null)
            {
                foreach (Port p in Port.ports)
                {
                    if (p != null && p.GetPortName() != null && p.GetPortName().IndexOf("Oasis", StringComparison.OrdinalIgnoreCase) >= 0) { oasis = p; break; }
                }
            }
            if (oasis != null)
            {
                GameState.lastVisitedPort = oasis;
                // the game's Recovery goes to the last port only if that port has a recovery berth; else to the nearest that has
                bool berth = false;
                try { berth = Recovery.GetPort(oasis.portIndex) != null; } catch (Exception) { }
                if (!berth) Debug.LogWarning("RealisticSkies: Lost at Sea: Oasis has no recovery berth; Recovery will go to the nearest port that has one");
            }
            else Debug.LogWarning("RealisticSkies: Lost at Sea: no port named Oasis; the last port is not set");

            Debug.Log("RealisticSkies: Lost at Sea started; last port " + (oasis != null ? oasis.GetPortName() : "none"));
        }
    }

    /// <summary>
    /// The first days of a Lost at Sea game: a clear sky (no storm comes near enough to cloud it) and a
    /// wind between 8 and 20, so the newcomer can take his sights and make way.
    /// </summary>
    [HarmonyPatch]
    internal static class LostAtSeaWeather
    {
        private const float WindMin = 8f, WindMax = 20f;

        private static bool calm;
        private static float checkedAt = -10f;

        private static bool Calm
        {
            get
            {
                if (Time.unscaledTime - checkedAt > 1f)
                {
                    checkedAt = Time.unscaledTime;
                    calm = GameState.playing && Sun.sun != null && LostAtSeaNotes.CalmUntil > GameState.day + Sun.sun.globalTime / 24f;
                }
                return calm;
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(WeatherStorms), "GetNormalizedDistance")]
        private static void NoCloud(ref float __result)
        {
            if (Calm) __result = 1f;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(WeatherStorms), "FindClosestStorm")]
        private static void NoStormWind()
        {
            if (Calm) WeatherStorms.currentStormDistance = 999999f;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Wind), "Update")]
        private static void FairWind()
        {
            if (!Calm) return;
            float m = Wind.currentWind.magnitude;
            if (m < 0.01f) Wind.currentWind = new Vector3(0.5f, 0f, 0.85f) * WindMin;
            else if (m < WindMin) Wind.currentWind *= WindMin / m;
            else if (m > WindMax) Wind.currentWind *= WindMax / m;
        }
    }
}
