using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;

namespace RealisticSkies
{
    /// <summary>
    /// The star chart and the Sea Legends pages are built into the DLL as embedded resources, so the
    /// mod ships as RealisticSkies.dll plus the asset bundle. A loose file with the same name in the
    /// plugin folder (StarChart.png, Lore/page01.png...) still wins, which keeps quick edits possible
    /// without rebuilding: drop the file in, restart, and delete it again when done.
    /// </summary>
    internal static class ModResources
    {
        private const string Prefix = "RealisticSkies.";     // LogicalName prefix set in the .csproj

        internal static string PluginDir => Path.Combine(Paths.PluginPath, "RealisticSkies");

        private static Assembly Asm => typeof(ModResources).Assembly;

        /// <summary>Relative path like "StarChart.png" or "Lore/page01.png" -> resource name.</summary>
        private static string ResourceName(string relPath) => Prefix + relPath.Replace('\\', '/').Replace('/', '.');

        /// <summary>Loose file if there is one, else the embedded copy, else null.</summary>
        internal static byte[] Read(string relPath, out string source)
        {
            string loose = Path.Combine(PluginDir, relPath);
            if (File.Exists(loose))
            {
                source = loose;
                return File.ReadAllBytes(loose);
            }
            source = "embedded " + relPath;
            using (Stream s = Asm.GetManifestResourceStream(ResourceName(relPath)))
            {
                if (s == null) return null;
                byte[] data = new byte[s.Length];
                int read = 0;
                while (read < data.Length)
                {
                    int n = s.Read(data, read, data.Length - read);
                    if (n <= 0) break;
                    read += n;
                }
                return data;
            }
        }

        /// <summary>
        /// The Sea Legends pages, in order, as relative paths ("Lore/page01.png"...). A Lore folder
        /// next to the plugin replaces the built-in pages as a whole, so a stale page from an old
        /// install can never mix with new ones.
        /// </summary>
        internal static string[] LorePages()
        {
            var pages = new List<string>();
            string dir = Path.Combine(PluginDir, "Lore");
            if (Directory.Exists(dir))
            {
                foreach (string f in Directory.GetFiles(dir, "page*.png"))
                {
                    pages.Add("Lore/" + Path.GetFileName(f));
                }
            }
            if (pages.Count == 0)
            {
                string head = Prefix + "Lore.page";
                foreach (string r in Asm.GetManifestResourceNames())
                {
                    if (r.StartsWith(head, StringComparison.OrdinalIgnoreCase) && r.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        pages.Add("Lore/" + r.Substring(Prefix.Length + "Lore.".Length));
                    }
                }
            }
            pages.Sort(StringComparer.OrdinalIgnoreCase);
            return pages.ToArray();
        }
    }
}
