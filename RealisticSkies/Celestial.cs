using System;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Shared sun and moon ephemeris. Both the sky (MoonOrbitPatch) and the almanac use these
    /// functions, so whatever the almanac prints is exactly what the sky shows.
    ///
    /// Conventions (same as the rest of the mod):
    ///   t     = absolute game time in days = GameState.day + globalTime / 24
    ///   hour  = Sun.globalTime (the time the quintant clock shows)
    ///   GHA   = Greenwich hour angle, degrees, measured westward, 0..360
    ///   LHA   = GHA + longitude (east positive), so a body is on your meridian when LHA = 0
    ///   Dec   = declination, degrees, north positive
    /// </summary>
    public static class Celestial
    {
        // Vanilla Moon.Update advances the phase by 1/28 per game day.
        public const double SynodicMonth = 28.0;

        public static bool MoonReworkActive =>
            !Plugin.configDisable.Value && Plugin.configMoonRework.Value;

        public static double Now =>
            Sun.sun == null ? 0.0 : GameState.day + Sun.sun.globalTime / 24.0;

        // ---------------- Sun ----------------
        // Vanilla sun ("mean sun"): localTime = globalTime + longitude / 15, always on the celestial
        // equator, so noon is always at 12:00 local time.
        //
        // With "Axial Tilt" the sun also moves north and south over the year (seasons).
        // With "Equation of Time" it is placed where the author's elliptical orbit and the tilt really
        // put it, so it runs up to ~20 minutes ahead of or behind the clock during the year.
        // Its ecliptic longitude comes from the same home-planet orbit PlanetPatch uses, so the sun
        // stays on the same track as the planets.

        public static bool SunReworkActive =>
            !Plugin.configDisable.Value &&
            (Plugin.configAxialTilt.Value != 0f || Plugin.configEquationOfTime.Value);

        /// <summary>GHA of the vanilla clock sun.</summary>
        public static double MeanSunGHA(double hour) => Repeat(15.0 * (hour - 12.0), 360.0);

        public static double SunGHA(double t, double hour) => Repeat(MeanSunGHA(hour) + EquationOfTime(t), 360.0);

        public static double SunDec(double t)
        {
            if (Plugin.configDisable.Value) return 0.0;
            double eps = Plugin.configAxialTilt.Value * Mathf.Deg2Rad;
            if (eps == 0.0) return 0.0;
            double lambda = (Plugin.configEquationOfTime.Value ? SunLongitudeTrue(t) : SunLongitudeMean(t)) * Mathf.Deg2Rad;
            return Math.Asin(Math.Sin(eps) * Math.Sin(lambda)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// How many degrees the real sun is west of the clock sun (positive = sun is early, noon comes
        /// before 12:00). Divide by 15 for hours. Zero unless "Equation of Time" is on.
        /// </summary>
        public static double EquationOfTime(double t)
        {
            if (Plugin.configDisable.Value || !Plugin.configEquationOfTime.Value) return 0.0;
            double eps = Plugin.configAxialTilt.Value * Mathf.Deg2Rad;
            double lambda = SunLongitudeTrue(t) * Mathf.Deg2Rad;
            double ra = Math.Atan2(Math.Cos(eps) * Math.Sin(lambda), Math.Cos(lambda)) * Mathf.Rad2Deg;
            double e = SunLongitudeMean(t) - ra;
            return Repeat(e + 180.0, 360.0) - 180.0;
        }

        // Home planet orbit, same elements as PlanetPatch.planetHome (copied so this works with
        // the planets switched off).
        private const double HomeEcc = 0.0299;
        private const double HomePerihelion = 82.1461;   // omega, degrees (Omega = 0, inc = 0)
        private const double HomeTau = 0.781158692;
        private const double HomeN = 6.282937626;        // radians per year

        private static double YearOf(double t)
        {
            int len = Plugin.configYearLen.Value;
            return len > 0 ? t / len : 0.0;
        }

        /// <summary>Ecliptic longitude of the sun if the orbit were a circle, degrees.</summary>
        public static double SunLongitudeMean(double t)
        {
            double M = HomeN * (YearOf(t) + HomeTau);
            return Repeat(HomePerihelion + M * Mathf.Rad2Deg + 180.0, 360.0);
        }

        /// <summary>Ecliptic longitude of the sun on the real (elliptical) orbit, degrees.</summary>
        public static double SunLongitudeTrue(double t)
        {
            double M = Repeat(HomeN * (YearOf(t) + HomeTau), 2.0 * Math.PI);
            double E = M + HomeEcc * Math.Sin(M);
            for (int k = 0; k < 5; k++)
            {
                E -= (E - HomeEcc * Math.Sin(E) - M) / (1.0 - HomeEcc * Math.Cos(E));
            }
            double v = 2.0 * Math.Atan2(Math.Sqrt(1.0 + HomeEcc) * Math.Sin(E / 2.0), Math.Sqrt(1.0 - HomeEcc) * Math.Cos(E / 2.0));
            return Repeat(HomePerihelion + v * Mathf.Rad2Deg + 180.0, 360.0);
        }

        // Sky directions captured by SunTiltPatch before the sun is moved, used by the moon.
        internal static bool HaveSkyAxes;
        internal static Vector3 PoleAxis;      // axis the sky turns about (sunParent.right)
        internal static Vector3 NorthPole;     // celestial north pole
        internal static Vector3 MeanSunEq;     // direction of the clock sun on the equator

        // ---------------- Moon ----------------
        // Vanilla phase semantics are kept: phase 0 = full moon, 0.5 = new moon
        // (moonlight intensity in Moon.Update is |0.5 - phase| * 2).

        public static double MoonPhase(double t) =>
            Repeat(t / SynodicMonth + Plugin.configMoonEpoch.Value, 1.0);

        /// <summary>Angle the moon is east of the (clock) sun, degrees (180 at full moon).</summary>
        public static double MoonElongation(double t) => Repeat(180.0 + 360.0 * MoonPhase(t), 360.0);

        public static double MoonGHA(double t, double hour) => Repeat(MeanSunGHA(hour) - MoonElongation(t), 360.0);

        public static double MoonDec(double t)
        {
            double inc = Plugin.configMoonInclination.Value;
            if (inc == 0.0) return 0.0;
            return inc * Math.Sin(2.0 * Math.PI * t / DraconicMonth());
        }

        /// <summary>Illuminated fraction of the disc, 0..1.</summary>
        public static double MoonIllumination(double t) =>
            0.5 * (1.0 - Math.Cos(MoonElongation(t) * Mathf.Deg2Rad));

        /// <summary>Days since new moon.</summary>
        public static double MoonAge(double t) => Repeat(MoonPhase(t) - 0.5, 1.0) * SynodicMonth;

        /// <summary>
        /// Period after which the moon returns to the same node (same declination).
        /// The moon goes round the stars once per sidereal month; the nodes regress
        /// once per "Moon Node Period" days.
        /// </summary>
        public static double DraconicMonth()
        {
            double sidereal = SynodicMonth;
            if (Plugin.configSiderialToggle.Value && Plugin.configYearLen.Value > 0)
                sidereal = 1.0 / (1.0 / SynodicMonth + 1.0 / Plugin.configYearLen.Value);

            double node = Plugin.configMoonNodePeriod.Value;
            if (node <= 0.0) return sidereal;
            return 1.0 / (1.0 / sidereal + 1.0 / node);
        }

        /// <summary>
        /// Ecliptic (x toward longitude 0, z toward ecliptic north) to equatorial coordinates,
        /// turning about the equinox direction by the axial tilt. Matches EclipticTilt.Apply in the sky.
        /// </summary>
        public static Vector3 EclipticToEquatorial(Vector3 v)
        {
            float eps = Plugin.configDisable.Value ? 0f : Plugin.configAxialTilt.Value * Mathf.Deg2Rad;
            float c = Mathf.Cos(eps), s = Mathf.Sin(eps);
            return new Vector3(v.x, v.y * c - v.z * s, v.y * s + v.z * c);
        }

        /// <summary>
        /// GHA and Dec of a planet exactly as the Almanac computes them (ModItemAlmanac.calcPlanet),
        /// for any moment: day = whole game day, hour = clock time.
        /// </summary>
        public static void PlanetGHADec(Planet planet, int day, double hour, out double gha, out double dec)
        {
            int len = Plugin.configYearLen.Value;
            float yearTime = (float)((hour / 24.0 + day) / len);
            Vector3 home = PlanetPatch.calcPlanetPos(PlanetPatch.planetHome, yearTime);
            Vector3 target = PlanetPatch.calcPlanetPos(planet, yearTime);
            Vector3 Q = EclipticToEquatorial(target - home);

            double sidereal = hour;
            if (Plugin.configSiderialToggle.Value)
            {
                sidereal = hour + (day * 24.0 + hour) / len;
            }
            gha = Repeat(360.0 - Math.Atan2(Q.y, Q.x) * Mathf.Rad2Deg + sidereal * 15.0, 360.0);
            dec = Math.Asin(Q.z / Q.magnitude) * Mathf.Rad2Deg;
        }

        // ---------------- Sight reduction (used by the debug check) ----------------

        /// <summary>Computed altitude in degrees for an observer at lat/lon (east positive).</summary>
        public static double ComputedAltitude(double lat, double lon, double gha, double dec)
        {
            double lha = (gha + lon) * Mathf.Deg2Rad;
            double phi = lat * Mathf.Deg2Rad;
            double d = dec * Mathf.Deg2Rad;
            double s = Math.Sin(phi) * Math.Sin(d) + Math.Cos(phi) * Math.Cos(d) * Math.Cos(lha);
            return Math.Asin(Math.Max(-1.0, Math.Min(1.0, s))) * Mathf.Rad2Deg;
        }

        public static double Repeat(double v, double len)
        {
            v %= len;
            if (v < 0) v += len;
            return v;
        }

        /// <summary>Same look as the almanac columns: " 12°34.5" / "-12°34.5".</summary>
        public static string Format(double val)
        {
            bool neg = val < 0;
            double totalMin = Math.Round(Math.Abs(val) * 60.0, 1);
            int deg = (int)Math.Floor(totalMin / 60.0);
            double min = totalMin - deg * 60.0;
            deg %= 360;
            return (neg ? "-" : " ") + deg + "°" + min.ToString("00.0");
        }
    }
}
