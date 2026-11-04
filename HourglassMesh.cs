using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The half-hour glass, built entirely in code: everything is a surface of revolution ("lathe"),
    /// so the model needs no assets. Units are metres, y up, the base on y = 0.
    /// The sand is two bodies (the upper bulb with its crater, the heap in the lower one) whose
    /// levels are solved from the sand volume, so what leaves the top always arrives at the bottom.
    /// The model is built standing on y = 0 and then moved down so its centre (the neck) is the
    /// item's origin: the glass turns about its middle in the hand, not about its foot.
    /// </summary>
    internal static class HourglassMesh
    {
        // ---------------- design ----------------
        internal const float DiscR = 0.062f, DiscT = 0.014f;    // end discs
        private const float GlassY0 = 0.014f, GlassY1 = 0.226f;  // glass between the discs
        internal const float Neck = (GlassY0 + GlassY1) / 2f;
        internal const float Height = GlassY1 + DiscT;
        internal const float Radius = DiscR;
        internal const float Centre = Neck;                       // model origin, from the base

        /// <summary>Origin to the neck, then the whole glass scaled (the hour glass is a bigger copy).</summary>
        private static void Recentre(List<Vector3> v, float scale)
        {
            for (int i = 0; i < v.Count; i++) v[i] = new Vector3(v[i].x * scale, (v[i].y - Centre) * scale, v[i].z * scale);
        }
        private const float Half = (GlassY1 - GlassY0) / 2f;
        private const float Wall = 0.0015f;                       // glass thickness
        internal const float PostR = 0.054f;                      // posts stand on this circle
        private const float StreamTaper = 0.75f;                  // the falling sand thins as it speeds up

        // glass radius over s = distance from the neck / half height (0 = neck, 1 = cap)
        private static readonly Vector2[] GlassCtrl =
        {
            new Vector2(0f, 0.0045f), new Vector2(0.07f, 0.0065f), new Vector2(0.18f, 0.017f), new Vector2(0.35f, 0.034f),
            new Vector2(0.55f, 0.0445f), new Vector2(0.72f, 0.0455f), new Vector2(0.86f, 0.040f), new Vector2(0.95f, 0.030f),
            new Vector2(1f, 0.024f),
        };

        // turned post: radius over s = 0..1 along the post
        private static readonly Vector2[] PostCtrl =
        {
            new Vector2(0f, 0.0075f), new Vector2(0.06f, 0.0075f), new Vector2(0.09f, 0.0045f), new Vector2(0.2f, 0.0055f),
            new Vector2(0.35f, 0.0045f), new Vector2(0.5f, 0.0065f), new Vector2(0.65f, 0.0045f), new Vector2(0.8f, 0.0055f),
            new Vector2(0.91f, 0.0045f), new Vector2(0.94f, 0.0075f), new Vector2(1f, 0.0075f),
        };

        private static float Catmull(Vector2[] p, float x)
        {
            int n = p.Length;
            int i = 0;
            while (i < n - 2 && x > p[i + 1].x) i++;
            float t = Mathf.Clamp01((x - p[i].x) / (p[i + 1].x - p[i].x));
            float p0 = p[Math.Max(i - 1, 0)].y, p1 = p[i].y, p2 = p[i + 1].y, p3 = p[Math.Min(i + 2, n - 1)].y;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
        }

        private static float GlassR(float y) => Catmull(GlassCtrl, Mathf.Min(1f, Mathf.Abs(y - Neck) / Half));
        private static float InnerR(float y) => Mathf.Max(0f, GlassR(y) - Wall);

        // ---------------- lathe ----------------

        /// <summary>
        /// Adds a surface of revolution to the lists. The profile is (radius, height) points; a
        /// point given twice in a row makes a hard edge. 'inward' turns the faces to the axis.
        /// </summary>
        internal static void Lathe(List<Vector3> v, List<Vector3> n, List<int> tri, IList<Vector2> prof, int segs,
                                  Vector3 offset, bool inward = false, bool mirror = false)
        {
            int count = prof.Count;
            var normals2 = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 a = i > 0 ? prof[i - 1] : prof[i];
                Vector2 b = i < count - 1 ? prof[i + 1] : prof[i];
                if (i < count - 1 && prof[i + 1] == prof[i]) b = prof[i];        // hard edge: one-sided
                if (i > 0 && prof[i - 1] == prof[i]) a = prof[i];
                Vector2 t = b - a;
                if (t.sqrMagnitude < 1e-12f) t = Vector2.up;
                Vector2 nn = new Vector2(t.y, -t.x).normalized;                 // outward for a profile going up
                normals2[i] = inward ? -nn : nn;
            }
            int baseIndex = v.Count;
            for (int s = 0; s <= segs; s++)
            {
                float ang = 2f * Mathf.PI * s / segs;
                float c = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                for (int i = 0; i < count; i++)
                {
                    float y = mirror ? 2f * Neck - prof[i].y : prof[i].y;
                    float ny = mirror ? -normals2[i].y : normals2[i].y;
                    v.Add(offset + new Vector3(prof[i].x * c, y, prof[i].x * sn));
                    n.Add(new Vector3(normals2[i].x * c, ny, normals2[i].x * sn));
                }
            }
            bool flip = inward ^ mirror;
            for (int s = 0; s < segs; s++)
            {
                for (int i = 0; i < count - 1; i++)
                {
                    int a = baseIndex + s * count + i, b = a + count, c2 = a + 1, d = b + 1;
                    if (!flip) { tri.Add(a); tri.Add(c2); tri.Add(b); tri.Add(b); tri.Add(c2); tri.Add(d); }
                    else { tri.Add(a); tri.Add(b); tri.Add(c2); tri.Add(b); tri.Add(d); tri.Add(c2); }
                }
            }
        }

        private static List<Vector2> DiscProfile(float y0, bool top)
        {
            float e = 0.004f;
            var p = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(DiscR - e, 0f), new Vector2(DiscR - e, 0f), new Vector2(DiscR, e),
                new Vector2(DiscR, e), new Vector2(DiscR, DiscT - e), new Vector2(DiscR, DiscT - e),
                new Vector2(DiscR - e, DiscT), new Vector2(DiscR - e, DiscT), new Vector2(0f, DiscT),
            };
            for (int i = 0; i < p.Count; i++) p[i] = new Vector2(p[i].x, y0 + (top ? DiscT - p[i].y : p[i].y));
            if (top) p.Reverse();
            return p;
        }

        /// <summary>
        /// Frame and glass, two submeshes: 0 = wood, 1 = glass. postPhase turns the four posts about the
        /// axis (the watch glass has two of them on its x axis, where the pivot pins go in).
        /// </summary>
        internal static Mesh BuildBody(float scale, float postPhase = Mathf.PI / 4f)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>();
            var wood = new List<int>(); var glass = new List<int>();
            Lathe(v, n, wood, DiscProfile(0f, false), 48, Vector3.zero);
            Lathe(v, n, wood, DiscProfile(GlassY1, true), 48, Vector3.zero);
            var post = new List<Vector2> { new Vector2(0f, GlassY0) };
            for (int i = 0; i <= 40; i++)
            {
                float s = i / 40f;
                post.Add(new Vector2(Catmull(PostCtrl, s), GlassY0 + (GlassY1 - GlassY0) * s));
            }
            post.Add(new Vector2(0f, GlassY1));
            for (int k = 0; k < 4; k++)
            {
                float a = postPhase + k * Mathf.PI / 2f;
                Lathe(v, n, wood, post, 12, new Vector3(Mathf.Cos(a) * PostR, 0f, Mathf.Sin(a) * PostR));
            }
            var outer = new List<Vector2>(); var inner = new List<Vector2>();
            for (int i = 0; i <= 80; i++)
            {
                float y = Mathf.Lerp(GlassY0, GlassY1, i / 80f);
                outer.Add(new Vector2(GlassR(y), y));
                inner.Add(new Vector2(InnerR(y), y));
            }
            Lathe(v, n, glass, outer, 48, Vector3.zero);
            Lathe(v, n, glass, inner, 48, Vector3.zero, inward: true);

            var mesh = new Mesh { name = "hourglass body" };
            Recentre(v, scale);
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(wood, 0);
            mesh.SetTriangles(glass, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------- sand ----------------

        private const int Grid = 400;
        private static float[] cumY, cumV;      // volume inside the glass from the neck up to y (one bulb)
        private static float fullVolume;

        private static void InitVolumes()
        {
            if (cumY != null) return;
            cumY = new float[Grid + 1];
            cumV = new float[Grid + 1];
            float y1 = GlassY1 - Wall;
            for (int i = 0; i <= Grid; i++)
            {
                cumY[i] = Mathf.Lerp(Neck, y1, i / (float)Grid);
                if (i > 0)
                {
                    float r = InnerR((cumY[i] + cumY[i - 1]) / 2f);
                    cumV[i] = cumV[i - 1] + Mathf.PI * r * r * (cumY[i] - cumY[i - 1]);
                }
            }
            fullVolume = VolumeUpTo(Neck + 0.80f * Half);       // a full glass fills 80% of the bulb's height
        }

        /// <summary>Volume of one bulb from the neck to height y (the bulbs are mirror images).</summary>
        private static float VolumeUpTo(float y)
        {
            float t = Mathf.Clamp01((y - Neck) / (GlassY1 - Wall - Neck)) * Grid;
            int i = Math.Min((int)t, Grid - 1);
            return Mathf.Lerp(cumV[i], cumV[i + 1], t - i);
        }

        private static float Solve(Func<float, float> vol, float target, float lo, float hi)
        {
            for (int k = 0; k < 40; k++)
            {
                float m = (lo + hi) / 2f;
                if (vol(m) < target) lo = m; else hi = m;
            }
            return (lo + hi) / 2f;
        }

        /// <summary>
        /// Sand for a glass standing upright: 'upper' = share of the sand in the upper bulb.
        /// mirror = the glass is upside down in its own frame (the "A" bulb is the lower one).
        /// </summary>
        internal static void BuildSand(Mesh mesh, float upper, bool running, bool mirror, float scale)
        {
            InitVolumes();
            upper = Mathf.Clamp01(upper);
            var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
            float top = GlassY1 - Wall;
            float heapTop = GlassY0 + Wall;

            float vu = upper * fullVolume;
            if (vu > 1e-9f)
            {
                // the crater opens as soon as sand runs
                float dip = 0.012f * Mathf.Min(1f, (1f - upper) * 6f);
                float L = Solve(y => VolumeUpTo(y) - Mathf.PI * InnerR(y) * InnerR(y) * dip / 3f, vu, Neck, top);
                var p = new List<Vector2> { new Vector2(0f, Neck) };
                for (int i = 0; i <= 30; i++)
                {
                    float y = Mathf.Lerp(Neck, L, i / 30f);
                    p.Add(new Vector2(InnerR(y), y));
                }
                p.Add(new Vector2(InnerR(L), L));
                p.Add(new Vector2(0f, Mathf.Max(Neck, L - dip)));
                Lathe(v, n, tri, p, 36, Vector3.zero, mirror: mirror);
            }

            float vl = (1f - upper) * fullVolume;
            if (vl > 1e-9f)
            {
                // lower bulb, measured from its cap: mirror image of the upper one about the neck
                float heap = 0.018f * Mathf.Min(1f, (1f - upper) * 4f + 0.3f);
                // sand from the lower cap up to the wall level y, plus the cone-shaped heap on top
                float bulb = VolumeUpTo(top);
                float B = Solve(y => bulb - VolumeUpTo(2f * Neck - y) + Mathf.PI * InnerR(y) * InnerR(y) * heap / 3f,
                                vl, heapTop, Neck);
                var p = new List<Vector2> { new Vector2(0f, heapTop) };
                for (int i = 0; i <= 30; i++)
                {
                    float y = Mathf.Lerp(heapTop, B, i / 30f);
                    p.Add(new Vector2(InnerR(y), y));
                }
                p.Add(new Vector2(InnerR(B), B));
                p.Add(new Vector2(0f, B + heap));
                Lathe(v, n, tri, p, 36, Vector3.zero, mirror: mirror);
                heapTop = B + heap;
            }

            if (running && vu > 1e-9f)
            {
                // as wide as the neck where it leaves the funnel, a little thinner where it lands
                float rTop = InnerR(Neck), rBottom = rTop * StreamTaper;
                var s = new List<Vector2>
                {
                    new Vector2(0f, heapTop), new Vector2(rBottom, heapTop), new Vector2(rBottom, heapTop),
                    new Vector2(rTop, Neck), new Vector2(rTop, Neck), new Vector2(0f, Neck),
                };
                Lathe(v, n, tri, s, 8, Vector3.zero, mirror: mirror);
            }

            mesh.Clear();
            Recentre(v, scale);
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
        }
    }
}
