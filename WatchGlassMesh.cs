using System.Collections.Generic;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The stand of the watch glass: a heavy round plinth with two turned uprights, and a pivot pin
    /// from each upright into the glass's frame. The glass itself is the sand glass model
    /// (<see cref="HourglassMesh"/>) scaled up, turning about the x axis between the pins.
    /// Built standing on the floor (y = 0) and moved down by 'lift'; the pivot is at <see cref="AxleY"/> above the floor.
    /// </summary>
    internal static class WatchGlassMesh
    {
        internal const float GlassScale = 1.5f;
        internal const float BaseR = 0.17f, BaseH = 0.055f;
        // the frame sweeps a circle of this radius about the pivot when it turns
        internal static readonly float Sweep = Mathf.Sqrt(Sq(HourglassMesh.Height / 2f * GlassScale) + Sq(HourglassMesh.DiscR * GlassScale));
        internal static readonly float AxleY = BaseH + Sweep + 0.012f;
        internal static readonly float UprightX = HourglassMesh.DiscR * GlassScale + 0.03f;
        internal static readonly float Top = AxleY + HourglassMesh.Height / 2f * GlassScale;   // top of the resting frame

        private static float Sq(float x) => x * x;

        // upright: radius over s = 0..1 from its foot to the finial
        private static readonly Vector2[] UprightCtrl =
        {
            new Vector2(0f, 0.017f), new Vector2(0.05f, 0.017f), new Vector2(0.08f, 0.011f), new Vector2(0.2f, 0.0085f),
            new Vector2(0.4f, 0.012f), new Vector2(0.55f, 0.0075f), new Vector2(0.68f, 0.0095f), new Vector2(0.8f, 0.013f),
            new Vector2(0.86f, 0.009f), new Vector2(0.92f, 0.0065f), new Vector2(0.97f, 0.008f), new Vector2(1f, 0.004f),
        };

        private static float Catmull(Vector2[] p, float x)
        {
            int n = p.Length, i = 0;
            while (i < n - 2 && x > p[i + 1].x) i++;
            float t = Mathf.Clamp01((x - p[i].x) / (p[i + 1].x - p[i].x));
            float p0 = p[System.Math.Max(i - 1, 0)].y, p1 = p[i].y, p2 = p[i + 1].y, p3 = p[System.Math.Min(i + 2, n - 1)].y;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
        }

        /// <summary>The stand, moved down by 'lift' (the item's origin is the middle of its box).</summary>
        internal static Mesh BuildStand(float lift)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();

            // plinth: a rounded foot, a hollow (cove) and a raised top step
            var b = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(BaseR - 0.006f, 0f), new Vector2(BaseR - 0.006f, 0f),
            };
            for (int i = 0; i <= 8; i++)
            {
                float a = -Mathf.PI / 2f + Mathf.PI / 2f * i / 8f;
                b.Add(new Vector2(BaseR - 0.006f + 0.006f * Mathf.Cos(a), 0.006f + 0.006f * Mathf.Sin(a)));
            }
            b.Add(new Vector2(BaseR, 0.018f));
            for (int i = 0; i <= 10; i++)           // cove up to the top
            {
                float a = Mathf.PI / 2f * i / 10f;
                b.Add(new Vector2(BaseR - 0.016f + 0.016f * Mathf.Cos(a), 0.018f + 0.022f * Mathf.Sin(a)));
            }
            b.Add(new Vector2(BaseR - 0.016f, 0.040f));
            b.Add(new Vector2(BaseR - 0.022f, 0.040f)); b.Add(new Vector2(BaseR - 0.022f, 0.040f));
            for (int i = 0; i <= 8; i++)            // bead round the top step
            {
                float a = Mathf.PI / 2f * i / 8f;
                b.Add(new Vector2(BaseR - 0.030f + 0.008f * Mathf.Cos(a), 0.040f + (BaseH - 0.040f) * Mathf.Sin(a)));
            }
            b.Add(new Vector2(BaseR - 0.030f, BaseH)); b.Add(new Vector2(0f, BaseH));
            // the profile goes up the outside: faces outwards as built
            HourglassMesh.Lathe(v, n, tri, b, 64, Vector3.zero);

            // uprights from the plinth top past the pivot, with a finial
            float h = AxleY + 0.06f - BaseH;
            var up = new List<Vector2> { new Vector2(0f, BaseH) };
            for (int i = 0; i <= 48; i++)
            {
                float s = i / 48f;
                up.Add(new Vector2(Catmull(UprightCtrl, s), BaseH + h * s));
            }
            up.Add(new Vector2(0f, BaseH + h));
            for (int side = -1; side <= 1; side += 2)
            {
                HourglassMesh.Lathe(v, n, tri, up, 16, new Vector3(side * UprightX, 0f, 0f));
            }

            // pivot pins: from the frame's post into the upright, and a knob outside it
            float postX = HourglassMesh.PostR * GlassScale;
            var pin = new List<Vector2>
            {
                new Vector2(0f, postX), new Vector2(0.0045f, postX), new Vector2(0.0045f, postX),
                new Vector2(0.0045f, UprightX), new Vector2(0.0045f, UprightX), new Vector2(0.0085f, UprightX + 0.012f),
                new Vector2(0.0085f, UprightX + 0.012f), new Vector2(0.0110f, UprightX + 0.022f), new Vector2(0.0110f, UprightX + 0.022f),
                new Vector2(0.0070f, UprightX + 0.031f), new Vector2(0f, UprightX + 0.034f),
            };
            for (int side = -1; side <= 1; side += 2)
            {
                int start = v.Count, startT = tri.Count;
                HourglassMesh.Lathe(v, n, tri, pin, 16, Vector3.zero);
                // lathe axis y -> world x (side) at the pivot height: a proper rotation keeps the winding
                for (int i = start; i < v.Count; i++)
                {
                    Vector3 p = v[i], q = n[i];
                    v[i] = side > 0 ? new Vector3(p.y, AxleY - p.x, p.z) : new Vector3(-p.y, AxleY + p.x, p.z);
                    n[i] = side > 0 ? new Vector3(q.y, -q.x, q.z) : new Vector3(-q.y, q.x, q.z);
                }
            }

            for (int i = 0; i < v.Count; i++) v[i] = new Vector3(v[i].x, v[i].y - lift, v[i].z);
            var mesh = new Mesh { name = "watch glass stand" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
