using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Where the mod's own goods lie in each of its shops: the Sea Legends scroll, the two manuals, the sand
    /// glasses, the watch glass, the pocket watch, the journal, the cross-staff, the astrolabe and the nocturnal. A spot is a pose in the shop's own space
    /// (the shopPrefab transform from ShopInfo), per kind of item and per island. Items without a
    /// spot go next to the Star Chart.
    ///
    /// Placing by hand (Debug / Shop Placement on): put the item where it should lie in a shop, look
    /// at it and press the key (F8). The pose goes into the Shop Spots config entry, the shop's own
    /// copy moves there at once, and a ready line for <see cref="Baked"/> goes to the log. Config
    /// spots override the baked ones.
    /// </summary>
    internal static class ShopSpots
    {
        internal const string Scroll = "scroll", Glass30 = "glass30", Glass60 = "glass60", WatchGlass = "watchglass", PocketWatch = "pocketwatch", Journal = "journal", CrossStaff = "crossstaff", Astrolabe = "astrolabe", Nocturnal = "nocturnal", RingDial = "ringdial", TraverseBoard = "traverse", Companion = "companion", Modern = "modern";

        internal struct Spot
        {
            public Vector3 pos;
            public Quaternion rot;
        }

        /// <summary>Hand-placed spots: kind -> island -> pose.</summary>
        private static readonly Dictionary<string, Dictionary<int, Spot>> Baked = new Dictionary<string, Dictionary<int, Spot>>
        {
            {
                Scroll, new Dictionary<int, Spot>
                {
                    { 1, S(-0.912f, 0.770f, 0.611f, -0.0093f, -0.7703f, 0.6375f, -0.0115f) },
                    { 9, S(-0.889f, 0.908f, 0.048f, 0.5550f, 0.4904f, -0.5269f, 0.4169f) },
                    { 15, S(-0.878f, 0.781f, 0.437f, -0.0040f, -0.7739f, 0.6333f, -0.0031f) },
                }
            },
            {
                Glass30, new Dictionary<int, Spot>
                {
                    { 1, S(-0.402f, 0.867f, 0.609f, -0.0028f, 0.9949f, 0.1007f, 0.0035f) },
                    { 9, S(-0.083f, 0.950f, 0.247f, -0.0010f, 0.9976f, 0.0674f, -0.0144f) },
                    { 15, S(-0.679f, 0.899f, 0.309f, -0.0040f, 0.9944f, 0.0993f, -0.0367f) },
                }
            },
            {
                Glass60, new Dictionary<int, Spot>
                {
                    { 1, S(-0.574f, 0.937f, 0.437f, 0.0160f, -0.9863f, -0.0981f, 0.1316f) },
                    { 9, S(0.072f, 0.999f, 0.152f, -0.0031f, -0.9964f, -0.0667f, -0.0518f) },
                    { 15, S(-0.525f, 0.967f, 0.150f, 0.0007f, 0.9950f, 0.0991f, 0.0095f) },
                }
            },
            {
                WatchGlass, new Dictionary<int, Spot>
                {
                    { 1, S(-0.579f, 1.172f, -0.266f, -0.1138f, -0.5023f, -0.0713f, -0.8542f) },
                    { 9, S(-0.371f, 0.145f, 0.701f, -0.0185f, -0.9997f, -0.0058f, -0.0158f) },
                    { 15, S(-1.026f, 1.147f, -0.438f, 0.0822f, 0.4176f, 0.0379f, 0.9041f) },
                }
            },
            {
                PocketWatch, new Dictionary<int, Spot>
                {
                    { 1, S(0.009f, 0.748f, 0.663f, 0.0210f, 0.7738f, -0.6328f, 0.0195f) },
                    { 9, S(-0.627f, 0.909f, -0.268f, -0.0103f, 0.7531f, -0.6578f, -0.0089f) },
                    { 15, S(-1.148f, 0.800f, 0.252f, 0.2202f, 0.7418f, -0.6073f, 0.1801f) },
                }
            },
            {
                Journal, new Dictionary<int, Spot>
                {
                    { 1, S(-1.162f, 0.812f, 0.364f, -0.0108f, 0.9948f, 0.0979f, 0.0269f) },
                    { 9, S(0.065f, 0.917f, -0.261f, 0.0000f, -0.9978f, -0.0661f, 0.0051f) },
                    { 15, S(-0.573f, 0.951f, -0.502f, -0.0637f, 0.7048f, 0.0637f, -0.7037f) },
                }
            },
            {
                CrossStaff, new Dictionary<int, Spot>
                {
                    { 1, S(0.503f, 0.818f, 0.770f, 0.0043f, 0.8920f, -0.4520f, 0.0099f) },
                    { 9, S(-0.008f, 0.829f, 0.508f, 0.0003f, -0.9084f, 0.4180f, 0.0007f) },
                    { 15, S(0.595f, 0.825f, 0.599f, -0.0002f, -0.8110f, 0.5851f, -0.0001f) },
                }
            },
            {
                Astrolabe, new Dictionary<int, Spot>
                {
                    { 1, S(1.333f, 1.743f, -0.080f, -0.0014f, -0.9985f, 0.0467f, -0.0295f) },
                    { 9, S(-0.795f, 0.513f, 0.499f, -0.0001f, 0.9998f, -0.0051f, -0.0170f) },
                    { 15, S(-0.484f, 0.466f, 0.565f, 0.0000f, 1.0000f, -0.0043f, -0.0036f) },
                }
            },
            {
                Nocturnal, new Dictionary<int, Spot>
                {
                    { 1, S(0.629f, 0.753f, 0.627f, 0.5529f, -0.5411f, 0.4458f, 0.4503f) },
                    { 9, S(-0.633f, 0.823f, 0.323f, -0.5429f, -0.5215f, 0.4559f, -0.4747f) },
                    { 15, S(0.856f, 0.756f, 0.440f, -0.5260f, 0.5671f, -0.4647f, -0.4310f) },
                }
            },
            {
                Companion, new Dictionary<int, Spot>
                {
                    { 1, S(0.819f, 0.992f, -0.287f, 0.0071f, 0.7683f, -0.6399f, 0.0097f) },
                    { 9, S(0.904f, 0.889f, 0.016f, 0.5214f, 0.5428f, -0.4744f, 0.4566f) },
                    { 15, S(0.040f, 0.964f, -0.478f, -0.0156f, 0.7333f, -0.6797f, -0.0120f) },
                }
            },
            {
                Modern, new Dictionary<int, Spot>
                {
                    { 1, S(0.061f, 0.992f, -0.335f, -0.0120f, -0.7958f, 0.6053f, -0.0140f) },
                    { 9, S(0.550f, 1.031f, -0.415f, 0.0016f, -0.7902f, 0.6128f, -0.0041f) },
                    { 15, S(0.788f, 0.959f, -0.484f, 0.0021f, 0.7558f, -0.6548f, 0.0018f) },
                }
            },
            {
                RingDial, new Dictionary<int, Spot>
                {
                    { 1, S(-0.174f, 0.765f, 0.611f, 0.0201f, 0.7285f, -0.6845f, 0.0171f) },
                    { 9, S(0.077f, 0.833f, 0.347f, 0.0275f, 0.7148f, -0.6980f, 0.0335f) },
                    { 15, S(-0.099f, 0.770f, 0.460f, 0.5056f, 0.4989f, -0.4605f, 0.5323f) },
                }
            },
            {
                TraverseBoard, new Dictionary<int, Spot>
                {
                    { 1, S(1.259f, 1.772f, 1.007f, -0.0002f, 0.9994f, 0.0063f, 0.0352f) },
                    { 9, S(1.428f, 1.783f, 1.022f, 0.0000f, 0.9998f, 0.0223f, -0.0012f) },
                    { 15, S(0.910f, 2.730f, 0.177f, 0.0002f, 0.9997f, 0.0093f, -0.0214f) },
                }
            },
        };

        internal static Spot S(float x, float y, float z, float qx, float qy, float qz, float qw)
        {
            return new Spot { pos = new Vector3(x, y, z), rot = new Quaternion(qx, qy, qz, qw).normalized };
        }

        internal static bool TryGet(string kind, int island, out Spot spot)
        {
            if (Parse(Plugin.configShopSpots.Value).TryGetValue(kind + "@" + island, out spot)) return true;
            if (Baked.TryGetValue(kind, out Dictionary<int, Spot> d) && d.TryGetValue(island, out spot)) return true;
            spot = default(Spot);
            return false;
        }

        /// <summary>Moves a spawner onto a spot of the given shop (world pose from shop space).</summary>
        internal static void Apply(Transform spawner, Transform shop, Spot spot)
        {
            spawner.position = shop.TransformPoint(spot.pos);
            spawner.rotation = shop.rotation * spot.rot;
        }

        // ---------------- placing by hand ----------------

        // the live shops and the spawners of our goods, filled in as islands load
        internal static readonly Dictionary<int, Transform> Shops = new Dictionary<int, Transform>();
        private static readonly Dictionary<string, Transform> Spawners = new Dictionary<string, Transform>();

        internal static void RegisterSpawner(string kind, int island, Transform spawner)
        {
            Spawners[kind + "@" + island] = spawner;
        }

        private static string cachedText;
        private static Dictionary<string, Spot> cached = new Dictionary<string, Spot>();

        /// <summary>"glass30 9: x, y, z / qx, qy, qz, qw; scroll 1: ..." -> "kind@island" -> spot</summary>
        private static Dictionary<string, Spot> Parse(string text)
        {
            text = text ?? "";
            if (text == cachedText) return cached;
            var result = new Dictionary<string, Spot>();
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
                    float[] p = Floats(parts[0], 3), q = Floats(parts[1], 4);
                    result[head[0] + "@" + island] = S(p[0], p[1], p[2], q[0], q[1], q[2], q[3]);
                }
                catch (Exception)
                {
                    Debug.LogWarning("RealisticSkies: can't read shop spot \"" + e + "\"");
                }
            }
            cachedText = text;
            cached = result;
            return result;
        }

        private static float[] Floats(string s, int n)
        {
            string[] t = s.Split(',');
            if (t.Length != n) throw new FormatException();
            var f = new float[n];
            for (int i = 0; i < n; i++) f[i] = float.Parse(t[i].Trim(), CultureInfo.InvariantCulture);
            return f;
        }

        private static string Format(string kind, int island, Spot s)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1}: {2:F3}, {3:F3}, {4:F3} / {5:F4}, {6:F4}, {7:F4}, {8:F4}",
                kind, island, s.pos.x, s.pos.y, s.pos.z, s.rot.x, s.rot.y, s.rot.z, s.rot.w);
        }

        private static string FormatCode(int island, Spot s)
        {
            return string.Format(CultureInfo.InvariantCulture, "{{ {0}, S({1:F3}f, {2:F3}f, {3:F3}f, {4:F4}f, {5:F4}f, {6:F4}f, {7:F4}f) }},",
                island, s.pos.x, s.pos.y, s.pos.z, s.rot.x, s.rot.y, s.rot.z, s.rot.w);
        }

        /// <summary>Called every frame from the plugin.</summary>
        internal static void Tick()
        {
            if (!Plugin.configShopPlacement.Value || !Plugin.configShopPlaceKey.Value.IsDown() || Watchmaker.Ctrl) return;   // Ctrl: the watchmaker's bench
            try
            {
                Capture();
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: shop placement failed: " + e);
            }
        }

        private static string KindOf(ShipItem it)
        {
            if (it is ModItemLoreScroll ls) return ls.book == "Companion" ? Companion : ls.book == "Modern" ? Modern : Scroll;
            if (it is ModItemHourglass g) return g.kind;
            if (it is ModItemWatchGlass) return WatchGlass;
            if (it is ModItemPocketWatch) return PocketWatch;
            if (it is ModItemJournal) return Journal;
            if (it is ModItemCrossStaff) return CrossStaff;
            if (it is ModItemAstrolabe) return Astrolabe;
            if (it is ModItemNocturnal) return Nocturnal;
            if (it is ModItemRingDial) return RingDial;
            if (it is ModItemTraverseBoard) return TraverseBoard;
            return null;
        }

        private static void Capture()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 eye = cam.transform.position;

            // the item of ours you are looking at: closest to the line of sight, within reach
            ShipItem best = null;
            string kind = null;
            float bestScore = float.MaxValue;
            foreach (ShipItem it in UnityEngine.Object.FindObjectsOfType<ShipItem>())
            {
                string k = KindOf(it);
                if (k == null) continue;
                Vector3 d = it.transform.position - eye;
                float dist = d.magnitude;
                if (dist > 6f || dist < 0.01f) continue;
                float off = Vector3.Angle(cam.transform.forward, d);
                if (off > 25f) continue;
                float score = off + dist * 2f;
                if (score < bestScore) { bestScore = score; best = it; kind = k; }
            }
            if (best == null)
            {
                Notify("none of the mod's goods in sight (look at it, within 6 m)");
                return;
            }

            // the nearest of the mod's shops
            int island = -1;
            Transform shop = null;
            float shopDist = float.MaxValue;
            foreach (KeyValuePair<int, Transform> kv in Shops)
            {
                if (kv.Value == null) continue;
                float d = Vector3.Distance(kv.Value.position, best.transform.position);
                if (d < shopDist) { shopDist = d; shop = kv.Value; island = kv.Key; }
            }
            if (shop == null || shopDist > 150f)
            {
                Notify("no Realistic Skies shop nearby");
                return;
            }

            var spot = new Spot
            {
                pos = shop.InverseTransformPoint(best.transform.position),
                rot = Quaternion.Inverse(shop.rotation) * best.transform.rotation,
            };

            var all = new Dictionary<string, Spot>(Parse(Plugin.configShopSpots.Value)) { [kind + "@" + island] = spot };
            var keys = new List<string>(all.Keys);
            keys.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder();
            foreach (string k in keys)
            {
                int at = k.IndexOf('@');
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(Format(k.Substring(0, at), int.Parse(k.Substring(at + 1), CultureInfo.InvariantCulture), all[k]));
            }
            Plugin.configShopSpots.Value = sb.ToString();
            Plugin.instance.Config.Save();

            // the shop's own spawner moves there too (its item, if still on it, goes along)
            if (Spawners.TryGetValue(kind + "@" + island, out Transform sp) && sp != null && sp != best.transform)
            {
                bool onIt = best.transform.IsChildOf(sp);
                Apply(sp, shop, spot);
                if (onIt)
                {
                    best.transform.position = sp.position;
                    best.transform.rotation = sp.rotation;
                }
            }
            Notify(kind + " spot for island " + island + " saved");
            Debug.Log("RealisticSkies: shop spot  " + Format(kind, island, spot));
            Debug.Log("RealisticSkies: baked line (" + kind + ")  " + FormatCode(island, spot));
        }

        private static void Notify(string msg)
        {
            Debug.Log("RealisticSkies: shop placement: " + msg);
            try
            {
                NotificationUi.instance.ShowNotification("Shop placement: " + msg);
            }
            catch (Exception)
            {
                // no notification UI: the log line is enough
            }
        }
    }
}
