using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Debug: Ctrl+F12 counts the engine's objects (meshes, materials, textures...) by type and name
    /// and logs the commonest, and what has grown since the last count. For hunting whatever piles up
    /// objects for the game's unloading of unused assets after a save.
    /// </summary>
    internal static class AssetCensus
    {
        private static Dictionary<string, int> last;

        internal static void Tick()
        {
            if (!(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) || !Input.GetKeyDown(KeyCode.F12)) return;
            var now = new Dictionary<string, int>();
            var byType = new Dictionary<string, int>();
            foreach (Object o in Resources.FindObjectsOfTypeAll<Object>())
            {
                if (o is GameObject || o is Component) continue;
                string type = o.GetType().Name;
                string key = type + " '" + o.name + "'";
                now.TryGetValue(key, out int n); now[key] = n + 1;
                byType.TryGetValue(type, out int t); byType[type] = t + 1;
            }
            var sb = new StringBuilder("RealisticSkies census: ");
            sb.Append(string.Join(", ", byType.OrderByDescending(p => p.Value).Take(12).Select(p => p.Key + " " + p.Value).ToArray()));
            sb.Append("\n  commonest: ");
            sb.Append(string.Join("; ", now.OrderByDescending(p => p.Value).Take(20).Select(p => p.Key + " x" + p.Value).ToArray()));
            if (last != null)
            {
                sb.Append("\n  grown since last count: ");
                sb.Append(string.Join("; ", now.Select(p => new KeyValuePair<string, int>(p.Key, p.Value - (last.TryGetValue(p.Key, out int l) ? l : 0)))
                    .Where(p => p.Value > 0).OrderByDescending(p => p.Value).Take(20).Select(p => p.Key + " +" + p.Value).ToArray()));
            }
            last = now;
            Debug.Log(sb.ToString());
            try { NotificationUi.instance.ShowNotification("Census written to the log"); } catch (System.Exception) { }
        }
    }
}
