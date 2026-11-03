using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The mod's own pictures, models and sounds. They are looked for in this order:
    /// 1. a loose file with the same relative path in the plugin folder (StarChart.png,
    ///    Lore/page01.png...): quick edits without rebuilding anything;
    /// 2. the mod's asset bundle, realisticskies_assets, built in the Unity editor from unity/
    ///    (textures come GPU-compressed with their mips, ready to draw);
    /// 3. the copy embedded in the DLL, if the DLL was built with them (-p:EmbedAssets=true).
    /// The game's own prefabs stay in the original realisticskies bundle.
    /// </summary>
    internal static class ModResources
    {
        private const string Prefix = "RealisticSkies.";     // LogicalName prefix set in the .csproj

        internal static string PluginDir => Path.Combine(Paths.PluginPath, "RealisticSkies");

        private static Assembly Asm => typeof(ModResources).Assembly;

        /// <summary>Relative path like "StarChart.png" or "Lore/page01.png" -> resource name.</summary>
        private static string ResourceName(string relPath) => Prefix + relPath.Replace('\\', '/').Replace('/', '.');

        internal const string BundleFile = "realisticskies_assets";
        private const string BundleRoot = "assets/realisticskies/";
        private static AssetBundle bundle;
        private static bool bundleTried;
        private static HashSet<string> bundleNames;

        /// <summary>The mod's asset bundle, or null if it isn't installed.</summary>
        internal static AssetBundle Bundle
        {
            get
            {
                if (bundleTried) return bundle;
                bundleTried = true;
                string path = Path.Combine(PluginDir, BundleFile);
                if (!File.Exists(path))
                {
                    Debug.Log("RealisticSkies: no " + BundleFile + ", using the files built into the DLL");
                    return null;
                }
                bundle = AssetBundle.LoadFromFile(path);
                if (bundle == null) { Debug.LogWarning("RealisticSkies: could not load " + path); return null; }
                bundleNames = new HashSet<string>(bundle.GetAllAssetNames(), StringComparer.OrdinalIgnoreCase);
                Debug.Log("RealisticSkies: loaded " + BundleFile + " (" + bundleNames.Count + " assets)");
                return bundle;
            }
        }

        /// <summary>"Astrolabe/albedo.jpg" -> "assets/realisticskies/astrolabe/albedo.jpg"</summary>
        private static string BundleName(string relPath) => (BundleRoot + relPath.Replace('\\', '/')).ToLowerInvariant();

        private static bool InBundle(string name) => Bundle != null && bundleNames.Contains(name);

        private static string Loose(string relPath)
        {
            string loose = Path.Combine(PluginDir, relPath);
            return File.Exists(loose) ? loose : null;
        }

        /// <summary>
        /// A texture: loose file, bundle, embedded copy, or null. fromBundle tells the caller it is a
        /// shared, compressed, read-only asset: don't Compress/Apply it, and free it with
        /// Resources.UnloadAsset, not Destroy.
        /// </summary>
        internal static Texture2D Texture(string relPath, bool linear, out bool fromBundle)
        {
            fromBundle = false;
            if (Loose(relPath) == null && InBundle(BundleName(relPath)))
            {
                var t = bundle.LoadAsset<Texture2D>(BundleName(relPath));
                if (t != null) { fromBundle = true; return t; }
            }
            byte[] data = Read(relPath, out string source);
            if (data == null) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear);
            if (!ImageConversion.LoadImage(tex, data, false)) { UnityEngine.Object.Destroy(tex); return null; }
            return tex;
        }

        internal static Texture2D Texture(string relPath, bool linear) => Texture(relPath, linear, out _);

        /// <summary>Starts loading a bundle texture in the background; null if it isn't in the bundle (or a loose file overrides it).</summary>
        internal static AssetBundleRequest TextureAsync(string relPath)
        {
            if (Loose(relPath) != null || !InBundle(BundleName(relPath))) return null;
            return bundle.LoadAssetAsync<Texture2D>(BundleName(relPath));
        }

        /// <summary>A sound from the bundle, or null (the caller then decodes the WAV from Read).</summary>
        internal static AudioClip Audio(string relPath)
        {
            if (Loose(relPath) != null || !InBundle(BundleName(relPath))) return null;
            return bundle.LoadAsset<AudioClip>(BundleName(relPath));
        }

        /// <summary>Raw bytes: loose file, bundle (as name.bytes), embedded copy, else null. Use it for the models.</summary>
        internal static byte[] Read(string relPath, out string source)
        {
            string loose = Loose(relPath);
            if (loose != null)
            {
                source = loose;
                return File.ReadAllBytes(loose);
            }
            string bn = BundleName(relPath) + ".bytes";
            if (InBundle(bn))
            {
                var ta = bundle.LoadAsset<TextAsset>(bn);
                if (ta != null) { source = BundleFile + " " + relPath; return ta.bytes; }
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
        internal static string[] LorePages(string folder = "Lore")
        {
            var pages = new List<string>();
            string dir = Path.Combine(PluginDir, folder);
            if (Directory.Exists(dir))
            {
                foreach (string f in Directory.GetFiles(dir, "page*.png"))
                {
                    pages.Add(folder + "/" + Path.GetFileName(f));
                }
            }
            if (pages.Count == 0 && Bundle != null)
            {
                string head = BundleRoot + folder.ToLowerInvariant() + "/page";
                foreach (string n in bundleNames)
                {
                    if (n.StartsWith(head, StringComparison.OrdinalIgnoreCase) && n.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        pages.Add(folder + "/" + n.Substring(BundleRoot.Length + folder.Length + 1));
                    }
                }
            }
            if (pages.Count == 0)
            {
                string head = Prefix + folder + ".page";
                foreach (string r in Asm.GetManifestResourceNames())
                {
                    if (r.StartsWith(head, StringComparison.OrdinalIgnoreCase) && r.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        pages.Add(folder + "/" + r.Substring(Prefix.Length + folder.Length + 1));
                    }
                }
            }
            pages.Sort(StringComparer.OrdinalIgnoreCase);
            return pages.ToArray();
        }
    }
}
