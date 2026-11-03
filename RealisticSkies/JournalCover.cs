using System;

namespace RealisticSkies
{
    /// <summary>
    /// The journal's cover, painted in code: dark red-brown leather, mottled and rubbed pale at the
    /// edges and corners, a few scuffs, and a gilt eight-pointed star (a compass rose) in a ring with
    /// a gilt frame round the board, the gilding worn through in places.
    /// RGBA bytes, rows from the bottom (as Texture2D.LoadRawTextureData wants them); u across the
    /// board, v from its foot to its head. No Unity types, so it can be tried outside the game.
    /// </summary>
    internal static class JournalCover
    {
        internal const int W = 512, H = 720;

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static float Noise(float x, float y, int seed)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            float fx = Smooth(x - xi), fy = Smooth(y - yi);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        private static float Fbm(float x, float y, int seed)
        {
            float s = 0f, amp = 0.5f;
            for (int o = 0; o < 4; o++)
            {
                s += Noise(x, y, seed + o * 31) * amp;
                x *= 2.03f; y *= 2.03f; amp *= 0.5f;
            }
            return s / 0.9375f;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        private static float Ramp(float e0, float e1, float x) => Smooth(Clamp01((x - e0) / (e1 - e0)));

        // the star's outline: tips (N, E, S, W long, the others shorter) and the waists between them
        private static readonly double[] SX = new double[16], SY = new double[16];

        static JournalCover()
        {
            for (int k = 0; k < 16; k++)
            {
                double a = k * Math.PI / 8;
                double r = k % 2 == 1 ? 0.21 : (k / 2) % 2 == 0 ? 1.0 : 0.64;
                SX[k] = Math.Sin(a) * r;       // clockwise from "up"
                SY[k] = Math.Cos(a) * r;
            }
        }

        /// <summary>Coverage (0..1) of the star at a point given in star radii from its centre (pixel = 1/pxPerUnit), and its shading.</summary>
        private static float Star(float x, float y, float pxPerUnit, out float shade)
        {
            // inside test and distance to the outline
            bool inside = false;
            double best = double.MaxValue;
            for (int i = 0, j = 15; i < 16; j = i++)
            {
                if ((SY[i] > y) != (SY[j] > y) && x < (SX[j] - SX[i]) * (y - SY[i]) / (SY[j] - SY[i]) + SX[i]) inside = !inside;
                double ex = SX[j] - SX[i], ey = SY[j] - SY[i];
                double t = ((x - SX[i]) * ex + (y - SY[i]) * ey) / (ex * ex + ey * ey);
                t = t < 0 ? 0 : t > 1 ? 1 : t;
                double qx = x - SX[i] - t * ex, qy = y - SY[i] - t * ey;
                double d = Math.Sqrt(qx * qx + qy * qy);
                if (d < best) best = d;
            }
            float sd = (float)(best * pxPerUnit) * (inside ? 1f : -1f);
            // each point lit on one side of its ridge
            double ang = Math.Atan2(x, y);
            if (ang < 0) ang += 2 * Math.PI;
            double k = Math.Floor(ang / (Math.PI / 4) + 0.5);
            double off = ang - k * Math.PI / 4;
            shade = off >= 0 ? 1f : 0.70f;
            return Clamp01(sd + 0.5f);
        }

        internal static byte[] Paint()
        {
            var px = new byte[W * H * 4];
            var leather = new[] { 0.30f, 0.12f, 0.065f };
            var rubbed = new[] { 0.52f, 0.33f, 0.20f };
            var gold = new[] { 0.80f, 0.63f, 0.28f };
            var rnd = new Random(1789);
            // scuffs: short pale strokes
            int nScuff = 12;
            var sx = new float[nScuff]; var sy = new float[nScuff]; var sdx = new float[nScuff]; var sdy = new float[nScuff];
            for (int i = 0; i < nScuff; i++)
            {
                sx[i] = (float)rnd.NextDouble() * W; sy[i] = (float)rnd.NextDouble() * H;
                double a = rnd.NextDouble() * Math.PI;
                float l = 8f + (float)rnd.NextDouble() * 22f;
                sdx[i] = (float)Math.Cos(a) * l; sdy[i] = (float)Math.Sin(a) * l;
            }
            float cx = W * 0.5f, cy = H * 0.53f, R = W * 0.30f;
            float frame = 26f;

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    // leather: mottled, with a fine grain
                    float m = Fbm(u * 7f, v * 10f, 3);
                    float grain = Noise(x * 0.45f, y * 0.45f, 9) - 0.5f;
                    float lv = 0.78f + 0.42f * m + 0.10f * grain;
                    float r = leather[0] * lv, g = leather[1] * lv, b = leather[2] * lv;

                    // rubbed edges and corners
                    float d = Math.Min(Math.Min(x, W - 1 - x), Math.Min(y, H - 1 - y));
                    float cd = (float)Math.Sqrt(Math.Pow(Math.Min(x, W - 1 - x), 2) + Math.Pow(Math.Min(y, H - 1 - y), 2));
                    float wear = Ramp(22f, 0f, d) * (0.45f + 0.55f * Fbm(u * 18f, v * 18f, 5)) + Ramp(70f, 0f, cd) * 0.6f;
                    wear += Ramp(0.62f, 0.85f, Fbm(u * 4f, v * 5f, 11)) * 0.25f;      // a few worn patches
                    for (int i = 0; i < nScuff; i++)
                    {
                        float px0 = x - sx[i], py0 = y - sy[i];
                        float len2 = sdx[i] * sdx[i] + sdy[i] * sdy[i];
                        float t = Clamp01((px0 * sdx[i] + py0 * sdy[i]) / len2);
                        float qx = px0 - t * sdx[i], qy = py0 - t * sdy[i];
                        float dist = (float)Math.Sqrt(qx * qx + qy * qy);
                        if (dist < 1.6f) wear += (1f - dist / 1.6f) * 0.25f;
                    }
                    wear = Clamp01(wear);
                    r += (rubbed[0] - r) * wear; g += (rubbed[1] - g) * wear; b += (rubbed[2] - b) * wear;

                    // gilding: frame, ring and star
                    float gilt = 0f, shade = 1f;
                    float fd = Math.Abs(d - frame);
                    gilt = Math.Max(gilt, Clamp01(2.2f - fd));                          // frame line
                    float fd2 = Math.Abs(d - frame - 9f);
                    gilt = Math.Max(gilt, Clamp01(1.2f - fd2) * 0.9f);                   // thin inner line
                    float dx = (x - cx) / R, dy = (y - cy) / R;
                    float rr = (float)Math.Sqrt(dx * dx + dy * dy);
                    gilt = Math.Max(gilt, Clamp01(1.6f - Math.Abs(rr - 0.80f) * R));     // ring
                    gilt = Math.Max(gilt, Clamp01(1.0f - Math.Abs(rr - 0.86f) * R) * 0.8f);
                    float st = Star(dx, dy, R, out float sh);
                    if (st > gilt) { gilt = st; shade = sh; }
                    if (rr < 0.05f) { gilt = 1f; shade = 0.85f; }
                    // the gold worn through here and there
                    float keep = Ramp(0.26f, 0.42f, Fbm(u * 60f, v * 84f, 21));
                    gilt *= 0.45f + 0.55f * keep;
                    gilt *= 1f - 0.6f * wear;
                    float gl = shade * (0.88f + 0.24f * Noise(x * 0.8f, y * 0.8f, 13));
                    r += (gold[0] * gl - r) * gilt; g += (gold[1] * gl - g) * gilt; b += (gold[2] * gl - b) * gilt;

                    int o = (y * W + x) * 4;
                    px[o] = (byte)(Clamp01(r) * 255f + 0.5f);
                    px[o + 1] = (byte)(Clamp01(g) * 255f + 0.5f);
                    px[o + 2] = (byte)(Clamp01(b) * 255f + 0.5f);
                    px[o + 3] = 255;
                }
            }
            return px;
        }
    }
}
