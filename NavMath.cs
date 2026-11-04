using System;
using System.Globalization;

namespace RealisticSkies
{
    /// <summary>
    /// The journal's sums, done as from the Almanac so they give the same numbers as working by hand
    /// from its pages: a lunar distance looked up in the lunar table, and the longitude from an
    /// altitude with the Almanac's GHA and Dec; and reading and writing angles and times.
    ///
    /// Times are "absolute hours": day * 24 + hour at the zero meridian. Angles in degrees.
    /// </summary>
    internal static class NavMath
    {
        // ---------------- text ----------------

        /// <summary>"12 34.5", "12°34.5'", "12:34.5", "-12 30" or "12.575" -> degrees. Empty or bad: NaN.</summary>
        internal static double ParseAngle(string text)
        {
            if (string.IsNullOrEmpty(text)) return double.NaN;
            string s = text.Trim().Replace('°', ' ').Replace('\'', ' ').Replace(':', ' ').Replace(',', '.');
            bool neg = s.StartsWith("-");
            if (neg) s = s.Substring(1);
            string[] p = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 0 || p.Length > 2) return double.NaN;
            if (!double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return double.NaN;
            double m = 0;
            if (p.Length == 2 && !double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out m)) return double.NaN;
            if (m < 0 || m >= 60) return double.NaN;
            double v = d + m / 60.0;
            return neg ? -v : v;
        }

        /// <summary>"14:08:30", "14:08", "14 08 30" or "14.5" -> hours. Empty or bad: NaN.</summary>
        internal static double ParseTime(string text)
        {
            if (string.IsNullOrEmpty(text)) return double.NaN;
            string s = text.Trim().Replace(':', ' ').Replace(',', '.');
            bool neg = s.StartsWith("-");
            if (neg) s = s.Substring(1);
            string[] p = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 0 || p.Length > 3) return double.NaN;
            double v = 0, scale = 1;
            for (int i = 0; i < p.Length; i++)
            {
                if (!double.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double x)) return double.NaN;
                if (i > 0 && (x < 0 || x >= 60)) return double.NaN;
                v += x / scale;
                scale *= 60;
            }
            return neg ? -v : v;
        }

        /// <summary>12°34.5'</summary>
        internal static string Angle(double deg)
        {
            if (double.IsNaN(deg)) return "--";
            bool neg = deg < 0;
            double tm = Math.Round(Math.Abs(deg) * 60.0, 1);
            int d = (int)Math.Floor(tm / 60.0);
            double m = tm - d * 60.0;
            return (neg ? "-" : "") + d + "°" + m.ToString("00.0", CultureInfo.InvariantCulture) + "'";
        }

        /// <summary>Time of day hh:mm:ss (wrapped into 0..24).</summary>
        internal static string Clock(double hours)
        {
            if (double.IsNaN(hours)) return "--:--:--";
            hours = Celestial.Repeat(hours, 24.0);
            int s = (int)Math.Round(hours * 3600.0) % 86400;
            return (s / 3600).ToString("00") + ":" + (s / 60 % 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        /// <summary>A time difference: "+12m30s" / "-1h02m05s".</summary>
        internal static string Span(double hours)
        {
            if (double.IsNaN(hours)) return "--";
            int s = (int)Math.Round(Math.Abs(hours) * 3600.0);
            string sign = hours < 0 ? "-" : "+";
            if (s >= 3600) return sign + (s / 3600) + "h" + (s / 60 % 60).ToString("00") + "m" + (s % 60).ToString("00") + "s";
            return sign + (s / 60) + "m" + (s % 60).ToString("00") + "s";
        }

        // ---------------- time ----------------

        internal static double NowAbs => Sun.sun == null ? 0 : GameState.day * 24.0 + Sun.sun.globalTime;

        /// <summary>
        /// A time of day at the zero meridian placed on the right day: the one that puts it closest to
        /// now (a watch or a glass tells the hour, not the date).
        /// </summary>
        internal static double OnNearestDay(double hourOfDay)
        {
            double now = NowAbs;
            double day = Math.Floor(now / 24.0);
            double best = double.NaN, bestErr = double.MaxValue;
            for (int d = -1; d <= 1; d++)
            {
                double t = (day + d) * 24.0 + Celestial.Repeat(hourOfDay, 24.0);
                double e = Math.Abs(t - now);
                if (e < bestErr) { bestErr = e; best = t; }
            }
            return best;
        }

        private static void Split(double abs, out int day, out double hour)
        {
            day = (int)Math.Floor(abs / 24.0);
            hour = abs - day * 24.0;
        }

        // ---------------- lunar distance -> time ----------------

        /// <summary>The Almanac's printed distance (to 0.1') at a whole hour.</summary>
        internal static double TableDistance(int body, double absHour)
        {
            Split(absHour, out int day, out double hour);
            return Math.Round(Lunar.Distance(body, day, hour) * 600.0) / 600.0;
        }

        /// <summary>
        /// Time at the zero meridian when the moon was 'distance' from the body, found as a navigator
        /// does: the two whole hours of the table that bracket the measured distance, and a linear
        /// split between them. Of all the matches within 12 hours of 'approx', the nearest wins.
        /// </summary>
        internal static bool LunarTime(int body, double distance, double approx, out double gmt)
        {
            gmt = double.NaN;
            if (double.IsNaN(distance) || double.IsNaN(approx)) return false;
            double start = Math.Floor(approx) - 12;
            double best = double.MaxValue;
            double d0 = TableDistance(body, start);
            for (int k = 0; k < 24; k++)
            {
                double h0 = start + k;
                double d1 = TableDistance(body, h0 + 1);
                double lo = Math.Min(d0, d1), hi = Math.Max(d0, d1);
                if (distance >= lo && distance <= hi && hi - lo > 1e-6)
                {
                    double t = h0 + (distance - d0) / (d1 - d0);
                    if (Math.Abs(t - approx) < best)
                    {
                        best = Math.Abs(t - approx);
                        gmt = t;
                    }
                }
                d0 = d1;
            }
            return !double.IsNaN(gmt);
        }

        /// <summary>12°34.5' N / S</summary>
        internal static string Lat(double deg) => double.IsNaN(deg) ? "--" : Angle(Math.Abs(deg)) + (deg >= 0 ? " N" : " S");

        /// <summary>12°34.5' E / W</summary>
        internal static string Lon(double deg) => double.IsNaN(deg) ? "--" : Angle(Math.Abs(deg)) + (deg >= 0 ? " E" : " W");

        /// <summary>-180..180</summary>
        internal static double Wrap180(double v) => Celestial.Repeat(v + 180.0, 360.0) - 180.0;

        // ---------------- altitude sight -> longitude ----------------

        internal struct Sight
        {
            public bool ok;
            public string problem;
            public double gha, dec, lha, longitude, localTime;
        }

        /// <summary>
        /// Longitude from the altitude of a body at a known time, with the Almanac's GHA and Dec:
        /// cos LHA = (sin h - sin lat sin dec) / (cos lat cos dec); a body east of the meridian has
        /// LHA = 360 - that. Longitude = LHA - GHA (east positive, as the game's globe).
        /// For the sun, local (apparent) time = 12 h + LHA / 15.
        /// </summary>
        internal static Sight Reduce(int body, double gmtAbs, double altitude, double latitude, bool east)
        {
            var r = new Sight();
            if (double.IsNaN(gmtAbs) || double.IsNaN(altitude) || double.IsNaN(latitude))
            {
                r.problem = double.IsNaN(latitude) ? "latitude?" : double.IsNaN(gmtAbs) ? "time?" : "altitude?";
                return r;
            }
            int day = (int)Math.Floor(gmtAbs / 24.0);
            double hour = gmtAbs - day * 24.0;
            Lunar.BodyGHADec(body, day, hour, out r.gha, out r.dec);
            double phi = latitude * Math.PI / 180.0, d = r.dec * Math.PI / 180.0, h = altitude * Math.PI / 180.0;
            double den = Math.Cos(phi) * Math.Cos(d);
            if (Math.Abs(den) < 1e-9)
            {
                r.problem = "no answer at the pole";
                return r;
            }
            double c = (Math.Sin(h) - Math.Sin(phi) * Math.Sin(d)) / den;
            if (c > 1.0 || c < -1.0)
            {
                r.problem = "never at that altitude here";
                return r;
            }
            double lha = Math.Acos(c) * 180.0 / Math.PI;
            r.lha = east ? 360.0 - lha : lha;
            r.longitude = Wrap180(r.lha - r.gha);
            r.localTime = Celestial.Repeat(12.0 + r.lha / 15.0, 24.0);
            r.ok = true;
            return r;
        }
    }
}
