using System;
using System.Collections.Generic;

namespace RealisticSkies
{
    /// <summary>
    /// The texts of Training / Instrument Hints and of the journal's Handbook page.
    ///
    /// Item cards: what a chart or an instrument is for and how to use it, shown when the player
    /// looks at it (or holds it) and presses the Hint Key. Found by the item's own name
    /// (ShipItem.name), so the game's variants of one thing share a card.
    /// Topics: the four short lessons of the Handbook (the time of the prime meridian, latitude,
    /// longitude, dead reckoning).
    ///
    /// Plain words for a newcomer, short, with a worked example where there is room; the manuals
    /// (docs/MANUAL_*.md) and the README are the long versions.
    /// </summary>
    internal static class InstrumentHints
    {
        internal class Entry
        {
            public string title, text, actionLabel;
            public Action action;
        }

        private static Entry E(string title, string text, string actionLabel = null, Action action = null)
        {
            return new Entry { title = title, text = text, actionLabel = actionLabel, action = action };
        }

        // what a height is good for: under every instrument that takes one
        private const string HeightUse =
            "<b>What a height gives you</b>\n" +
            "Latitude, worked by yourself: at night the North Star's height is your latitude; at noon it is 90° less the Sun's height, plus the Sun's DEC from the Almanac.\n" +
            "Longitude, worked by the journal: take the height of a body standing east or west of you, and give the Sights page your latitude and the body's name.\n" +
            "More in the journal's Handbook.";

        // ---------------- the Handbook's lessons ----------------

        internal static readonly Entry[] Topics =
        {
            E("Prime meridian time",
                "Longitude is a difference of two times: your own local time, which the sky gives freely, and the time at the prime meridian, which you must carry with you.\n\n" +
                "<b>Winning it from the Moon.</b> The Moon crosses the stars by about her own width every hour, like the hand of a clock. " +
                "Take the cross-staff or the quintant, press L, pick the Sun or a star of today's lunar table in the Almanac, bring the Moon's near edge to it and take the reading. " +
                "The journal's Lunar distance page turns it into the time at the prime meridian.\n\n" +
                "You need no watch to take a lunar: the Almanac's table gives the hour.\n\n" +
                "<b>Take several.</b> One lunar is good to a few minutes, about a degree of longitude. Take three or four one after another: the journal takes their mean.\n\n" +
                "<b>Keeping it.</b> One button on that page sets your pocket watch, or the ship's watch glass, by the lunar. From then on it carries the time to your sights. " +
                "With neither at hand the journal keeps the time for two hours from the sight: long enough to fetch a watch and wind it, no longer.\n\n" +
                "No watch and no glass is true for long: they wander a few minutes a day, and every 4 minutes is a whole degree of longitude. Take a new lunar every few days."),

            E("Finding latitude",
                "Latitude is how far north you are. The journal does not work it out: you do, and you give it to the journal.\n\n" +
                "<b>The Sun at noon.</b> Take its height when it stands highest, due south. Latitude = 90° less the height, plus the Sun's DEC (Almanac, today's Sun table, row 12).\n" +
                "   Example: height 49°, DEC -7°. 90 - 49 = 41; 41 - 7 = 34° North.\n\n" +
                "<b>The North Star at night.</b> Its height is your latitude. It circles the pole a little, so the height can be up to 24' off.\n\n" +
                "<b>The North Star, exactly.</b> Twice a day the star stands level with the pole, and its height is your latitude with no error. " +
                "That is when Aries' AHA in the Almanac is near 20° or 200°, less your longitude east (or plus your longitude west).\n" +
                "   Example: at 3° East look for the hour when Aries stands nearest 17° or 197°, and take your sight in that hour.\n" +
                "Three degrees either way cost barely one minute of latitude.\n\n" +
                "<b>The sun compass</b> reads latitude from the noon shadow, less exactly: see its own note."),

            E("Finding longitude",
                "First you need the time at the prime meridian, kept by your pocket watch or the watch glass. Then find your own local time and compare.\n\n" +
                "Every hour of difference is 15°; every 4 minutes is 1°. If your time is ahead of the prime meridian's you are east of it; behind, west.\n" +
                "   Example: your local time is 12:10 when the watch says 12:00. Ten minutes ahead: 2°30' East.\n\n" +
                "Three ways to your local time, the surest first:\n\n" +
                "<b>1. A height.</b> Measure how high the Sun or a star stands, with the quadrant, the astrolabe, the cross-staff or the quintant. " +
                "The nearer the body is to due east or west, the smaller the error. On the journal's Sights page name the body, say east or west, give your latitude: the journal gives the longitude.\n\n" +
                "<b>2. The ring dial.</b> Set your latitude and today's date on it and read the Sun's time. The reading goes into the journal's Notes with a rough longitude.\n\n" +
                "<b>3. The nocturnal.</b> The roughest, but it needs no latitude: only the North Star and Corvin. Set the date first. " +
                "After midnight it is still the evening's date: at one in the morning of Autumn 4, set Autumn 3."),

            E("Dead reckoning",
                "Between sights, and under cloud, you reckon: where you were, which way you steered, how fast and how long.\n\n" +
                "<b>Keep the board.</b> Every two hours put a peg in the traverse board's rose for the course steered by the compass, and a peg in its grid for the speed by the chip log. (In Automatic mode the board does it for you.)\n\n" +
                "<b>Copy it.</b> At the end of the watch click the board with the journal in hand. The Traverse page adds the legs together: the course and miles made good, and how many minutes of arc you have gone north or south and east or west.\n\n" +
                "<b>Put it on the chart.</b> Start from your last sure position. Move it by the minutes of latitude and of longitude the journal gives (60 minutes to the degree), and mark the new place with the charting kit.\n\n" +
                "The board knows what you steered, not where wind and sea pushed you. Check your reckoning by the sky whenever it shows itself."),
        };

        // ---------------- the item cards ----------------

        private static readonly Dictionary<string, Entry> ByName = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase)
        {
            // ---- charts and books ----
            {
                "ocean map", E("Degrees and minutes",
                    "The chart is ruled in degrees of latitude (north and south) and longitude (east and west).\n\n" +
                    "A degree is divided into 60 minutes, not 100: 35°20' is 35 and a third degrees, and 35°30' is 35 and a half.\n\n" +
                    "Click to unfold it. With the charting kit you can mark your position and draw your course on it.")
            },
            {
                "Star Chart", E("The Star Map",
                    "A map of the sky: a chart of each pole and a band along the equator, with every constellation drawn and the bright stars named. Click to open it; Shift + right click brings it to your eye.\n\n" +
                    "To learn the sky faster, turn on the star labels: at night, look at a star or a planet and its name appears beside it, with the figure of its constellation.",
                    "Turn on star labels",
                    () =>
                    {
                        Plugin.configStarLabels.Value = true;
                        Plugin.configConstellationLines.Value = true;
                        Plugin.instance.Config.Save();
                    })
            },
            {
                "almanac", E("The Almanac",
                    "The tables of the sky: where the Sun, the Moon, the planets and the bright stars stand at every hour of every day, and the Moon's distances from them.\n\n" +
                    "What you will look up most:\n" +
                    "   The Sun's DEC, today's Sun table, row 12: for your latitude at noon.\n" +
                    "   The lunar table: which bodies to measure the Moon from today.\n\n" +
                    "Click to open it, scroll to turn the pages. Keep it in a pocket or lying near: the journal needs it for its sums.")
            },
            {
                "navigator's journal", E("The Navigator's Journal",
                    "Carry it in a pocket and your instruments write every reading into it. Press J to open it.\n\n" +
                    "<b>Latitude</b> you find yourself and write at the top of the page. The journal does not find it for you.\n" +
                    "<b>Lunar distance:</b> the time at the prime meridian from the Moon; one click sets your watch or glass by it.\n" +
                    "<b>Sights:</b> every height is a line. Name the body, say east or west, and it gives your longitude.\n" +
                    "<b>Traverse:</b> your dead reckoning from the traverse board.\n" +
                    "<b>Handbook:</b> these notes, and the lessons on latitude and longitude.\n\n" +
                    "It needs an Almanac at hand, and it takes your word for everything: a wrong star or a wrong latitude gives a wrong place.")
            },
            {
                "The Navigator's Companion", E("The Navigator's Companion",
                    "The manual of the old instruments: the Almanac, the journal, the glasses, the quadrant, the astrolabe, the cross-staff, lunar distances, the nocturnal, the ring dial and the traverse board, step by step, with pictures.\n\n" +
                    "Click to open it, scroll to turn the pages.")
            },
            {
                "The Modern Navigator", E("The Modern Navigator",
                    "The manual of the newer instruments: the quintant, the pocket watch, the chronometers and the watchmaker.\n\n" +
                    "Click to open it, scroll to turn the pages.")
            },

            // ---- taking the height ----
            {
                "quadrant", E("The Quadrant",
                    "A quarter circle with a plumb line: it measures how high a star or the Sun stands above the horizon.\n\n" +
                    "Sight the body along its edge and right click to pinch the line against the scale. The line swings with the ship: pinch it as it passes the middle. It reads to a quarter of a degree.\n\n" +
                    HeightUse)
            },
            {
                "astrolabe", E("The Mariner's Astrolabe",
                    "A heavy brass ring that hangs true by its own weight and measures the height of a star or the Sun. It needs no horizon, so it works on the darkest night. It reads to ten minutes.\n\n" +
                    "A star: hold right click to raise it, bring the star into the holes, let go.\n" +
                    "The Sun: never look at it. Turn your back to the Sun and look down at the instrument; bring the spot of sunlight onto the lower hole and let go.\n\n" +
                    HeightUse)
            },
            {
                "cross-staff", E("The Cross-Staff",
                    "A rod with a sliding cross-piece. It measures the angle between two things: the horizon and a star for a height, or the Moon and a star for a lunar distance.\n\n" +
                    "Scroll to slide the cross-piece (Shift: finely). Hold right click to bring it to your eye, lay the lower end on the horizon and the upper end on the body, and let go.\n" +
                    "Press L for a lunar distance, which gives the time at the prime meridian: right click the Sun or a star, then lay one end on it and the other on the Moon's near edge.\n\n" +
                    HeightUse)
            },
            {
                "quintant", E("The Quintant",
                    "The finest instrument for heights and lunar distances: it reads to minutes of arc.\n\n" +
                    "Scroll to raise or lower the crosshairs, click to take the reading.\n" +
                    "Press L for a lunar distance, which gives the time at the prime meridian: click a star or the Sun to lock onto it, then bring the Moon's edge to it and click.\n\n" +
                    HeightUse)
            },
            {
                "sun compass", E("The Sun Compass",
                    "At noon its shadow reads your latitude. But its rings are drawn for a Sun that stands on the equator: correct the reading by the Sun's DEC from the Almanac.\n\n" +
                    "<b>Autumn and Winter</b> (DEC south, with a minus): the Sun is lower, the shadow longer, the compass reads too high. Take the DEC away.\n" +
                    "   Example: the compass reads 39°, DEC is -7°. 39 - 7 = 32° North.\n\n" +
                    "<b>Spring and Summer</b> (DEC north, with a plus): the Sun is higher, the shadow shorter. Add the DEC.\n" +
                    "   Example: the compass reads 25°, DEC is +7°. 25 + 7 = 32° North.")
            },

            // ---- keeping time ----
            {
                "nocturnal", E("The Nocturnal",
                    "The night clock: your local time from the stars. The roughest way to a longitude, but it needs no latitude and no tables.\n\n" +
                    "<b>Set the date</b> with the scroll wheel. After midnight it is still the evening's date: at one in the morning of Autumn 4, set Autumn 3.\n" +
                    "<b>Take the time:</b> hold right click to raise it, handle down, with the North Star in the hole. Scroll to swing the arm until its edge touches Corvin, the brightest star of Corvin's Sloop, and let go.\n\n" +
                    "<b>Longitude:</b> compare with the time at the prime meridian on your watch or glass. Every 4 minutes of difference is 1°; ahead of the prime meridian is east.")
            },
            {
                "ring dial", E("The Ring Dial",
                    "A sundial that works anywhere: your local time by the Sun. A fair way to a longitude by day.\n\n" +
                    "<b>Set it:</b> scroll for your latitude, Shift + scroll for today's date. A wrong latitude or date gives a wrong time.\n" +
                    "<b>Read it:</b> face north and right click to unfold it. Hold right click and turn it until the spot of sunlight sits on the middle line, then let go.\n\n" +
                    "The reading goes into the journal's Notes, with a rough longitude from the time at the prime meridian on your watch or glass.\n\n" +
                    "No spot near noon, in shade or under cloud. With the spot on the line, the dial also points true north.")
            },
            {
                "watch glass", E("The Watch Glass",
                    "The ship's clock: an hour glass that turns itself every hour and strikes the bells of the watch. It keeps the time at the prime meridian, which every longitude needs.\n\n" +
                    "Right click to start or stop it. Nail it down in a seaway.\n\n" +
                    "Take a lunar distance, and the journal's Lunar distance page sets the glass by it. The journal then takes the hour of every sight from the glass.\n\n" +
                    "It wanders a few minutes a day, and 4 minutes is a degree of longitude: set it again from the Moon every few days.")
            },
            {
                "pocket watch", E("The Pocket Watch",
                    "It keeps the time at the prime meridian, which every longitude needs, to the minute.\n\n" +
                    "Scroll to wind it: a winding lasts a day and a half. Hold right click to bring it to your eye; scroll there to set the minute hand (Shift: the hour hand).\n\n" +
                    "Take a lunar distance, and the journal's Lunar distance page tells how far the watch is off and sets it for you. " +
                    "Every watch gains or loses a few minutes a day: take a new lunar every few days.")
            },

            // ---- steering and reckoning ----
            {
                "compass", E("The Compass",
                    "The needle points north. Set it where you can see it from the helm and steer by it.\n\n" +
                    "A course is named by its point (north-east) or in degrees from north, clockwise: east is 90°, south 180°, west 270°.")
            },
            {
                "chip log", E("The Chip Log",
                    "It measures the ship's speed through the water, in knots. Use it at the stern.\n\n" +
                    "Speed and course, written down every two hours, are your dead reckoning: where you ought to be until the sky tells you better. The traverse board keeps that record.")
            },
            {
                "traverse board", E("The Traverse Board",
                    "The helmsman's record of the watch: a peg in the rose for the course steered and a peg in the grid for the speed, every two hours.\n\n" +
                    "Right click it with empty hands to step up close; click a hole to put its peg in or take it out. With the journal in hand, click the board to copy it: " +
                    "the Traverse page tells how far north or south and east or west you have run.\n\n" +
                    "In Automatic mode the board keeps itself while you are aboard. More under Dead reckoning in the journal's Handbook.")
            },
            {
                "charting kit", E("The Charting Kit",
                    "Ink and rule for the chart: with it you can mark where you are and draw your course on an unfolded map.")
            },
            {
                "spyglass", E("The Spyglass",
                    "For the horizon: land, a sail or a harbour mouth, long before the bare eye finds them. Climb high and look ahead when you think land is near.")
            },
        };

        private static readonly Entry Glass = E("The Sand Glass",
            "Turn it over and the sand runs for half an hour or an hour, and a bell strikes when it runs out. Right click to turn it.\n\n" +
            "Take a sight, turn the glass at once, and you know exactly how much later your next sight was taken. On its side it stops; in a pocket it keeps running.");

        private static readonly Entry Chronometer = E("The Chronometer",
            "A clock that carries the time at the prime meridian across the sea. Compare it with your local time from the sky and you have your longitude: every 4 minutes of difference is 1°.\n\n" +
            "Each one gains or loses a little every day, at its own rate; the journal's Chronometers page keeps account of it. Only a watchmaker can regulate one.");

        /// <summary>Every item card, for the Handbook's list, in reading order.</summary>
        internal static IEnumerable<Entry> Cards()
        {
            foreach (Entry e in ByName.Values) yield return e;
            yield return Glass;
            yield return Chronometer;
        }

        /// <summary>The hint for an item, or null.</summary>
        internal static Entry Find(ShipItem item)
        {
            if (item == null) return null;
            if (item is ModItemTraverseBoard) return ByName["traverse board"];
            if (item is ModItemHourglass) return Glass;
            SaveablePrefab sp = item.GetComponent<SaveablePrefab>();
            if (sp != null && sp.prefabIndex >= 170 && sp.prefabIndex <= 172) return Chronometer;
            string name = item.name;
            if (string.IsNullOrEmpty(name)) return null;
            return ByName.TryGetValue(name.Trim(), out Entry e) ? e : null;
        }
    }
}
