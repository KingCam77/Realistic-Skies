using System;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The ring dial's numbers, as docs/make_ringdial.py builds and engraves it. The bridge's date
    /// scale is cut once, for a tilt of 23.44 degrees and the true (elliptical) orbit, so the dial
    /// does not follow a Custom tilt: set differently, it is simply wrong by that much.
    /// </summary>
    internal static class RingDialMath
    {
        internal const float R_MO = 0.075f, R_MI = 0.066f, T_M = 0.0045f;      // meridian ring
        internal const float R_EO = 0.0655f, R_EI = 0.0585f, T_E = 0.0045f;    // equatorial ring
        internal const float R_SPOT = R_EI;                                    // the spot lands on the inner face
        internal const double EngravedTilt = 23.44;
        internal const float HangY = 0.1046f;                                  // inside top of the shackle's ring, shackle space

        /// <summary>The Sun's declination the bridge is engraved with for day d of the year.</summary>
        internal static double EngravedDec(int d)
        {
            double eps = EngravedTilt * Mathf.Deg2Rad;
            double lambda = Celestial.SunLongitudeTrue(d + 0.5) * Mathf.Deg2Rad;
            return Math.Asin(Math.Sin(eps) * Math.Sin(lambda)) * Mathf.Rad2Deg;
        }

        /// <summary>Where the cursor's pinhole sits along the bridge (the dial's x) for day d.</summary>
        internal static float CursorX(int d)
        {
            // asked for several times a frame; the same day always gives the same answer
            int len = Plugin.configYearLen.Value;
            if (d != cursorDay || len != cursorLen)
            {
                cursorDay = d; cursorLen = len;
                cursorX = R_SPOT * (float)Math.Tan(EngravedDec(d) * Mathf.Deg2Rad);
            }
            return cursorX;
        }

        private static int cursorDay = int.MinValue, cursorLen;
        private static float cursorX;

        /// <summary>The shackle's ring in the dial's own space at a latitude.</summary>
        internal static Vector3 HangPoint(float latitude) => Quaternion.AngleAxis(-latitude, Vector3.forward) * new Vector3(0f, HangY, 0f);

        internal enum Fault { None, Low, Noon, Equinox, Miss, NoSun }

        internal const float HoleR = 0.00055f;                                  // the pinhole's radius
        internal const float RailH = 0.0028f;                                   // the shackle's rail round the meridian ring's rim
        internal const float MinVisible = 0.2f;                                 // less of the hole open than this: no reading

        internal struct Spot
        {
            internal Fault fault;
            internal Vector3 hit;       // on the inner face, dial space
            internal float th;          // degrees round the ring from the bottom hinge (noon), + afternoon
            internal float visible;     // how much of the pinhole the rings leave open to the Sun, 0..1
            internal float hours => 12f + th / 15f;
        }

        private static float SS(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Where a ray from p along d (both in a plane's coordinates a, b) first goes out through the
        /// circle of radius r about the origin; NaN if it never does.
        /// </summary>
        private static float Exit(float pa, float pb, float da, float db, float r)
        {
            float A = da * da + db * db;
            if (A < 1e-10f) return float.NaN;
            float B = pa * da + pb * db, C = pa * pa + pb * pb - r * r;
            float disc = B * B - A * C;
            if (disc < 0f) return float.NaN;
            float t = (-B + Mathf.Sqrt(disc)) / A;
            return t > 0f ? t : float.NaN;
        }

        /// <summary>How far an interval [u0, u1] (either order) lies outside [-h, h]; negative when they overlap.</summary>
        private static float Outside(float u0, float u1, float h)
        {
            float lo = Mathf.Min(u0, u1), hi = Mathf.Max(u0, u1);
            return Mathf.Max(lo - h, -h - hi);
        }

        /// <summary>
        /// How much the rings hide the Sun from a point p inside them (dial space), s toward the Sun,
        /// with a soft edge pen wide: the near side of the equatorial ring (its band, from the inner to
        /// the outer face) and the meridian ring with its rail. 0 open, 1 hidden.
        /// </summary>
        internal static void RingShade(Vector3 p, Vector3 s, float pen, out float eq, out float mer)
        {
            eq = mer = 0f;
            // the equatorial ring: a band |x| <= T_E/2 between radii R_EI and R_EO about the x axis
            float t1 = Exit(p.y, p.z, s.y, s.z, R_EI), t2 = Exit(p.y, p.z, s.y, s.z, R_EO);
            if (!float.IsNaN(t1) && !float.IsNaN(t2))
                eq = 1f - SS(-pen, pen, Outside(p.x + t1 * s.x, p.x + t2 * s.x, T_E / 2f));
            // the meridian ring: a band |z| <= T_M/2 between radii R_MI and R_MO + the rail, in the x/y plane
            t1 = Exit(p.x, p.y, s.x, s.y, R_MI); t2 = Exit(p.x, p.y, s.x, s.y, R_MO + RailH);
            if (!float.IsNaN(t1) && !float.IsNaN(t2))
                mer = 1f - SS(-pen, pen, Outside(p.z + t1 * s.z, p.z + t2 * s.z, T_M / 2f));
        }

        /// <summary>
        /// The spot through the pinhole, s = the Sun's direction in the dial's space (the bridge turned
        /// to face it), xp = the cursor's place. The rings close the hole from one edge as the Sun comes
        /// near the equator (the equatorial ring) or the meridian (the meridian ring, at noon); with too
        /// little of it open, there is no spot to read.
        /// </summary>
        internal static Spot Compute(Vector3 s, float xp, float sunAltitude)
        {
            var r = new Spot();
            if (sunAltitude < 1f) { r.fault = Fault.Low; return r; }
            float r2 = Mathf.Sqrt(s.y * s.y + s.z * s.z);
            if (r2 < 1e-4f) { r.fault = Fault.NoSun; return r; }
            float t = R_EI / r2;
            r.hit = new Vector3(xp - s.x * t, -s.y * t, -s.z * t);
            r.th = Mathf.Atan2(-r.hit.z, -r.hit.y) * Mathf.Rad2Deg;

            // sample the pinhole (in the bridge's plane, which faces the Sun) and see how much the rings leave open
            Vector3 w = new Vector3(0f, -s.z / r2, s.y / r2);
            float open = 0f, eqSum = 0f, merSum = 0f;
            int n = 0;
            const int K = 4;
            for (int i = -K; i <= K; i++)
            {
                for (int j = -K; j <= K; j++)
                {
                    float u = (float)i / K, v = (float)j / K;
                    if (u * u + v * v > 1f) continue;
                    Vector3 h = new Vector3(xp + u * HoleR, 0f, 0f) + w * (v * HoleR);
                    RingShade(h, s, 0.00005f, out float eq, out float mer);
                    open += (1f - eq) * (1f - mer);
                    eqSum += eq; merSum += mer;
                    n++;
                }
            }
            r.visible = open / n;
            if (r.visible < MinVisible) { r.fault = eqSum >= merSum ? Fault.Equinox : Fault.Noon; return r; }
            if (Mathf.Abs(r.hit.x) > T_E / 2f - 0.0002f) r.fault = Fault.Miss;
            return r;
        }
    }
}
