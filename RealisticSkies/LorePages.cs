using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Sea Legends pages, loaded without stalling the game. A 2048x2048 PNG used to be decoded with
    /// ImageConversion.LoadImage and then DXT-compressed on the main thread, which took the better
    /// part of a second per page. Now the PNG is decoded and its mip chain built on a worker thread;
    /// the main thread only uploads the finished pixels. The pages next to the one being read are
    /// fetched ahead, so turning a page is normally instant.
    /// </summary>
    internal static class LorePages
    {
        private const int Keep = 4;     // textures kept alive: the page being read and its neighbours

        private class Job
        {
            public volatile bool done;
            public byte[] raw;          // RGBA32, bottom row first, full mip chain
            public byte[] png;          // fallback: let Unity decode it
            public int w, h;
            public string error;
        }

        private static string[] files;
        private static readonly Dictionary<int, Texture2D> cache = new Dictionary<int, Texture2D>();
        private static readonly Dictionary<int, Job> jobs = new Dictionary<int, Job>();
        private static readonly List<int> recent = new List<int>();

        internal static string[] Files
        {
            get
            {
                if (files == null)
                {
                    files = ModResources.LorePages();
                    if (files.Length == 0) Debug.LogWarning("RealisticSkies: no Sea Legends pages found, the scroll is empty");
                }
                return files;
            }
        }

        internal static int Count => Files.Length;

        /// <summary>The page if it is ready, else null (it is then loading; ask again next frame).</summary>
        internal static Texture2D Get(int index)
        {
            if (index < 0 || index >= Count) return null;
            Pump();
            if (cache.TryGetValue(index, out Texture2D tex) && tex != null)
            {
                Touch(index);
                return tex;
            }
            Request(index);
            return null;
        }

        /// <summary>Starts loading a page in the background if it is not loaded or loading already.</summary>
        internal static void Request(int index)
        {
            if (index < 0 || index >= Count) return;
            if (cache.ContainsKey(index) || jobs.ContainsKey(index)) return;
            var job = new Job();
            jobs[index] = job;
            string file = Files[index];
            ThreadPool.QueueUserWorkItem(_ => Decode(job, file));
        }

        /// <summary>Uploads finished pages (main thread). Cheap when nothing is waiting.</summary>
        internal static void Pump()
        {
            if (jobs.Count == 0) return;
            List<int> finished = null;
            foreach (KeyValuePair<int, Job> kv in jobs)
            {
                if (kv.Value.done) (finished ?? (finished = new List<int>())).Add(kv.Key);
            }
            if (finished == null) return;
            foreach (int index in finished)
            {
                Job job = jobs[index];
                jobs.Remove(index);
                Texture2D tex = Upload(job, index);
                if (tex == null) continue;
                cache[index] = tex;
                Touch(index);
            }
        }

        private static Texture2D Upload(Job job, int index)
        {
            string name = "Sea Legends " + Path.GetFileName(Files[index]);
            try
            {
                Texture2D tex;
                if (job.raw != null)
                {
                    tex = new Texture2D(job.w, job.h, TextureFormat.RGBA32, true);
                    tex.LoadRawTextureData(job.raw);
                    tex.Apply(false, true);
                }
                else if (job.png != null)
                {
                    // an unusual PNG the small decoder below does not handle: the old, slow way
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                    if (!ImageConversion.LoadImage(tex, job.png, true)) throw new Exception("not a PNG");
                }
                else
                {
                    throw new Exception(job.error ?? "no data");
                }
                tex.name = name;
                tex.anisoLevel = 4;
                tex.wrapMode = TextureWrapMode.Clamp;
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not load " + name + ": " + e.Message);
                return null;
            }
        }

        private static void Touch(int index)
        {
            recent.Remove(index);
            recent.Add(index);
            while (recent.Count > Keep)
            {
                int old = recent[0];
                recent.RemoveAt(0);
                if (cache.TryGetValue(old, out Texture2D t) && t != null) UnityEngine.Object.Destroy(t);
                cache.Remove(old);
            }
        }

        // ---------------- worker thread ----------------

        private static void Decode(Job job, string file)
        {
            try
            {
                byte[] png = ModResources.Read(file, out string source);
                if (png == null) { job.error = "missing " + source; return; }
                if (!DecodePng(png, out byte[] rgba, out int w, out int h))
                {
                    job.png = png;
                    return;
                }
                job.raw = BuildMips(rgba, w, h);
                job.w = w;
                job.h = h;
            }
            catch (Exception e)
            {
                job.error = e.Message;
            }
            finally
            {
                job.done = true;
            }
        }

        /// <summary>8-bit, non-interlaced PNG -> RGBA32 rows, bottom row first (Unity's order).</summary>
        private static bool DecodePng(byte[] d, out byte[] rgba, out int w, out int h)
        {
            rgba = null; w = h = 0;
            if (d.Length < 8 || d[0] != 0x89 || d[1] != (byte)'P' || d[2] != (byte)'N' || d[3] != (byte)'G') return false;
            int colorType = -1;
            var idat = new MemoryStream();
            int p = 8;
            while (p + 8 <= d.Length)
            {
                int len = (d[p] << 24) | (d[p + 1] << 16) | (d[p + 2] << 8) | d[p + 3];
                string type = new string(new[] { (char)d[p + 4], (char)d[p + 5], (char)d[p + 6], (char)d[p + 7] });
                int body = p + 8;
                if (type == "IHDR")
                {
                    w = (d[body] << 24) | (d[body + 1] << 16) | (d[body + 2] << 8) | d[body + 3];
                    h = (d[body + 4] << 24) | (d[body + 5] << 16) | (d[body + 6] << 8) | d[body + 7];
                    int depth = d[body + 8];
                    colorType = d[body + 9];
                    int interlace = d[body + 12];
                    if (depth != 8 || interlace != 0) return false;
                    if (colorType != 0 && colorType != 2 && colorType != 4 && colorType != 6) return false;
                }
                else if (type == "IDAT")
                {
                    idat.Write(d, body, len);
                }
                else if (type == "IEND")
                {
                    break;
                }
                p = body + len + 4;     // skip the CRC
            }
            if (colorType < 0 || w <= 0 || h <= 0) return false;
            int bpp = colorType == 6 ? 4 : colorType == 2 ? 3 : colorType == 4 ? 2 : 1;
            int stride = w * bpp;

            // zlib stream: 2 header bytes, then raw deflate
            byte[] raw = new byte[(stride + 1) * h];
            idat.Position = 2;
            using (var z = new DeflateStream(idat, CompressionMode.Decompress))
            {
                int got = 0;
                while (got < raw.Length)
                {
                    int n = z.Read(raw, got, raw.Length - got);
                    if (n <= 0) return false;
                    got += n;
                }
            }

            // undo the per-row filters in place
            byte[] prev = new byte[stride];
            byte[] cur = new byte[stride];
            rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                int rowStart = y * (stride + 1);
                int filter = raw[rowStart];
                Buffer.BlockCopy(raw, rowStart + 1, cur, 0, stride);
                switch (filter)
                {
                    case 0: break;
                    case 1: for (int i = bpp; i < stride; i++) cur[i] += cur[i - bpp]; break;
                    case 2: for (int i = 0; i < stride; i++) cur[i] += prev[i]; break;
                    case 3:
                        for (int i = 0; i < stride; i++)
                            cur[i] += (byte)(((i >= bpp ? cur[i - bpp] : 0) + prev[i]) >> 1);
                        break;
                    case 4:
                        for (int i = 0; i < stride; i++)
                        {
                            int a = i >= bpp ? cur[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
                            int pa = Math.Abs(b - c), pb = Math.Abs(a - c), pc = Math.Abs(a + b - 2 * c);
                            cur[i] += (byte)(pa <= pb && pa <= pc ? a : pb <= pc ? b : c);
                        }
                        break;
                    default: return false;
                }
                // to RGBA, flipped vertically
                int o = (h - 1 - y) * w * 4;
                for (int x = 0, s = 0; x < w; x++, s += bpp, o += 4)
                {
                    switch (colorType)
                    {
                        case 6: rgba[o] = cur[s]; rgba[o + 1] = cur[s + 1]; rgba[o + 2] = cur[s + 2]; rgba[o + 3] = cur[s + 3]; break;
                        case 2: rgba[o] = cur[s]; rgba[o + 1] = cur[s + 1]; rgba[o + 2] = cur[s + 2]; rgba[o + 3] = 255; break;
                        case 4: rgba[o] = rgba[o + 1] = rgba[o + 2] = cur[s]; rgba[o + 3] = cur[s + 1]; break;
                        default: rgba[o] = rgba[o + 1] = rgba[o + 2] = cur[s]; rgba[o + 3] = 255; break;
                    }
                }
                byte[] t = prev; prev = cur; cur = t;
            }
            return true;
        }

        /// <summary>
        /// Full mip chain, largest first, as Texture2D.LoadRawTextureData expects. Each level is a 2x2
        /// box filter; colour is weighted by alpha so the transparent paper never darkens the ink.
        /// </summary>
        private static byte[] BuildMips(byte[] level0, int w, int h)
        {
            var levels = new List<byte[]> { level0 };
            int total = level0.Length;
            byte[] src = level0;
            int sw = w, sh = h;
            while (sw > 1 || sh > 1)
            {
                int dw = Math.Max(1, sw / 2), dh = Math.Max(1, sh / 2);
                byte[] dst = new byte[dw * dh * 4];
                for (int y = 0; y < dh; y++)
                {
                    int y0 = Math.Min(sh - 1, y * 2), y1 = Math.Min(sh - 1, y * 2 + 1);
                    for (int x = 0; x < dw; x++)
                    {
                        int x0 = Math.Min(sw - 1, x * 2), x1 = Math.Min(sw - 1, x * 2 + 1);
                        int i00 = (y0 * sw + x0) * 4, i01 = (y0 * sw + x1) * 4, i10 = (y1 * sw + x0) * 4, i11 = (y1 * sw + x1) * 4;
                        int a00 = src[i00 + 3], a01 = src[i01 + 3], a10 = src[i10 + 3], a11 = src[i11 + 3];
                        int asum = a00 + a01 + a10 + a11;
                        int o = (y * dw + x) * 4;
                        for (int c = 0; c < 3; c++)
                        {
                            dst[o + c] = asum > 0
                                ? (byte)((src[i00 + c] * a00 + src[i01 + c] * a01 + src[i10 + c] * a10 + src[i11 + c] * a11 + asum / 2) / asum)
                                : (byte)((src[i00 + c] + src[i01 + c] + src[i10 + c] + src[i11 + c] + 2) >> 2);
                        }
                        dst[o + 3] = (byte)((asum + 2) >> 2);
                    }
                }
                levels.Add(dst);
                total += dst.Length;
                src = dst; sw = dw; sh = dh;
            }
            byte[] all = new byte[total];
            int off = 0;
            foreach (byte[] l in levels)
            {
                Buffer.BlockCopy(l, 0, all, off, l.Length);
                off += l.Length;
            }
            return all;
        }
    }
}
