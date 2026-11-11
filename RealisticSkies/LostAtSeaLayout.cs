using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// "Lost at Sea" (docs/LOSTATSEA_SPEC.md), step 1: what lies aboard the scenario's ship.
    ///
    /// Debug: with Debug / Scenario Layout Capture on, the Scenario Layout Key (F6) writes down every
    /// item on the boat you stand on, the way the game's own save does (SaveablePrefab.PrepareSaveData):
    /// prefab, pose in the boat's own space, amount, health, food state, the crate it lies in, and
    /// the boat's sails and parts. The file is BepInEx/config/RealisticSkies/lostatsea_layout.txt.
    ///
    /// The lines:
    ///   boat  name=... index=...
    ///   parts 0,2,1,...                          (BoatCustomParts' active options)
    ///   part  i=.. option=.. of=.. object=..  # label   (the same, by the option's name)
    ///   sail  prefab=.. mast=.. height=.. min=.. max=.. color=.. sy=.. sz=..
    ///   item  id=.. prefab=.. pos=x,y,z rot=x,y,z,w nailed=.. health=.. amount=.. days=.. crate=.. slot=.. extra=a,b,c,d,e  # name
    /// pos and rot are local to the boat's saved object (the transform the game saves items against);
    /// crate is the id of the line of the crate the item lies in (0: none); slot is the player's
    /// pocket (-1: none); hook is the id of the lamp hook the item hangs on (0: none).
    /// </summary>
    internal static class LostAtSeaLayout
    {
        internal const string FileName = "lostatsea_layout.txt";

        private static readonly int[] Chronometers = { 170, 171, 172 };

        internal static string FilePath => Path.Combine(Path.Combine(Paths.ConfigPath, "RealisticSkies"), FileName);

        internal static void Tick()
        {
            if (!Plugin.configLayoutCapture.Value || !Plugin.configLayoutKey.Value.IsDown()) return;
            try
            {
                Capture();
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: layout capture failed: " + e);
                Notify("failed, see the log");
            }
        }

        private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        private static void Capture()
        {
            Transform boat = GameState.currentBoat;
            SaveableObject so = boat != null ? boat.GetComponentInParent<SaveableObject>() : null;
            if (so == null)
            {
                Notify("stand on the boat first");
                return;
            }
            int boatIndex = so.sceneIndex;
            Transform root = so.transform;
            Quaternion toLocal = Quaternion.Inverse(root.rotation);

            // everything the game counts as aboard this boat, plus what is in the player's pockets
            var aboard = new List<SaveablePrefab>();
            var all = new List<SaveablePrefab>(SaveLoadManager.instance.GetCurrentPrefabs());
            foreach (SaveablePrefab p in all)
            {
                if (p == null) continue;
                ShipItem it = p.GetComponent<ShipItem>();
                if (it == null) continue;
                if (p.GetParentObject() == boatIndex || it.GetCurrentInventorySlot() > -1) aboard.Add(p);
            }
            // and what lies in the crates aboard, should the game not count it by itself
            bool grew = true;
            while (grew)
            {
                grew = false;
                var ids = new HashSet<int>(aboard.Select(p => p.instanceId));
                foreach (SaveablePrefab p in all)
                {
                    if (p == null || p.currentCrateId <= 0 || aboard.Contains(p) || !ids.Contains(p.currentCrateId)) continue;
                    aboard.Add(p);
                    grew = true;
                }
            }

            // crates first, so a line's crate is always above it
            aboard = aboard.OrderBy(p => p.currentCrateId > 0 ? 1 : 0).ThenBy(p => p.prefabIndex).ToList();
            var lineOf = new Dictionary<int, int>();
            for (int i = 0; i < aboard.Count; i++) lineOf[aboard[i].instanceId] = i + 1;

            var sb = new StringBuilder();
            sb.AppendLine("# Realistic Skies, Lost at Sea: the ship's layout. Written by the Scenario Layout Key.");
            sb.AppendLine("# pos and rot are in the boat's own space. See LostAtSeaLayout.cs for the fields.");
            sb.AppendLine("boat  name=" + root.name.Replace(' ', '_') + " index=" + boatIndex);

            if (so.customization != null)
            {
                SaveBoatCustomizationData c = so.customization.GetData();
                if (c.partActiveOptions != null && c.partActiveOptions.Count > 0)
                    sb.AppendLine("parts " + string.Join(",", c.partActiveOptions.Select(o => o.ToString(CultureInfo.InvariantCulture)).ToArray()));
                // the same by name: other mods (ShipyardExpansion) add parts and options, so the numbers alone don't say what is fitted
                BoatCustomParts bcp = root.GetComponent<BoatCustomParts>();
                if (bcp != null && bcp.availableParts != null)
                {
                    for (int i = 0; i < bcp.availableParts.Count; i++)
                    {
                        BoatPart part = bcp.availableParts[i];
                        BoatPartOption o = part.partOptions != null && part.activeOption >= 0 && part.activeOption < part.partOptions.Count ? part.partOptions[part.activeOption] : null;
                        sb.AppendLine("part  i=" + i + " option=" + part.activeOption + " of=" + (part.partOptions != null ? part.partOptions.Count : 0)
                            + " object=" + (o != null ? o.gameObject.name.Replace(' ', '_') : "?") + "  # " + (o != null ? o.optionName : "?"));
                    }
                }
                foreach (SaveSailData s in c.sails)
                {
                    sb.AppendLine("sail  prefab=" + s.prefabIndex + " mast=" + s.mastIndex + " height=" + F(s.installHeight)
                        + " min=" + F(s.minAngle) + " max=" + F(s.maxAngle) + " color=" + s.sailColor
                        + " sy=" + F(s.scaleY) + " sz=" + F(s.scaleZ));
                }
            }

            var count = new SortedDictionary<int, int>();
            var names = new Dictionary<int, string>();
            var warnings = new List<string>();
            int hanging = 0;
            for (int i = 0; i < aboard.Count; i++)
            {
                SaveablePrefab p = aboard[i];
                ShipItem it = p.GetComponent<ShipItem>();
                SavePrefabData d = p.PrepareSaveData();
                // the game keeps a boat item's place in the boat's space, but its rotation in the world's
                Vector3 pos = d.itemParentObject == boatIndex ? (Vector3)d.position : root.InverseTransformPoint(p.transform.position);
                Quaternion rot = toLocal * p.transform.rotation;
                int crate = 0;
                if (d.crateId > 0) lineOf.TryGetValue(d.crateId, out crate);
                string name = string.IsNullOrEmpty(it.name) ? p.gameObject.name : it.name;
                // hanging on a lamp hook: the id of the hook's line (the save doesn't keep it: the game hangs things again by their place)
                int hook = 0;
                HangableItem hang = p.GetComponent<HangableItem>();
                if (hang != null && hang.IsHanging())
                {
                    Collider hc = HarmonyLib.Traverse.Create(hang).Field("currentHook").GetValue<Collider>();
                    SaveablePrefab hp = hc != null ? hc.GetComponent<SaveablePrefab>() : null;
                    if (hp == null || !lineOf.TryGetValue(hp.instanceId, out hook)) hook = -1;
                    hanging++;
                }

                sb.Append("item  id=").Append(i + 1)
                  .Append(" prefab=").Append(d.prefabIndex)
                  .Append(" pos=").Append(F(pos.x)).Append(',').Append(F(pos.y)).Append(',').Append(F(pos.z))
                  .Append(" rot=").Append(F(rot.x)).Append(',').Append(F(rot.y)).Append(',').Append(F(rot.z)).Append(',').Append(F(rot.w))
                  .Append(" nailed=").Append(d.isNailed ? 1 : 0)
                  .Append(" health=").Append(F(d.itemHealth))
                  .Append(" amount=").Append(F(d.itemAmount))
                  .Append(" days=").Append(d.daysInStorage)
                  .Append(" crate=").Append(crate)
                  .Append(" slot=").Append(d.inventorySlot)
                  .Append(" hook=").Append(hook)
                  .Append(" extra=").Append(F(d.extraValue0)).Append(',').Append(F(d.extraValue1)).Append(',').Append(F(d.extraValue2))
                  .Append(',').Append(F(d.extraValue3)).Append(',').Append(F(d.extraValue4))
                  .Append("  # ").AppendLine(name);

                count.TryGetValue(d.prefabIndex, out int n);
                count[d.prefabIndex] = n + 1;
                names[d.prefabIndex] = name;

                if (Array.IndexOf(Chronometers, d.prefabIndex) >= 0) warnings.Add("a chronometer (" + d.prefabIndex + ") is aboard: the scenario will leave it out");
                if (!d.isSold) warnings.Add(name + " (" + d.prefabIndex + ") is not bought");
                if (d.itemMissionIndex > -1) warnings.Add(name + " (" + d.prefabIndex + ") is mission cargo: it will come back as plain cargo");
                if (it.held != null) warnings.Add(name + " (" + d.prefabIndex + ") is in your hand: put it down and capture again");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, sb.ToString());

            var log = new StringBuilder("RealisticSkies: layout of " + root.name + " captured: " + aboard.Count + " items -> " + FilePath);
            foreach (KeyValuePair<int, int> kv in count) log.Append("\n  ").Append(kv.Key).Append("  x").Append(kv.Value).Append("  ").Append(names[kv.Key]);
            foreach (string w in warnings.Distinct()) log.Append("\n  WARNING: ").Append(w);
            Debug.Log(log.ToString());

            Debug.Log("RealisticSkies: layout capture: " + hanging + " items hang on hooks");
            Notify(aboard.Count + " items (" + hanging + " on hooks) on " + root.name + " written to " + FileName
                + (warnings.Count > 0 ? "\n" + warnings.Distinct().Count() + " warning(s), see the log" : ""));
        }

        private static void Notify(string msg)
        {
            Debug.Log("RealisticSkies: layout capture: " + msg);
            try { NotificationUi.instance.ShowNotification("Layout capture: " + msg); } catch (Exception) { }
        }
    }
}
