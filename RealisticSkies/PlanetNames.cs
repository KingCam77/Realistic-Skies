using System;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Names of the seven visible planets. The Almanac numbers them 1-7 skipping the home planet
    /// (so PlanetPatch.planet4 is "Planet 3"); with "Planet Names" on they get proper names.
    /// </summary>
    public static class PlanetNames
    {
        private static readonly string[] names =
        {
            "Vesta",      // Planet 1: the swift one, never far from the sun
            "Lux",        // Planet 2: the brightest, morning and evening star
            "Ember",      // Planet 3: the orange one
            "Sister",     // Planet 4: pale and faint, Ember's sister
            "Leviathan",  // Planet 5: the bright blue one
            "Pilgrim",    // Planet 6: slow golden wanderer
            "Teardrop",   // Planet 7: faint, best seen through the observatory telescope
        };

        /// <summary>number = Almanac planet number, 1..7</summary>
        public static string Get(int number)
        {
            if (Plugin.configPlanetNames.Value && number >= 1 && number <= names.Length)
            {
                return names[number - 1];
            }
            return "Planet " + number;
        }
    }
}
