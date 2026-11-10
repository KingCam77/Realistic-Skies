using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The watchmaker in the capitals: an old trader (a copy of one of the game's shopkeepers) with a
    /// small stall. Bring him a pocket watch in your hand and he offers to regulate it; pay, and he lays
    /// it on his stall and works on it for four hours. Only while he is there (7:00 to 18:00) can you
    /// take it back, and only when it is done. A regulated watch shows the true time, is fully wound,
    /// and keeps a much smaller rate of its own.
    ///
    /// Placing by hand (Debug / Shop Placement on):
    ///   F9   look at a trader: he becomes the watchmaker's model; look at anything else (a small
    ///        stall, a table): it becomes the stall's model. Models go to the config as recipes.
    ///   F10  put the watchmaker where you look, facing you (Shift+F10: take him away from this town).
    ///   F11  put the stall where you look, facing you (Shift+F11: take it away).
    ///   F7   turn the last one you put by 15 degrees (Shift: the other way).
    ///   Ctrl+F8  look at a pocket watch lying where the watchmaker should lay watches: stores that spot.
    /// Spots are in the space of the mod's shop in that town, like the goods' spots.
    /// </summary>
    internal static class Watchmaker
    {
        internal const string Npc = "npc", Stall = "stall", Bench = "bench";
        private const float WorkHours = 4f;
        private const float PriceShare = 0.3f;      // of the watch's price
        internal const float Opens = 7f, Closes = 18f;

        private class Built
        {
            public GameObject npc, stall;
            public string npcKey, stallKey;         // what they were built from, to rebuild on a change
        }

        private static readonly Dictionary<int, Transform> Shops = new Dictionary<int, Transform>();
        private static readonly Dictionary<int, Built> Towns = new Dictionary<int, Built>();
        internal static readonly Dictionary<int, WatchmakerNpc> Npcs = new Dictionary<int, WatchmakerNpc>();

        /// <summary>Hand-placed spots, baked in once settled: kind -> island -> pose (shop space).</summary>
        private static readonly Dictionary<string, Dictionary<int, ShopSpots.Spot>> Baked = new Dictionary<string, Dictionary<int, ShopSpots.Spot>>
        {
            {
                Npc, new Dictionary<int, ShopSpots.Spot>
                {
                    { 1, ShopSpots.S(-1.417f, 0.001f, 15.124f, 0.0000f, -0.8856f, 0.0000f, -0.4644f) },
                    { 9, ShopSpots.S(3.362f, -0.033f, -1.290f, 0.0000f, -0.0212f, 0.0000f, 0.9998f) },
                    { 15, ShopSpots.S(-23.640f, -0.010f, 8.454f, 0.0000f, -0.1805f, 0.0000f, -0.9836f) },     // moved: another mod's trader stood here
                }
            },
            {
                Stall, new Dictionary<int, ShopSpots.Spot>
                {
                    { 1, ShopSpots.S(-0.724f, 0.001f, 14.771f, 0.0000f, 0.3050f, 0.0000f, 0.9523f) },
                    { 9, ShopSpots.S(3.448f, -0.033f, -0.437f, 0.0000f, -0.7103f, 0.0000f, 0.7039f) },
                    { 15, ShopSpots.S(-23.316f, -0.012f, 9.233f, 0.0000f, -0.5735f, 0.0000f, 0.8192f) },
                }
            },
            {
                Bench, new Dictionary<int, ShopSpots.Spot>
                {
                    { 1, ShopSpots.S(-0.765f, 0.719f, 14.755f, 0.5815f, -0.3513f, 0.3085f, 0.6659f) },
                    { 15, ShopSpots.S(-23.325f, 0.699f, 9.262f, -0.1238f, 0.7401f, -0.6463f, -0.1387f) },     // the old bench, carried along with the stall
                }
            },
        };
        private const int BenchModel = 15;     // a town without its own bench lays watches as this one does, on its stall
        private const string BakedNpcModel = "";
        private const string BakedStallModel = "mesh|market_stall_E1#402|buildings_E_paint|0,0,0|-0.7071068,0,0,0.7071068|1,1,1; meshcol|0|0,0,0|-0.7071068,0,0,0.7071068|1,1,1|market_stall_E1#402,0|0";

        internal static void AddToShop(Transform shop, int island)
        {
            Shops[island] = shop;
            if (Towns.TryGetValue(island, out Built b))
            {
                if (b.npc != null) UnityEngine.Object.Destroy(b.npc);
                if (b.stall != null) UnityEngine.Object.Destroy(b.stall);
                Towns.Remove(island);
            }
        }

        /// <summary>How each capital's watchmaker looks (Player Model style), unless the config says otherwise.</summary>
        private static readonly Dictionary<int, string> BakedLooks = new Dictionary<int, string>
        {
            { 1, "gender=1;head=7;hair=20;eyebrow=8;facialhair=2;headgear=0;torso=10;hips=6;legs=20;c_skin=5;c_hair=12;c_primary=3;c_secondary=18;c_leather=2;c_metal=4" },
            { 9, "gender=1;head=1;hair=2;eyebrow=8;facialhair=18;headgear=0;torso=15;hips=10;legs=18;c_skin=6;c_hair=11;c_primary=12;c_secondary=6;c_leather=2;c_metal=1" },
            { 15, "gender=1;head=5;hair=8;eyebrow=8;facialhair=18;headgear=9;torso=28;hips=28;legs=11;c_skin=2;c_hair=6;c_primary=19;c_secondary=20;c_leather=3;c_metal=1" },
        };

        internal static string Look(int island)
        {
            string cfg = Plugin.configWatchmakerLook.Value.Trim();
            if (cfg.Length > 0) return cfg;
            return BakedLooks.TryGetValue(island, out string l) ? l : "";
        }

        /// <summary>A world scale, whatever the scale of the town's scenery it hangs under.</summary>
        private static void WorldScale(Transform t, float scale)
        {
            Vector3 p = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(scale / Nz(p.x), scale / Nz(p.y), scale / Nz(p.z));
        }

        private static float Nz(float f) => Mathf.Abs(f) < 1e-6f ? 1f : f;

        private static string NpcModel =>
            Plugin.configWatchmakerModel.Value.Length > 0 ? Plugin.configWatchmakerModel.Value
            : BakedNpcModel.Length > 0 ? BakedNpcModel
            : "char|||";     // dressed from the look alone: any figure will do
        private static string StallModel => Plugin.configWatchmakerStall.Value.Length > 0 ? Plugin.configWatchmakerStall.Value : BakedStallModel;

        // ---------------- every frame ----------------

        private static float nextTry;

        internal static void Tick()
        {
            try
            {
                if (Plugin.configShopPlacement.Value) Keys();
                if (Time.time >= nextTry)
                {
                    nextTry = Time.time + 2f;
                    BuildTowns();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: watchmaker: " + e);
                nextTry = Time.time + 10f;
            }
        }

        private static void BuildTowns()
        {
            foreach (KeyValuePair<int, Transform> kv in Shops)
            {
                int island = kv.Key;
                Transform shop = kv.Value;
                if (shop == null) continue;
                if (!Towns.TryGetValue(island, out Built b)) Towns[island] = b = new Built();

                string npcKey = TryGet(Npc, island, out ShopSpots.Spot ns) ? NpcModel + "@" + SpotKey(ns) + "#" + Look(island) : null;
                if (b.npcKey != npcKey || (npcKey != null && b.npc == null))
                {
                    if (b.npc != null) UnityEngine.Object.Destroy(b.npc);
                    b.npc = null;
                    b.npcKey = null;
                    if (npcKey != null && NpcModel.Length > 0)
                    {
                        GameObject go = NpcModel.StartsWith("char|") ? Townsfolk.Build(NpcModel, "watchmaker", Look(island)) : Recipe.Build(NpcModel, "watchmaker", true);
                        if (go != null)
                        {
                            go.transform.SetParent(shop.parent, true);
                            ShopSpots.Apply(go.transform, shop, ns);
                            var npc = go.AddComponent<WatchmakerNpc>();
                            npc.island = island;
                            Npcs[island] = npc;
                            b.npc = go;
                            b.npcKey = npcKey;
                        }
                    }
                    else if (npcKey == null)
                    {
                        b.npcKey = null;
                    }
                }

                string stallKey = TryGet(Stall, island, out ShopSpots.Spot ss) ? StallModel + "@" + SpotKey(ss) + "x" + Plugin.configWatchmakerStallScale.Value.ToString("R", CultureInfo.InvariantCulture) : null;
                if (b.stallKey != stallKey || (stallKey != null && b.stall == null))
                {
                    if (b.stall != null) UnityEngine.Object.Destroy(b.stall);
                    b.stall = null;
                    b.stallKey = null;
                    if (stallKey != null && StallModel.Length > 0)
                    {
                        GameObject go = Recipe.Build(StallModel, "watchmaker's stall", false);
                        if (go != null)
                        {
                            go.transform.SetParent(shop.parent, true);
                            ShopSpots.Apply(go.transform, shop, ss);
                            WorldScale(go.transform, Mathf.Clamp(Plugin.configWatchmakerStallScale.Value, 0.1f, 3f));
                            b.stall = go;
                            b.stallKey = stallKey;
                        }
                    }
                }
            }
        }

        private static string SpotKey(ShopSpots.Spot s) => s.pos.ToString("F4") + s.rot.ToString("F5");

        /// <summary>Where the watchmaker lays a watch he works on, in the world.</summary>
        internal static bool BenchPose(int island, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            if (Shops.TryGetValue(island, out Transform shop) && shop != null && TryGet(Bench, island, out ShopSpots.Spot b))
            {
                pos = shop.TransformPoint(b.pos);
                rot = shop.rotation * b.rot;
                return true;
            }
            if (Shops.TryGetValue(island, out shop) && shop != null && TryGet(Stall, island, out ShopSpots.Spot st)
                && TryGet(Stall, BenchModel, out ShopSpots.Spot ms) && TryGet(Bench, BenchModel, out ShopSpots.Spot mb))
            {
                // no bench of its own: where the model town's lies on its stall (the top slopes a little)
                Quaternion inv = Quaternion.Inverse(ms.rot);
                Vector3 rel = inv * (mb.pos - ms.pos);
                Quaternion relRot = inv * mb.rot;
                pos = shop.TransformPoint(st.pos + st.rot * rel);
                rot = shop.rotation * st.rot * relRot;
                return true;
            }
            if (Npcs.TryGetValue(island, out WatchmakerNpc npc) && npc != null)
            {
                // nothing stored: on the counter in front of him
                Transform t = npc.transform;
                pos = t.position + t.forward * 0.6f + Vector3.up * 0.95f;
                rot = Quaternion.LookRotation(Vector3.up, -t.forward);
                return true;
            }
            return false;
        }

        /// <summary>The capital whose watchmaker is nearest a point (-1: none within 80 m).</summary>
        internal static int NearestTown(Vector3 at)
        {
            int best = -1;
            float bd = 80f;
            foreach (KeyValuePair<int, WatchmakerNpc> kv in Npcs)
            {
                if (kv.Value == null) continue;
                float d = Vector3.Distance(kv.Value.transform.position, at);
                if (d < bd) { bd = d; best = kv.Key; }
            }
            return best;
        }

        /// <summary>Whether the watchmaker of this town is at his stall now.</summary>
        internal static bool Present(int island)
        {
            return Npcs.TryGetValue(island, out WatchmakerNpc npc) && npc != null && npc.Present;
        }

        // ---------------- regulating ----------------

        internal static int Price(ShipItem item, int region)
        {
            float raw = Mathf.Max(1f, item.value * PriceShare * (ChronoState.IsChrono(item) ? Mode.ChronometerPrice : 1f));
            try
            {
                return CurrencyMarket.instance.GetBuyPriceInCurrency((Currency)region, raw, false);
            }
            catch (Exception)
            {
                return Mathf.RoundToInt(raw);
            }
        }

        internal static void Regulate(WatchmakerNpc npc, ShipItem item)
        {
            int region = npc.Region;
            if (region < 0)
            {
                NotificationUi.instance.ShowNotification("The watchmaker can't take your money here");
                return;
            }
            int price = Price(item, region);
            if (PlayerGold.currency[region] < price)
            {
                NotificationUi.instance.ShowNotification("Not enough money");
                return;
            }
            if (!BenchPose(npc.island, out Vector3 pos, out Quaternion rot)) return;

            UISoundPlayer.instance.PlayGoldSound();
            PlayerGold.currency[region] -= price;
            MoneyNotification.instance.PlayNotif(-price, region);
            try { DayLogs.instance.dayLogs[region].LogTransaction(-price, item); } catch (Exception) { }
            BuyItemUI.instance.DeactivateUI();

            if (item.held != null) item.held.DropItem();
            var job = item.gameObject.GetComponent<RepairJob>() ?? item.gameObject.AddComponent<RepairJob>();
            job.island = npc.island;
            job.readyAt = NavMath.NowAbs + WorkHours;

            // Вычисляем наклон: часы кладем штатно, а компас доворачиваем на -90 по X, чтобы лег плашмя
            Quaternion layRot = rot;
            if (item is ShipItemCompass)
            {
                layRot = rot * Quaternion.Euler(-90f, 0f, 0f);
            }

            job.Lay(pos, layRot);

            if (item is ModItemPocketWatch w)
            {
                w.Regulate();
                NavJournal.WatchRegulated();
            }
            else if (ChronoState.IsChrono(item))
            {
                ChronoState cs = ChronoState.Of(item);
                float rate = cs.Regulate();
                NavJournal.Certificate(cs.serial, NavMath.NowAbs, rate);
                LiftOntoBench(job, item, pos, layRot);
            }

            NotificationUi.instance.ShowNotification("The watchmaker will have it ready at " + LocalClock(job.readyAt));
            Debug.Log("RealisticSkies: watchmaker on island " + npc.island + " took " + item.name + " for " + price + ", ready at " + job.readyAt.ToString("F2", CultureInfo.InvariantCulture));
        }

        /// <summary>A chronometer is laid on the bench by its body's bottom, not by its middle.</summary>
        internal static void LiftOntoBench(RepairJob job, ShipItem item, Vector3 pos, Quaternion layRot)
        {
            if (!ChronoState.IsChrono(item)) return;
            bool any = false;
            Bounds wb = default(Bounds);
            foreach (Renderer r in item.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                if (!any) { wb = r.bounds; any = true; }
                else wb.Encapsulate(r.bounds);
            }
            // Приподнимаем на толщину корпуса, чтобы предмет лежал строго на крышке стола
            if (any) job.Lay(pos + Vector3.up * (pos.y - wb.min.y + 0.002f), layRot);
        }

        /// <summary>An absolute time (game hours) as the port's clock shows it.</summary>
        internal static string LocalClock(double abs)
        {
            double offset = Sun.sun != null ? Sun.sun.localTime - Sun.sun.globalTime : 0;
            double h = (abs + offset) % 24.0;
            if (h < 0) h += 24.0;
            int m = (int)Math.Round(h * 60.0) % (24 * 60);
            return (m / 60).ToString("00") + ":" + (m % 60).ToString("00");
        }

        // ---------------- placing by hand ----------------

        private static string lastKind;
        private static int lastIsland = -1;

        private static bool Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        internal static bool Ctrl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        private static void Keys()
        {
            if (Input.GetKeyDown(Plugin.configWatchmakerPickKey.Value.MainKey)) Pick();
            else if (Input.GetKeyDown(Plugin.configWatchmakerPlaceKey.Value.MainKey)) Place(Npc);
            else if (Input.GetKeyDown(Plugin.configWatchmakerStallKey.Value.MainKey)) Place(Stall);
            else if (Input.GetKeyDown(Plugin.configWatchmakerTurnKey.Value.MainKey)) Turn();
            else if (Ctrl && Input.GetKeyDown(Plugin.configShopPlaceKey.Value.MainKey)) PlaceBench();
        }

        private static bool Look(out RaycastHit hit, bool triggers)
        {
            hit = default(RaycastHit);
            Camera cam = Camera.main;
            if (cam == null) return false;
            RaycastHit[] hits = Physics.RaycastAll(cam.transform.position, cam.transform.forward, 15f, ~0,
                triggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in hits)
            {
                if (h.distance < 0.35f) continue;                       // the player's own colliders
                if (h.collider.GetComponentInParent<PickupableItem>() != null) continue;
                if (h.collider.isTrigger && h.collider.GetComponentInParent<Shopkeeper>() == null) continue;
                hit = h;
                return true;
            }
            return false;
        }

        private static void Pick()
        {
            if (!Look(out RaycastHit hit, true))
            {
                Notify("nothing in sight");
                return;
            }
            Shopkeeper keeper = hit.collider.GetComponentInParent<Shopkeeper>();
            if (keeper == null)
            {
                // a trader may have colliders of his own without being the hit's parent: the nearest
                foreach (Shopkeeper k in UnityEngine.Object.FindObjectsOfType<Shopkeeper>())
                {
                    if (k.GetComponentInParent<WatchmakerNpc>() != null) continue;
                    if (Vector3.Distance(k.transform.position, hit.point) < 1.5f) { keeper = k; break; }
                }
            }
            // or anyone in the street: a townsman's figure
            Transform person = Townsfolk.FigureNear(keeper != null ? keeper.transform.position : hit.point, keeper != null ? 2.5f : 1.2f);
            if (person != null && person.GetComponentInParent<WatchmakerNpc>() == null)
            {
                string r = Townsfolk.Capture(person);
                if (r != null)
                {
                    Plugin.configWatchmakerModel.Value = r;
                    Plugin.instance.Config.Save();
                    nextTry = 0f;
                    Notify("watchmaker model: " + person.name);
                    Debug.Log("RealisticSkies: watchmaker model from " + Path(person) + ": " + r);
                    return;
                }
            }
            if (keeper != null)
            {
                Notify("can't find the trader's figure near " + keeper.name);
                return;
            }

            // a stall: the hit object, grown up its parents while they stay small
            Transform t = hit.collider.transform;
            if (t.GetComponentInParent<WatchmakerNpc>() != null) { Notify("that is the watchmaker himself"); return; }
            while (t.parent != null && t.parent.GetComponent<IslandSceneryScene>() == null
                   && t.parent.GetComponentInChildren<Shopkeeper>() == null && Size(t.parent) < 3.5f)
            {
                t = t.parent;
            }
            string s = Recipe.Capture(t, false);
            if (s.Length == 0) { Notify("nothing to copy in " + t.name); return; }
            Plugin.configWatchmakerStall.Value = s;
            Plugin.instance.Config.Save();
            Notify("stall model: " + t.name + " (" + Size(t).ToString("F1") + " m)");
            Debug.Log("RealisticSkies: watchmaker's stall model from " + Path(t) + ": " + s);
        }

        private static float Size(Transform t)
        {
            bool any = false;
            Bounds b = default(Bounds);
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer)) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any ? b.size.magnitude : 0f;
        }

        private static string Path(Transform t)
        {
            string p = t.name;
            for (Transform u = t.parent; u != null; u = u.parent) p = u.name + "/" + p;
            return p;
        }

        private static bool NearestShop(Vector3 at, out int island, out Transform shop)
        {
            island = -1;
            shop = null;
            float best = float.MaxValue;
            foreach (KeyValuePair<int, Transform> kv in Shops)
            {
                if (kv.Value == null) continue;
                float d = Vector3.Distance(kv.Value.position, at);
                if (d < best) { best = d; shop = kv.Value; island = kv.Key; }
            }
            if (shop == null || best > 300f)
            {
                Notify("no capital nearby (the mod's shop must be within 300 m)");
                return false;
            }
            return true;
        }

        private static void Place(string kind)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            if (Shift)
            {
                if (!NearestShop(cam.transform.position, out int isl, out Transform _)) return;
                Store(kind, isl, null);
                Notify(kind + " taken away from island " + isl);
                return;
            }
            if ((kind == Npc ? NpcModel : StallModel).Length == 0)
            {
                Notify("pick a " + (kind == Npc ? "trader" : "stall") + " model first (F9)");
                return;
            }
            if (!Look(out RaycastHit hit, false))
            {
                Notify("look at the ground where it should stand");
                return;
            }
            if (!NearestShop(hit.point, out int island, out Transform shop)) return;
            Vector3 face = cam.transform.position - hit.point;
            face.y = 0f;
            if (face.sqrMagnitude < 1e-4f) face = -cam.transform.forward;
            Quaternion world = Quaternion.LookRotation(face.normalized, Vector3.up);
            // the market stall's open side is its -z: that side to you, the watchmaker's side away
            if (kind == Stall) world = world * Quaternion.Euler(0f, 180f, 0f);
            var spot = new ShopSpots.Spot
            {
                pos = shop.InverseTransformPoint(hit.point),
                rot = Quaternion.Inverse(shop.rotation) * world,
            };
            Store(kind, island, spot);
            lastKind = kind;
            lastIsland = island;
            Notify(kind + " placed on island " + island);
        }

        private static void Turn()
        {
            if (lastKind == null || !TryGet(lastKind, lastIsland, out ShopSpots.Spot s))
            {
                Notify("place something first");
                return;
            }
            float step = Shift ? -15f : 15f;
            Quaternion world = Quaternion.AngleAxis(step, Vector3.up);
            if (Shops.TryGetValue(lastIsland, out Transform shop) && shop != null)
            {
                // about the world's up, in shop space
                s.rot = Quaternion.Inverse(shop.rotation) * world * shop.rotation * s.rot;
            }
            else
            {
                s.rot = world * s.rot;
            }
            Store(lastKind, lastIsland, s);
        }

        private static void PlaceBench()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            ModItemPocketWatch best = null;
            float bestScore = float.MaxValue;
            foreach (ModItemPocketWatch w in UnityEngine.Object.FindObjectsOfType<ModItemPocketWatch>())
            {
                if (w.held != null) continue;
                Vector3 d = w.transform.position - cam.transform.position;
                float dist = d.magnitude;
                if (dist > 6f) continue;
                float off = Vector3.Angle(cam.transform.forward, d);
                if (off > 25f) continue;
                float score = off + dist * 2f;
                if (score < bestScore) { bestScore = score; best = w; }
            }
            if (best == null)
            {
                Notify("look at a pocket watch lying where the watchmaker should lay watches");
                return;
            }
            if (!NearestShop(best.transform.position, out int island, out Transform shop)) return;
            Store(Bench, island, new ShopSpots.Spot
            {
                pos = shop.InverseTransformPoint(best.transform.position),
                rot = Quaternion.Inverse(shop.rotation) * best.transform.rotation,
            });
            Notify("the watchmaker's bench on island " + island + " stored");
        }

        // ---------------- the spots in the config ----------------

        private static string cachedText;
        private static Dictionary<string, ShopSpots.Spot> cached = new Dictionary<string, ShopSpots.Spot>();

        private static bool TryGet(string kind, int island, out ShopSpots.Spot spot)
        {
            if (Parse(Plugin.configWatchmakerSpots.Value).TryGetValue(kind + "@" + island, out spot)) return true;
            if (Baked.TryGetValue(kind, out Dictionary<int, ShopSpots.Spot> d) && d.TryGetValue(island, out spot)) return true;
            spot = default(ShopSpots.Spot);
            return false;
        }

        private static Dictionary<string, ShopSpots.Spot> Parse(string text)
        {
            text = text ?? "";
            if (text == cachedText) return cached;
            var result = new Dictionary<string, ShopSpots.Spot>();
            foreach (string entry in text.Split(';'))
            {
                string e = entry.Trim();
                if (e.Length == 0) continue;
                try
                {
                    int colon = e.IndexOf(':');
                    string[] head = e.Substring(0, colon).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int island = int.Parse(head[1], CultureInfo.InvariantCulture);
                    string[] parts = e.Substring(colon + 1).Split('/');
                    float[] p = Recipe.Floats(parts[0], 3), q = Recipe.Floats(parts[1], 4);
                    result[head[0] + "@" + island] = new ShopSpots.Spot { pos = new Vector3(p[0], p[1], p[2]), rot = new Quaternion(q[0], q[1], q[2], q[3]).normalized };
                }
                catch (Exception)
                {
                    Debug.LogWarning("RealisticSkies: can't read watchmaker spot \"" + e + "\"");
                }
            }
            cachedText = text;
            cached = result;
            return result;
        }

        private static string Format(string kind, int island, ShopSpots.Spot s)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1}: {2:F3}, {3:F3}, {4:F3} / {5:F4}, {6:F4}, {7:F4}, {8:F4}",
                kind, island, s.pos.x, s.pos.y, s.pos.z, s.rot.x, s.rot.y, s.rot.z, s.rot.w);
        }

        private static void Store(string kind, int island, ShopSpots.Spot? spot)
        {
            var all = new Dictionary<string, ShopSpots.Spot>(Parse(Plugin.configWatchmakerSpots.Value));
            if (spot.HasValue) all[kind + "@" + island] = spot.Value;
            else all.Remove(kind + "@" + island);
            var keys = new List<string>(all.Keys);
            keys.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder();
            foreach (string k in keys)
            {
                int at = k.IndexOf('@');
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(Format(k.Substring(0, at), int.Parse(k.Substring(at + 1), CultureInfo.InvariantCulture), all[k]));
            }
            Plugin.configWatchmakerSpots.Value = sb.ToString();
            Plugin.instance.Config.Save();
            nextTry = 0f;               // rebuild at once
            if (spot.HasValue) Debug.Log("RealisticSkies: watchmaker spot  " + Format(kind, island, spot.Value));
        }

        internal static void Notify(string msg)
        {
            Debug.Log("RealisticSkies: watchmaker placement: " + msg);
            try { NotificationUi.instance.ShowNotification(msg); } catch (Exception) { }
        }
    }

    /// <summary>
    /// A copy of something in the game's scenery, kept as a recipe of names: meshes and materials by
    /// name (found again among the loaded assets), poses relative to the copied object, and its
    /// colliders. "mesh|name#verts|mat,mat|p|q|s; box|trigger|p|q|s|cx,cy,cz,sx,sy,sz; ..."
    /// </summary>
    internal static class Recipe
    {
        internal static float[] Floats(string s, int n)
        {
            string[] t = s.Split(',');
            if (t.Length != n) throw new FormatException();
            var f = new float[n];
            for (int i = 0; i < n; i++) f[i] = float.Parse(t[i].Trim(), CultureInfo.InvariantCulture);
            return f;
        }

        private static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R}", v.x, v.y, v.z);
        private static string Q(Quaternion q) => string.Format(CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R},{3:R}", q.x, q.y, q.z, q.w);
        private static string F(float f) => f.ToString("R", CultureInfo.InvariantCulture);
        private static string Clean(string n) => (n ?? "").Replace(" (Instance)", "").Replace("|", "").Replace(";", "").Replace(",", "");
        private static string MeshKey(Mesh m) => Clean(m.name) + "#" + m.vertexCount;

        internal static string Capture(Transform root, bool withTriggers)
        {
            var sb = new StringBuilder();
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>())
            {
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || mr == null || !mr.enabled) continue;
                var mats = new List<string>();
                foreach (Material m in mr.sharedMaterials) mats.Add(m != null ? Clean(m.name) : "");
                Pose(mf.transform, root, out Vector3 p, out Quaternion q, out Vector3 s);
                if (sb.Length > 0) sb.Append("; ");
                sb.Append("mesh|").Append(MeshKey(mf.sharedMesh)).Append('|').Append(string.Join(",", mats.ToArray()))
                  .Append('|').Append(V(p)).Append('|').Append(Q(q)).Append('|').Append(V(s));
            }
            foreach (Collider c in root.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger && !withTriggers) continue;
                Pose(c.transform, root, out Vector3 p, out Quaternion q, out Vector3 s);
                string head = "|" + (c.isTrigger ? "1" : "0") + "|" + V(p) + "|" + Q(q) + "|" + V(s) + "|";
                string e = null;
                if (c is BoxCollider bc) e = "box" + head + V(bc.center) + "," + V(bc.size);
                else if (c is SphereCollider sc) e = "sphere" + head + V(sc.center) + "," + F(sc.radius);
                else if (c is CapsuleCollider cc) e = "capsule" + head + V(cc.center) + "," + F(cc.radius) + "," + F(cc.height) + "," + cc.direction;
                else if (c is MeshCollider mc && mc.sharedMesh != null) e = "meshcol" + head + MeshKey(mc.sharedMesh) + "," + (mc.convex ? "1" : "0");
                if (e == null) continue;
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(e).Append('|').Append(c.gameObject.layer);
            }
            return sb.ToString();
        }

        /// <summary>
        /// A part's pose in an upright frame at the copied object: its place and its turn about the
        /// vertical only. Models made lying down (stood up in the scene by their own rotation) then
        /// come out standing too.
        /// </summary>
        private static void Pose(Transform t, Transform root, out Vector3 p, out Quaternion q, out Vector3 s)
        {
            Vector3 right = Vector3.ProjectOnPlane(root.right, Vector3.up);
            if (right.sqrMagnitude < 1e-4f) right = Vector3.ProjectOnPlane(-Vector3.Cross(root.forward, Vector3.up), Vector3.up);
            Quaternion frame = Quaternion.LookRotation(Vector3.Cross(right.normalized, Vector3.up), Vector3.up);
            p = Quaternion.Inverse(frame) * (t.position - root.position);
            q = Quaternion.Inverse(frame) * t.rotation;
            s = t.lossyScale;
        }

        private static float Nz(float f) => Mathf.Abs(f) < 1e-6f ? 1f : f;

        private static Dictionary<string, Mesh> meshes;
        private static Dictionary<string, Material> materials;
        private static int lookupFrame = -1000;

        private static void Index()
        {
            if (meshes != null && Time.frameCount - lookupFrame < 60) return;
            lookupFrame = Time.frameCount;
            meshes = new Dictionary<string, Mesh>();
            foreach (Mesh m in Resources.FindObjectsOfTypeAll<Mesh>())
            {
                if (m == null) continue;
                string k = MeshKey(m);
                if (!meshes.ContainsKey(k)) meshes[k] = m;
            }
            materials = new Dictionary<string, Material>();
            foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (m == null) continue;
                string k = Clean(m.name);
                // prefer the asset to someone's instance of it
                if (!materials.ContainsKey(k) || m.name.IndexOf("(Instance)", StringComparison.Ordinal) < 0) materials[k] = m;
            }
        }

        /// <summary>Builds the copy, or null while some of its meshes aren't loaded.</summary>
        internal static GameObject Build(string recipe, string name, bool person)
        {
            Index();
            string[] entries = recipe.Split(';');
            // all the meshes first: no half-built copies
            foreach (string raw in entries)
            {
                string[] f = raw.Trim().Split('|');
                if (f[0] == "mesh" && !meshes.ContainsKey(f[1])) return null;
            }
            var root = new GameObject(name);
            bool anyTrigger = false;
            foreach (string raw in entries)
            {
                string e = raw.Trim();
                if (e.Length == 0) continue;
                try
                {
                    string[] f = e.Split('|');
                    if (f[0] == "mesh")
                    {
                        var part = Child(root.transform, f[1], f[3], f[4], f[5]);
                        part.AddComponent<MeshFilter>().sharedMesh = meshes[f[1]];
                        var mr = part.AddComponent<MeshRenderer>();
                        var mats = new List<Material>();
                        foreach (string mn in f[2].Split(','))
                        {
                            materials.TryGetValue(mn, out Material m);
                            mats.Add(m);
                        }
                        mr.sharedMaterials = mats.ToArray();
                        continue;
                    }
                    bool trig = f[1] == "1";
                    var holder = Child(root.transform, f[0], f[2], f[3], f[4]);
                    string[] a = f[5].Split(',');
                    float G(int i) => float.Parse(a[i], CultureInfo.InvariantCulture);
                    Collider c = null;
                    if (f[0] == "box")
                    {
                        var bc = holder.AddComponent<BoxCollider>();
                        bc.center = new Vector3(G(0), G(1), G(2));
                        bc.size = new Vector3(G(3), G(4), G(5));
                        c = bc;
                    }
                    else if (f[0] == "sphere")
                    {
                        var sc = holder.AddComponent<SphereCollider>();
                        sc.center = new Vector3(G(0), G(1), G(2));
                        sc.radius = G(3);
                        c = sc;
                    }
                    else if (f[0] == "capsule")
                    {
                        var cc = holder.AddComponent<CapsuleCollider>();
                        cc.center = new Vector3(G(0), G(1), G(2));
                        cc.radius = G(3);
                        cc.height = G(4);
                        cc.direction = int.Parse(a[5], CultureInfo.InvariantCulture);
                        c = cc;
                    }
                    else if (f[0] == "meshcol")
                    {
                        if (!meshes.TryGetValue(a[0], out Mesh m)) { UnityEngine.Object.Destroy(holder); continue; }
                        var mc = holder.AddComponent<MeshCollider>();
                        mc.convex = a.Length > 1 && a[1] == "1";
                        mc.sharedMesh = m;
                        c = mc;
                    }
                    if (c != null)
                    {
                        c.isTrigger = trig;
                        anyTrigger |= trig;
                        if (f.Length > 6) holder.layer = int.Parse(f[6], CultureInfo.InvariantCulture);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("RealisticSkies: can't build \"" + e + "\": " + ex.Message);
                }
            }
            if (!person) Recentre(root.transform);
            if (person && !anyTrigger)
            {
                // where you hold an item out to him
                var t = new GameObject("reach");
                t.transform.SetParent(root.transform, false);
                var cap = t.AddComponent<CapsuleCollider>();
                cap.isTrigger = true;
                cap.center = new Vector3(0f, 1.1f, 0.3f);
                cap.radius = 0.6f;
                cap.height = 2.2f;
            }
            return root;
        }

        /// <summary>Puts the root at the middle of the copy's foot, so it stands and turns about where you look.</summary>
        private static void Recentre(Transform root)
        {
            bool any = false;
            Bounds b = default(Bounds);
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                Matrix4x4 m = Matrix4x4.TRS(mf.transform.localPosition, mf.transform.localRotation, mf.transform.localScale);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 w = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(w, Vector3.zero); any = true; }
                    else b.Encapsulate(w);
                }
            }
            if (!any) return;
            Vector3 shift = new Vector3(b.center.x, b.min.y, b.center.z);
            foreach (Transform c in root) c.localPosition -= shift;
        }

        private static GameObject Child(Transform root, string name, string p, string q, string s)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            float[] pp = Floats(p, 3), qq = Floats(q, 4), ss = Floats(s, 3);
            go.transform.localPosition = new Vector3(pp[0], pp[1], pp[2]);
            go.transform.localRotation = new Quaternion(qq[0], qq[1], qq[2], qq[3]).normalized;
            go.transform.localScale = new Vector3(ss[0], ss[1], ss[2]);
            return go;
        }
    }

    /// <summary>The watchmaker himself: keeps a trader's hours and offers to regulate a watch held out to him.</summary>
    internal class WatchmakerNpc : MonoBehaviour
    {
        internal int island;
        private Shopkeeper keeper;          // a dormant one, only for the game's buy/sell window
        private Renderer[] renderers;
        private Collider[] colliders;
        private bool shown = true;
        private float settleUntil = float.MaxValue;
        private int region = -1;

        internal Shopkeeper Keeper => keeper;
        internal bool Present => shown && OpenNow;

        private static bool OpenNow
        {
            get
            {
                if (Sun.sun == null) return true;
                float t = Sun.sun.localTime;
                return t >= Watchmaker.Opens && t < Watchmaker.Closes;
            }
        }

        internal int Region
        {
            get
            {
                if (region < 0) FindRegion();
                return region;
            }
        }

        private void Awake()
        {
            // where you hold a watch out to him (on the layer the traders' own reach is on)
            var reach = new GameObject("reach");
            reach.transform.SetParent(transform, false);
            reach.layer = 2;
            var sph = reach.AddComponent<SphereCollider>();
            sph.isTrigger = true;
            sph.center = new Vector3(0f, 1.1f, 0.4f);
            sph.radius = 1.4f;
            reach.AddComponent<WatchmakerReach>().npc = this;
            var k = new GameObject("keeper");
            k.SetActive(false);
            k.transform.SetParent(transform, false);
            keeper = k.AddComponent<Shopkeeper>();
            keeper.enabled = false;         // its Start wants a home and an island it doesn't have
            k.AddComponent<WatchmakerDesk>().npc = this;
            k.SetActive(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
        }

        private void Start()
        {
            settleUntil = Time.time + 5f;
            FindRegion();
        }

        private void FindRegion()
        {
            Shopkeeper best = null;
            float bd = float.MaxValue;
            foreach (Shopkeeper s in FindObjectsOfType<Shopkeeper>())
            {
                if (s == keeper) continue;
                Region r = Traverse.Create(s).Field("parentRegion").GetValue<Region>();
                if (r == null) continue;
                float d = Vector3.Distance(s.transform.position, transform.position);
                if (d < bd) { bd = d; best = s; }
            }
            if (best == null || bd > 500f) return;
            Region reg = Traverse.Create(best).Field("parentRegion").GetValue<Region>();
            Traverse.Create(keeper).Field("parentRegion").SetValue(reg);
            region = (int)reg.portRegion;
        }

        private void Update()
        {
            // at night he goes home, as the traders do, but not before your eyes
            bool open = OpenNow;
            if (Time.time < settleUntil)
            {
                // The figure is copied from a townsman as he is at that moment; just after a load
                // the game may have his parts switched off, and the copy kept them off till the
                // watchmaker next went home and came back. Put his state on him, whatever came along.
                shown = open;
                foreach (Renderer r in renderers) if (r != null && r.enabled != open) r.enabled = open;
                foreach (Collider c in colliders) if (c != null && c.enabled != open) c.enabled = open;
                return;
            }
            if (open == shown) return;
            if (!open)
            {
                foreach (Renderer r in renderers) if (r != null && r.isVisible) return;
            }
            shown = open;
            foreach (Renderer r in renderers) if (r != null) r.enabled = open;
            foreach (Collider c in colliders) if (c != null) c.enabled = open;
            if (!open && BuyItemUI.instance != null && BuyItemUI.instance.activeShopkeeperIs(keeper)) BuyItemUI.instance.DeactivateUI();
        }

        internal void Enter(Collider other)
        {
            if (!Present) return;
            ShipItem item = other.GetComponent<ShipItem>();
            if (item == null) return;
            BuyItemUI ui = BuyItemUI.instance;
            if (ui.activeItem == null && item.held != null)
            {
                bool takes = (item is ModItemPocketWatch && Mode.PocketWatches != Keeping.Dead) || (ChronoState.IsChrono(item) && Mode.Chronometers == Keeping.Imperfect);
                if (!takes || !item.sold || item.GetComponent<RepairJob>() != null) return;
                int reg = Region;
                if (reg < 0) return;
                int price = Watchmaker.Price(item, reg);
                ui.ActivateUI(item, keeper, price.ToString(), true);
                if (ui.activeItem == item)
                {
                    ui.buyText.text = "Regulate\n" + item.name + "?";
                    ui.buttonText.text = "Regulate (" + price + ")";
                }
            }
            else if (ui.activeItem == item && item.held == null)
            {
                ui.DeactivateUI();
            }
        }

        internal void Exit(Collider other)
        {
            ShipItem item = other.GetComponent<ShipItem>();
            if (item != null && BuyItemUI.instance.activeItem == item && BuyItemUI.instance.activeShopkeeperIs(keeper)) BuyItemUI.instance.DeactivateUI();
        }

        private void OnTriggerEnter(Collider other) => Enter(other);
        private void OnTriggerExit(Collider other) => Exit(other);

        private void OnDestroy()
        {
            if (Watchmaker.Npcs.TryGetValue(island, out WatchmakerNpc n) && n == this) Watchmaker.Npcs.Remove(island);
        }
    }

    /// <summary>
    /// The townsfolk are the game's modular figures (PsychoticLab's CharacterCustomizer): one rig with
    /// every head, torso, hat and so on, and a set of numbers that says which are worn. A trader is
    /// copied as those numbers and his material; the copy is made from any figure loaded at the time
    /// (all capitals have some) with the numbers put on before it dresses itself.
    /// "char|figure name|field=value,field=value,...|material"
    /// </summary>
    internal static class Townsfolk
    {
        /// <summary>The figure (its outermost part that is still the figure) nearest a point.</summary>
        internal static Transform FigureNear(Vector3 at, float within)
        {
            Transform best = null;
            float bd = within;
            foreach (PsychoticLab.CharacterCustomizer c in UnityEngine.Object.FindObjectsOfType<PsychoticLab.CharacterCustomizer>())
            {
                Transform root = Root(c);
                float d = Vector3.Distance(Flat(root.position), Flat(at));
                if (d < bd) { bd = d; best = root; }
            }
            return best;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static Transform Root(PsychoticLab.CharacterCustomizer c)
        {
            Transform t = c.transform;
            NPCAnimations anims = c.GetComponentInParent<NPCAnimations>();
            if (anims != null) t = anims.transform;
            if (t.parent != null && t.parent.GetComponent<NPCPlayerCol>() != null && t.parent.GetComponent<Shopkeeper>() == null) t = t.parent;
            return t;
        }

        internal static string Capture(Transform figure)
        {
            var c = figure.GetComponentInChildren<PsychoticLab.CharacterCustomizer>(true);
            if (c == null) return null;
            var sb = new StringBuilder();
            foreach (System.Reflection.FieldInfo f in typeof(PsychoticLab.CharacterCustomizer).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (f.FieldType != typeof(int) && f.FieldType != typeof(bool)) continue;
                if (sb.Length > 0) sb.Append(',');
                object v = f.GetValue(c);
                sb.Append(f.Name).Append('=').Append(v is bool b ? (b ? "1" : "0") : ((int)v).ToString(CultureInfo.InvariantCulture));
            }
            string mat = c.mat != null ? c.mat.name.Replace(" (Instance)", "") : "";
            var parts = new List<string>();
            foreach (Component k in figure.GetComponentsInChildren<Component>(true)) if (!(k is Transform)) parts.Add(k.GetType().Name);
            Debug.Log("RealisticSkies: figure " + figure.name + ": " + figure.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length + " skinned parts, components: " + string.Join(", ", Distinct(parts)));
            return "char|" + figure.name.Replace("|", "") + "|" + sb + "|" + mat.Replace("|", "");
        }

        private static string[] Distinct(List<string> l)
        {
            var seen = new HashSet<string>();
            var r = new List<string>();
            foreach (string s in l) if (seen.Add(s)) r.Add(s);
            return r.ToArray();
        }

        /// <summary>Dresses a copy of a loaded figure as the recipe says; null while there is none.</summary>
        internal static GameObject Build(string recipe, string name, string look)
        {
            string[] f = recipe.Split('|');
            if (f.Length < 4) return null;
            Transform source = null;
            foreach (PsychoticLab.CharacterCustomizer c in UnityEngine.Object.FindObjectsOfType<PsychoticLab.CharacterCustomizer>())
            {
                if (c.GetComponentInParent<WatchmakerNpc>() != null) continue;
                if (Traverse.Create(c).Field("bodyPartsRoot").GetValue<GameObject>() == null) continue;
                Transform r = Root(c);
                if (source == null || (f[1].Length > 0 && r.name == f[1])) source = r;
                if (f[1].Length > 0 && r.name == f[1]) break;
            }
            if (source == null) return null;

            var root = new GameObject(name);
            GameObject fig = UnityEngine.Object.Instantiate(source.gameObject, root.transform, false);
            fig.name = f[1];
            fig.transform.localPosition = Vector3.zero;
            fig.transform.localRotation = Quaternion.identity;
            // no trader's or shop's own workings in the copy
            foreach (Shopkeeper k in fig.GetComponentsInChildren<Shopkeeper>(true)) UnityEngine.Object.Destroy(k);
            foreach (ShopArea a in fig.GetComponentsInChildren<ShopArea>(true)) UnityEngine.Object.Destroy(a);

            var cust = fig.GetComponentInChildren<PsychoticLab.CharacterCustomizer>(true);
            if (cust != null)
            {
                Type t = typeof(PsychoticLab.CharacterCustomizer);
                foreach (string kv in f[2].Split(','))
                {
                    int eq = kv.IndexOf('=');
                    if (eq <= 0) continue;
                    System.Reflection.FieldInfo fi = t.GetField(kv.Substring(0, eq));
                    if (fi == null) continue;
                    int v = int.Parse(kv.Substring(eq + 1), CultureInfo.InvariantCulture);
                    if (fi.FieldType == typeof(bool)) fi.SetValue(cust, v != 0);
                    else if (fi.FieldType == typeof(int)) fi.SetValue(cust, v);
                }
                if (f[3].Length > 0)
                {
                    foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
                    {
                        if (m != null && m.name == f[3]) { cust.mat = m; break; }
                    }
                }
            }
            if (look.Length > 0) fig.AddComponent<TownsfolkLook>().look = look;
            return root;
        }
    }

    internal static class BuyItemUIExt
    {
        internal static bool activeShopkeeperIs(this BuyItemUI ui, Shopkeeper k)
        {
            return Traverse.Create(ui).Field("activeShopkeeper").GetValue<Shopkeeper>() == k;
        }
    }

    internal class WatchmakerReach : MonoBehaviour
    {
        internal WatchmakerNpc npc;
        private void OnTriggerEnter(Collider other) { if (npc != null) npc.Enter(other); }
        private void OnTriggerExit(Collider other) { if (npc != null) npc.Exit(other); }
    }

    /// <summary>Marks the watchmaker's dormant shopkeeper, whose "buy" button means "regulate".</summary>
    internal class WatchmakerDesk : MonoBehaviour
    {
        internal WatchmakerNpc npc;
    }

    /// <summary>A watch at the watchmaker's: lies on his bench, can't be taken till it's done and he's there.</summary>
    internal class RepairJob : MonoBehaviour
    {
        internal int island;
        internal double readyAt;
        private Vector3 pos;
        private Quaternion rot;
        private bool laid;

        internal void Lay(Vector3 p, Quaternion r)
        {
            // Kept in real (unshifted) coordinates. The floating origin moves the world's children,
            // not the world itself, so a place kept in the space of the parent (the world, or the
            // scene's root a loaded item hangs at) would pull the watch back each time the world
            // shifts, as it does at once after a load, and leave it kilometres from the stall.
            pos = p - Offset;
            rot = r;
            laid = true;
            transform.position = p;
            transform.rotation = r;
            Hold();
        }

        private float rescueAt;

        /// <summary>
        /// A watch that strayed from the bench (older versions let it stay behind when the world
        /// shifted, so it ended up far out at sea): once the player is at a watchmaker's, it is back on
        /// his bench. One whose town isn't known goes to the watchmaker the player is at.
        /// </summary>
        private void Rescue()
        {
            if (Time.time < rescueAt) return;
            rescueAt = Time.time + 2f;
            ShipItem it = GetComponent<ShipItem>();
            if (it == null || it.held != null) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            int here = Watchmaker.NearestTown(cam.transform.position);
            if (here < 0) return;
            int town = island;
            if (town < 0)
            {
                town = Watchmaker.NearestTown(transform.position);
                if (town < 0) town = here;
            }
            if (town != here) return;               // its own watchmaker isn't where the player is
            if (!Watchmaker.BenchPose(town, out Vector3 p, out Quaternion r)) return;
            if (Vector3.Distance(transform.position, p) < 3f) { island = town; return; }
            Debug.Log("RealisticSkies: watchmaker on island " + town + " finds " + name + " " + Vector3.Distance(transform.position, p).ToString("F0") + " m from his bench and lays it back");
            island = town;
            Quaternion lay = it is ShipItemCompass ? r * Quaternion.Euler(-90f, 0f, 0f) : r;
            Lay(p, lay);
            Watchmaker.LiftOntoBench(this, it, p, lay);
        }

        private void Update() => Rescue();

        private static Vector3 Offset => FloatingOriginManager.instance != null ? FloatingOriginManager.instance.outCurrentOffset : Vector3.zero;

        /// <summary>
        /// The game moves an item to where its physics body (a separate object) lies, and drops a far
        /// one from the world: while at the watchmaker's the body is put to sleep with the item on it.
        /// </summary>
        private void Hold()
        {
            ShipItem it = GetComponent<ShipItem>();
            if (it == null || it.held != null) return;
            if (laid)
            {
                transform.position = pos + Offset;
                transform.rotation = rot;
            }
            ItemRigidbody irb = it.GetItemRigidbody();
            if (irb == null) return;
            if (irb.enabled) irb.enabled = false;
            irb.transform.position = transform.position;
            irb.transform.rotation = transform.rotation;
            Rigidbody body = irb.GetBody();
            if (body != null)
            {
                body.isKinematic = true;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            foreach (Collider c in irb.GetComponentsInChildren<Collider>()) if (c.enabled) c.enabled = false;
        }

        private void FixedUpdate() => Hold();
        private void LateUpdate() => Hold();

        internal bool Ready => NavMath.NowAbs >= readyAt;

        /// <summary>Whether it may be taken now; says why not.</summary>
        internal bool MayTake()
        {
            if (island < 0) island = Watchmaker.NearestTown(transform.position);
            if (!Watchmaker.Present(island))
            {
                NotificationUi.instance.ShowNotification("The watchmaker is away: he is here from 7:00 to 18:00");
                return false;
            }
            if (!Ready)
            {
                NotificationUi.instance.ShowNotification("The watchmaker is still at work: ready at " + Watchmaker.LocalClock(readyAt));
                return false;
            }
            return true;
        }

        internal void Done()
        {
            ShipItem it = GetComponent<ShipItem>();
            ItemRigidbody irb = it != null ? it.GetItemRigidbody() : null;
            if (irb != null)
            {
                foreach (Collider c in irb.GetComponentsInChildren<Collider>(true)) c.enabled = true;
                irb.transform.position = transform.position;
                irb.transform.rotation = transform.rotation;
                irb.enabled = true;
                // after a load the body was put to sleep before its Start made its Rigidbody: Start
                // (now that it is enabled) makes it and resets the place itself
                if (irb.GetBody() != null) irb.ResetPos();
            }
            Destroy(this);
        }
    }

    [HarmonyPatch]
    internal static class WatchmakerPatches
    {
        /// <summary>The watchmaker's "buy" (the player selling) is the regulating.</summary>
        [HarmonyPatch(typeof(Shopkeeper), "TryToBuyItem")]
        [HarmonyPrefix]
        private static bool Regulate(Shopkeeper __instance, ShipItem item)
        {
            WatchmakerDesk desk = __instance.GetComponent<WatchmakerDesk>();
            if (desk == null) return true;
            if (desk.npc != null && item != null) Watchmaker.Regulate(desk.npc, item);
            return false;
        }

        [HarmonyPatch(typeof(GoPointer), "PickUpItem")]
        [HarmonyPrefix]
        private static bool Take(PickupableItem item)
        {
            if (item == null) return true;
            RepairJob job = item.GetComponent<RepairJob>();
            if (job == null) return true;
            if (!job.MayTake()) return false;
            job.Done();
            return true;
        }

        [HarmonyPatch(typeof(SaveablePrefab), "PrepareSaveData")]
        [HarmonyPostfix]
        private static void Save(SaveablePrefab __instance, SavePrefabData __result)
        {
            RepairJob job = __instance.GetComponent<RepairJob>();
            if (job == null || __result == null) return;
            if (ChronoState.IsChrono(__instance))
            {
                __result.extraValue4 = (float)job.readyAt;      // the chronometer's own state has the others
                return;
            }
            __result.extraValue0 = (float)job.readyAt;
            __result.extraValue1 = job.island + 1;
        }

        // A chronometer's extra values are all taken by its own state: the town of its job is kept in
        // the save's mod data instead, by the item's instance id. The game writes the mod data after
        // the items and reads it back after them.
        private const string IslandKey = "RealisticSkies.Watchmaker.";

        [HarmonyPatch(typeof(SaveLoadManager), "SaveModData")]
        [HarmonyPrefix]
        private static void SaveIslands()
        {
            try
            {
                if (GameState.modData == null) GameState.modData = new Dictionary<string, string>();
                var old = new List<string>();
                foreach (string k in GameState.modData.Keys) if (k.StartsWith(IslandKey, StringComparison.Ordinal)) old.Add(k);
                foreach (string k in old) GameState.modData.Remove(k);
                foreach (RepairJob job in UnityEngine.Object.FindObjectsOfType<RepairJob>())
                {
                    SaveablePrefab sp = job.GetComponent<SaveablePrefab>();
                    if (sp == null || job.island < 0) continue;
                    GameState.modData[IslandKey + sp.instanceId.ToString(CultureInfo.InvariantCulture)] = job.island.ToString(CultureInfo.InvariantCulture);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: watchmaker: can't save the towns of the watches: " + e);
            }
        }

        [HarmonyPatch(typeof(SaveLoadManager), "LoadModData")]
        [HarmonyPostfix]
        private static void LoadIslands()
        {
            try
            {
                if (GameState.modData == null) return;
                foreach (RepairJob job in UnityEngine.Object.FindObjectsOfType<RepairJob>())
                {
                    if (job.island >= 0) continue;
                    SaveablePrefab sp = job.GetComponent<SaveablePrefab>();
                    if (sp == null) continue;
                    if (GameState.modData.TryGetValue(IslandKey + sp.instanceId.ToString(CultureInfo.InvariantCulture), out string s)
                        && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int isl))
                    {
                        job.island = isl;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: watchmaker: can't read the towns of the watches: " + e);
            }
        }

        [HarmonyPatch(typeof(SaveablePrefab), "Load")]
        [HarmonyPostfix]
        private static void Load(SaveablePrefab __instance, SavePrefabData data)
        {
            if (data == null || __instance.GetComponent<RepairJob>() != null) return;
            int island;
            double readyAt;
            if (ChronoState.IsChrono(__instance) && data.extraValue4 > 0f)
            {
                island = -1;            // found by where it lies
                readyAt = data.extraValue4;
            }
            else if (__instance.GetComponent<ModItemPocketWatch>() != null && data.extraValue1 >= 0.5f)
            {
                island = Mathf.RoundToInt(data.extraValue1) - 1;
                readyAt = data.extraValue0;
            }
            else return;
            var job = __instance.gameObject.AddComponent<RepairJob>();
            job.island = island;
            job.readyAt = readyAt;
            job.Lay(__instance.transform.position, __instance.transform.rotation);
        }
    }
}
