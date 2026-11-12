using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Lunar distances: the angle between the moon and a star (or the sun). The moon moves against
    /// the stars by about 0.7 deg per hour, so a measured distance plus the Almanac table gives the
    /// time at the prime meridian without a clock.
    /// </summary>
    public static class Lunar
    {
        public struct Star
        {
            public string name;     // name shown to the player (lore name if there is one)
            public string letter;   // the Almanac / atlas designation, e.g. "A"
            public double sha;
            public double dec;
        }

        /// <summary>Index used for the sun in lock / table code.</summary>
        public const int SunIndex = -1;

        private static List<Star> stars;

        /// <summary>The 51 bright stars, read from the Almanac's own "Bright Stars" page.</summary>
        public static List<Star> Stars
        {
            get
            {
                if (stars == null)
                {
                    stars = LoadCatalog();
                }
                return stars;
            }
        }

        public static string Name(int index) =>
            index == SunIndex ? "Sun" : IsPlanet(index) ? PlanetNames.Get(index - PlanetBase) : Stars[index].name;

        // ---------------- planets, as bodies for a sight ----------------
        // A planet is body PlanetBase + its Almanac number (1..7), well clear of the star indices, so a
        // saved sight keeps its body whatever the star catalogue holds. Only the four planets the
        // Almanac tabulates can be worked: Planet 2, 3, 5 and 6 (Lux, Ember, Leviathan, Pilgrim).

        public const int PlanetBase = 1000;
        public static readonly int[] AlmanacPlanets = { 2, 3, 5, 6 };

        public static bool IsPlanet(int index) => index > PlanetBase && index <= PlanetBase + 7;

        /// <summary>The Sun, a star of the catalogue, or a planet of the Almanac.</summary>
        public static bool IsBody(int index) =>
            index == SunIndex || (index >= 0 && index < Stars.Count) || (IsPlanet(index) && Array.IndexOf(AlmanacPlanets, index - PlanetBase) >= 0);

        private static Planet PlanetOf(int number)
        {
            switch (number)       // the Almanac skips the home planet: its Planet 3 is planet4, and so on
            {
                case 1: return PlanetPatch.planet1;
                case 2: return PlanetPatch.planet2;
                case 3: return PlanetPatch.planet4;
                case 4: return PlanetPatch.planet5;
                case 5: return PlanetPatch.planet6;
                case 6: return PlanetPatch.planet7;
                default: return PlanetPatch.planet8;
            }
        }

        /// <summary>
        /// Sailors' names for the Almanac's lettered stars (see LORE.md). Letters are case sensitive:
        /// "W" and "w" are different stars.
        /// </summary>
        private static readonly Dictionary<string, string> LoreNames = new Dictionary<string, string>
        {
            // Corvin's Sloop and its legend (Aestrin)
            { "A", "Willow" },      // the captain's wife, watching the Sloop sail away
            { "D", "Sickle" },      // the sickle-shaped tail fin of Morvath, all Captain Corvin ever saw of her
            { "I", "Morvath" },     // the sea monster herself, at the gaping maw
            { "W", "Corvin" },      // the captain, brightest star of his Sloop
            { "Z", "Lantern" },     // the Sloop's stern lantern
            { "h", "Flint" },       // the Arrowhead
            // around Oriens (Aestrin)
            { "F", "Lashing" },     // where the Harpoon's barbs are lashed to the shaft
            { "G", "Barb" },        // the tip of the Harpoon's lower barb
            { "L", "Spearhead" },   // the Harpoon's point
            { "J", "Mouser" },      // the Ship's Cat
            { "t", "Eddy" },        // the Whirlpool
            { "n", "Cork" },        // a float of the Fisherman's Net
            { "T", "Peak" },        // peak of the Lug Sail
            { "s", "Tack" },        // tack of the Lug Sail
            // the other half of the sky
            { "X", "Dhanab" },      // Al'Ankh: "tail" (of the Scorpion)
            { "U", "Buq" },         // Al'Ankh: "horn, trumpet"
            { "x", "Sada" },        // Al'Ankh: "echo"
            { "a", "Witchlight" },  // the light at the crown of the Witch's Hat
            { "B", "Watar" },       // Al'Ankh: "string" (of the Harp)
            { "N", "Naghma" },      // Al'Ankh: "melody"
            { "P", "Snath" },       // the Haymaker's scythe handle
            { "O", "Heel" },        // heel of the scythe blade
            // the far south
            { "o", "Ain" },         // Al'Ankh: "eye" (of the Bull)
            { "r", "Qarn" },        // Al'Ankh: "horn"
            { "H", "Lulu" },        // Al'Ankh: "pearl", held by the Fiddler Crab
            { "R", "Mikhlab" },     // Al'Ankh: "claw"
            { "E", "Oboshi" },      // Emerald Archipelago: "tail star" (of the Cobra)
            { "k", "Kashira" },     // Emerald Archipelago: "head"
        };

        /// <summary>"Willow (A)" for the Almanac's star list, or just the letter.</summary>
        public static string DisplayNameWithLetter(string letter)
        {
            string shown = DisplayName(letter);
            return shown == letter ? letter : shown + " (" + letter + ")";
        }

        /// <summary>Lore name for an Almanac star designation, or the designation itself.</summary>
        public static string DisplayName(string letter)
        {
            if (Plugin.configStarNames.Value && LoreNames.TryGetValue(letter, out string lore))
            {
                return lore;
            }
            return letter;
        }

        // ---------------- positions ----------------

        /// <summary>GHA of Aries, same as the Almanac's Aries column.</summary>
        public static double AriesGHA(int day, double hour)
        {
            double sidereal = hour;
            int len = Plugin.configYearLen.Value;
            if (Plugin.configSiderialToggle.Value && len > 0)
            {
                sidereal = hour + (day * 24.0 + hour) / len;
            }
            return Celestial.Repeat(sidereal * 15.0, 360.0);
        }

        public static void BodyGHADec(int index, int day, double hour, out double gha, out double dec)
        {
            double t = day + hour / 24.0;
            if (index == SunIndex)
            {
                gha = Celestial.SunGHA(t, hour);
                dec = Celestial.SunDec(t);
                return;
            }
            if (IsPlanet(index))
            {
                Celestial.PlanetGHADec(PlanetOf(index - PlanetBase), day, hour, out gha, out dec);      // as the Almanac prints it
                return;
            }
            Star s = Stars[index];
            gha = Celestial.Repeat(AriesGHA(day, hour) + s.sha, 360.0);
            dec = s.dec;
        }

        /// <summary>Angle between the moon and a star / the sun, degrees.</summary>
        public static double Distance(int index, int day, double hour)
        {
            double t = day + hour / 24.0;
            BodyGHADec(index, day, hour, out double gha, out double dec);
            return Angle(gha, dec, Celestial.MoonGHA(t, hour), Celestial.MoonDec(t));
        }

        private static double moonSD = double.NaN;

        /// <summary>
        /// The moon's semi-diameter in degrees: its angular radius as seen in the game. The game's
        /// moon hangs at a fixed distance, so it doesn't change (the phase only changes what is lit).
        /// Measured once off the moon's own mesh; NaN until the moon exists.
        /// </summary>
        public static double MoonSemiDiameter
        {
            get
            {
                if (!double.IsNaN(moonSD) || Moon.instance == null) return moonSD;
                MeshFilter mf = Moon.instance.GetComponent<MeshFilter>();
                Camera cam = Camera.main;
                if (cam == null) return moonSD;
                float radius;
                if (mf != null && mf.sharedMesh != null)
                {
                    Vector3 e = mf.sharedMesh.bounds.extents;
                    Vector3 sc = Moon.instance.transform.lossyScale;
                    radius = Mathf.Max(e.x * Mathf.Abs(sc.x), e.y * Mathf.Abs(sc.y));
                }
                else
                {
                    Renderer rd = Moon.instance.GetComponent<Renderer>();
                    if (rd == null) return moonSD;
                    radius = Mathf.Min(rd.bounds.extents.x, Mathf.Min(rd.bounds.extents.y, rd.bounds.extents.z));
                    if (radius <= 0f) radius = Mathf.Max(rd.bounds.extents.x, rd.bounds.extents.y);
                }
                float dist = Vector3.Distance(Moon.instance.transform.position, cam.transform.position);
                if (dist <= 0f || radius <= 0f) return moonSD;
                moonSD = Math.Atan(radius / dist) * Mathf.Rad2Deg;
                Debug.Log("RealisticSkies: the moon's semi-diameter is " + Celestial.Format(moonSD) + "'");
                return moonSD;
            }
        }

        /// <summary>Forget the measured semi-diameter (the moon was resized).</summary>
        public static void ResetMoonSemiDiameter() => moonSD = double.NaN;

        public static double Angle(double gha1, double dec1, double gha2, double dec2)
        {
            double d1 = dec1 * Mathf.Deg2Rad, d2 = dec2 * Mathf.Deg2Rad;
            double c = Math.Sin(d1) * Math.Sin(d2) + Math.Cos(d1) * Math.Cos(d2) * Math.Cos((gha1 - gha2) * Mathf.Deg2Rad);
            return Math.Acos(Math.Max(-1.0, Math.Min(1.0, c))) * Mathf.Rad2Deg;
        }

        /// <summary>World direction of a body with the given GHA and Dec (needs the sun patch's sky axes).</summary>
        public static Vector3 WorldDirection(double gha, double dec, double hour)
        {
            // GHA grows westward and the sky turns by AngleAxis(-15 deg/h, axis), so a body
            // (gha - meanSunGHA) degrees west of the clock sun is turned by the negative of that.
            float west = (float)(gha - Celestial.MeanSunGHA(hour));
            Vector3 eq = Quaternion.AngleAxis(-west, Celestial.PoleAxis) * Celestial.MeanSunEq;
            float d = (float)dec * Mathf.Deg2Rad;
            return (Mathf.Cos(d) * eq + Mathf.Sin(d) * Celestial.NorthPole).normalized;
        }

        // ---------------- which stars go in the table ----------------

        /// <summary>
        /// Up to 'count' bright stars whose distance from the moon changes steadily over both days and
        /// stays between 15 and 120 degrees, so every hour can be looked up without ambiguity, and that
        /// changes fast enough (0.4 deg per hour or more) to give a usable time.
        /// The catalogue is in order of brightness, so the brightest suitable stars come first.
        /// </summary>
        public static List<int> TableStars(int day1, int count, ICollection<int> exclude)
        {
            var result = new List<int>();
            for (int i = 0; i < Stars.Count && result.Count < count; i++)
            {
                if (exclude.Contains(i) || Math.Abs(Stars[i].dec) > 70.0)
                {
                    continue;
                }
                if (Usable(i, day1))
                {
                    result.Add(i);
                }
            }
            return result;
        }

        public static bool Usable(int index, int day1)
        {
            double first = Distance(index, day1, 0.0);
            double prev = first;
            if (prev < 15.0 || prev > 120.0) return false;
            int sign = 0;
            for (int h = 1; h < 48; h++)
            {
                double d = Distance(index, day1 + h / 24, h % 24);
                if (d < 15.0 || d > 120.0) return false;
                int s = Math.Sign(d - prev);
                if (sign != 0 && s != sign) return false;
                sign = s;
                prev = d;
            }
            // slow-moving pairs give the time badly: ask for at least 0.4 deg per hour on average
            return Math.Abs(prev - first) >= 0.4 * 47.0;
        }

        // ---------------- catalogue ----------------

        private static List<Star> LoadCatalog()
        {
            var list = new List<Star>();
            try
            {
                if (!Patches.itemPrefabs.TryGetValue(701, out GameObject almanac))
                {
                    Debug.LogWarning("RealisticSkies: almanac prefab not loaded, no star catalogue");
                    return list;
                }
                Transform names = almanac.transform.GetChild(1).GetChild(5).Find("Star Names");
                string[] n = Lines(names.GetComponent<TextMesh>().text);
                string[] sha = Lines(names.Find("Star SHA").GetComponent<TextMesh>().text);
                string[] dec = Lines(names.Find("Star DEC").GetComponent<TextMesh>().text);
                int count = Math.Min(n.Length, Math.Min(sha.Length, dec.Length));
                for (int i = 0; i < count; i++)
                {
                    list.Add(new Star { name = DisplayName(n[i]), letter = n[i], sha = ParseAngle(sha[i]), dec = ParseAngle(dec[i]) });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not read the star catalogue: " + e.Message);
            }
            return list;
        }

        private static string[] Lines(string text)
        {
            var result = new List<string>();
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length > 0)
                {
                    result.Add(line);
                }
            }
            return result.ToArray();
        }

        /// <summary>"-55°47.9" -> -55.798</summary>
        private static double ParseAngle(string s)
        {
            s = s.Trim();
            bool neg = s.StartsWith("-");
            s = s.TrimStart('-', '+', ' ');
            string[] parts = s.Split('°');
            double deg = double.Parse(parts[0], CultureInfo.InvariantCulture);
            double min = parts.Length > 1 && parts[1].Length > 0 ? double.Parse(parts[1], CultureInfo.InvariantCulture) : 0.0;
            double v = deg + min / 60.0;
            return neg ? -v : v;
        }
    }
}
