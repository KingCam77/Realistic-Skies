# Realistic Skies

A complete overhaul of the skies in Sailwind adding more depth, realism, and interaction to stars.

*Version 2.3.0F-2 is a fork of [KingCam77's Realistic Skies](https://github.com/KingCam77/Realistic-Skies), made with his permission. It adds navigation by the Sun and the Moon, lunar distances, seasons, sailors' constellations and their legends, and the instruments of the old navigators, from the cross-staff to the chronometer.

<img width="2344" height="1439" alt="image" src="https://github.com/user-attachments/assets/03c9f2bc-2c1d-4061-bfab-828deabe0dd1" />

## Getting started

<img width="1954" height="1439" alt="image" src="https://github.com/user-attachments/assets/930bc8fe-6989-4ee3-bd75-a896cd2bf87a" />

Everything is sold in the three capitals, on the stall with the Star Map. To learn how to use it all, buy **The Navigator's Companion**, and outside the Magellan difficulty also **The Modern Navigator**. These two illustrated scrolls teach every instrument step by step, in game. This page is the quick reference.

Navigation comes down to two things:
- **Latitude** (how far north or south you are) comes from the height of the Sun at noon or of a star at night.
- **Longitude** (how far east or west) is the difference between your local time and the time at the zero meridian. Your local time comes from the sky. The zero meridian's time you have to carry with you, in a clock or a glass, or win back from the Moon.

## Difficulty
One setting picks the age you sail in:

| Difficulty | What you get |
|---|---|
| **Magellan** | The age before clocks. You sail with sand glasses, the cross-staff, the astrolabe, the quadrant and the nocturnal. No quintants, pocket watches or chronometers are sold, and a chronometer you already own runs a few days and stops. Your time at the zero meridian comes from lunar distances: work one out in the journal and set the ship's watch glass by it. |
| **Chronometers** (default) | Clocks are good but not perfect. Chronometers and pocket watches gain or lose a little every day, each at its own rate. Chronometers cost twice as much. The cabin boy winds them; you wind your pocket watch. |
| **Vanilla** | Perfect clocks that need no winding, and the base game's sun. |
| **Custom** | The options in the Custom section decide. |

Magellan and Chronometers also turn on the seasons and the equation of time (see *Seasons*).

##### Config Options
- Difficulty: *Magellan, Chronometers, Vanilla or Custom. Default = Chronometers*
- Cabin Boy Winds Chronometers: *off: you wind each chronometer yourself with the scroll wheel. Default = true*
- Custom only: Axial Tilt, Equation of Time, Quintant Clock, Chronometers (Perfect / Imperfect / Relic), Pocket Watches (Perfect / Imperfect / Dead), Chronometer Rate Jumps, Journal Keeps Time By The Glass.

## Sky

### Brand New Skybox
Realistic Skies features a completely brand new skybox build from real data to create a realistic, good looking, but unique sky. The new skybox also has some additional behaviors to improve the looks and bring it closer to reality.
The sky will rotate over the course of the year as the planet moves around the Sun, stars will rise and set through out the year, some constellations may only be visible later in the game.
Additionally the stars will dim when the moon is out, making fainter stars harder to see.

##### Config Options
- Disable Everything: *this is used to disable all changes to the sky, use if you only want the items. Default = false*
- Star Type: *this changes which skybox is displayed, New is the custom sky the mod focuses on, Real is the real world stars, Debug is a test pattern. Default = New*
- Year Length: *92 (the islands' year of four 23-day seasons) or 365 days. Default = 92*
- Siderial Rotation: *this causes the stars to rotate over the year. Default = true*
- Star Dimming: *this causes the stars to dim when the moon is bright. Default = true*

### Moon
The moon is resized to closer match the real size of the moon. To help with finding the much smaller moon, a glare is added to increase its apparent size without blocking the stars behind it.

The moon also keeps steady time now: it no longer drifts while you are in a shop or at the shipyard. Its path is tilted and slowly turns, so full moons ride high in some seasons and low in others. The Almanac predicts all of it.

##### Config Options
- Moon Size: *shrinks the moon to its real size and adds the glare. Default = true*
- Moon Orbit Rework: *the steady, tilted moon described above. Needed for the moon tables in the Almanac. Default = true*
- Moon Inclination: *how far north or south of the sun's path the moon can go, in degrees. Default = 12*
- Moon Node Period: *days for the moon's path to turn once around the sky. Default = 184*
- Moon Phase Offset: *moon phase on day 0 (0 = full moon, 0.5 = new moon). Default = 0*

### Seasons
The planet's axis can be tilted, as Earth's is. The sun then climbs higher in summer and lower in winter, and days grow longer and shorter. Day 0 of every year is the autumn equinox. With the Equation of Time on as well, noon drifts up to about 20 minutes before or after 12:00 over the year. The Almanac's sun table already includes both effects, so just use it.

##### Config Options
- Set by the **Difficulty**: on in Magellan and Chronometers, off in Vanilla. In Custom:
- Axial Tilt: *tilt of the axis in degrees; 0 keeps the vanilla sun. Default = 23.44*
- Equation of Time: *noon drifts around 12:00 over the year. Default = true*

### Constellations and Legends

<img width="2114" height="1439" alt="image" src="https://github.com/user-attachments/assets/9c87d144-c2ea-4d0f-8c10-47ce643e6949" />

Sailors have drawn their own figures among the stars: Corvin's Sloop and the Widow's Grief around the North Star, the Harpoon with bright Oriens, the Ship's Cat, the Haymaker and his Bull, the Cobra near the southern pole, and more. Several of them point the way: the Arrowhead to the North Star, the Cobra's tail to the southern pole, Oriens to Milnead. Thirty-two stars and all seven planets have sailors' names. **[LORE.md](LORE.md)** describes all of them, and the **Sea Legends** scroll tells them in game.

##### Config Options
- Star Names: *use the sailors' names for the Almanac's lettered stars (Willow, Corvin, Lulu...); the Almanac keeps the letter in brackets. Default = true*

### Star Labels (training)

<img width="1186" height="969" alt="image" src="https://github.com/user-attachments/assets/47ea9aad-5401-4ac0-8461-4f7efe9ddcb1" />

Turn on Star Labels and, at night, look at a star or a planet: its name fades in next to it, with its atlas designation, SHA and Dec, and the constellation it belongs to, whose figure is drawn on the sky around it. Bright stars answer from a couple of degrees away, faint ones only when you look right at them. It is handy for learning the sky, and you don't need an instrument.

#### Reading atlas designations
A designation like **2Y-061-165** tells you where to look in the Celestial Atlas:
- **2**: the group of atlas pages (1 = 21h to 3h, 2 = 3h to 9h, 3 = 9h to 15h, 4 = 15h to 21h).
- **Y**: the band of declination: A = +60 to +90, C = +30 to +60, B = 0 to +30, Y = 0 to -30, X = -30 to -60, Z = -60 to -90.
- **061**: right ascension, 6.1h (SHA = 360 - 15 x 6.1 = 268.5).
- **165**: declination, 16.5 degrees, south because of the Y.

##### Config Options
- Star Labels: *show the names of stars and planets you look at. Default = false*
- Constellation Lines: *also draw the figure of the constellation you look at. Default = true*

### Zodiacal Light
Realistic Skies also adds the zodiacal light, the band of brightness along the ecliptic.

##### Config Options
- Zodiacal Light: *this setting enables the zodiacal light. Default = true*

### Planets
Realistic Skies features a full solar system with 7 visible planets (to varying degrees of difficulty). These planets follow real paths through the stars and change brightness appropriately. Sailors call them Vesta, Lux, Ember, Sister, Leviathan, Pilgrim and Teardrop.

##### Config Options
- Planets: *this setting enables the planets. Default = true*
- Planet Names: *use the names above instead of Planet 1-7. Default = true*

## Navigating by the Sun and the Moon
The Sun and the Moon can be used just like the stars and planets. Every page of the Almanac has **Sun** and **Moon** tables next to the bright stars: their hour angle and declination for every hour, and how much of the moon is lit. A noon sight of the Sun gives your latitude. The Moon is often up in daylight, so you can take the Sun and the Moon together for a full fix without waiting for night.

### Lunar distances
Before good clocks, navigators told the time from the Moon. It slides across the stars by about its own width every hour, so the angle between the Moon and a star works like the hand of a clock. The Almanac's **Lunar Distances** page, after the planets, lists that angle for every hour.

1. Take the quintant or the cross-staff and press **L**.
2. Pick the Sun or a star from today's lunar table.
3. Bring the near edge of the Moon onto it and take the reading.
4. The Navigator's Journal looks it up for you and gives the time at the zero meridian. You can also do it by hand: find the two hours your distance falls between in the table and split the difference.

Turn your glass or note your watch the moment you take the sight, and carry that time to your next sights.

##### Config Options
- Lunar Mode Key: *switches the quintant and the cross-staff to lunar distance. Default = L*

## Locations
Realistic Skies adds three merchants at each of the capital cities and one special location.

### The watchmaker
An old craftsman with a small stall in each capital, there from 7:00 to 18:00. Hold out a pocket watch or a chronometer to him and he offers to regulate it for about a third of its price. Four hours later it is back on his stall: right to the second, wound, and keeping much better time. For a chronometer he writes a certificate into your journal.

##### Config Options
- Watchmaker Look: *how he looks, in the format of the Sailwind Player Model mod's Character line. Empty = the default old craftsman*

### Kicia Bay Observatory
Kicia Bay Observatory is a unique location built on the mountain next to Kicia Bay. Here is where the more niche instruments can be found that can be useful but not required.

#### Telescope
Kicia Bay Observatory also features a clock drive equitorial telescope that can be used to observe the heavens in detail. Its clock drive will track a particular spot in the sky throughout the sky without manual input. When interacting with the base **W**, **A**, **S** and **D** can be used to change the siderial hour angle and declination that its targeting.

## Items
Realistic Skies features a number of items which allow you to interact with the stars in more depth than the base game, primarily accurate and indepth celestial navigation mirroring real life practices.\
**All items were designed with the new skybox in mind and may not work for real stars or differing year lengths**

### Books and charts

#### Almanac
The Almanac is required for celestial navigation. It gives where every body of the sky stands at every hour: the 51 brightest stars, the 4 brightest planets, the Sun and the Moon, and the lunar distances. It can be found at all of the locations. Each Almanac is good for one year, shown on the cover (only the planets change from year to year).\
**Only works with default star set but any year length**

##### Controls
- Click: *this opens the Almanac*
- Scroll while open: *this changes the current page*

#### Navigator's Journal
About 130 crowns. Carry it, in hand or in a pocket, and your instruments write every reading into it with the time from your watch or glass; you can correct any value. Then sit down with the Almanac and it works out your sights:
- **Lunar distance:** the time at the zero meridian, and how far off your watch is. One click sets the watch, or the ship's glass, by it.
- **Sights:** every height is a line. Name the body and say whether it stood east or west of you, and you get your latitude and longitude.
- **Chronometers:** each chronometer's error and rate, from the watchmaker's certificate or from comparing it with your watch.
- **Notes:** free writing; the nocturnal's readings and today's date go here too.

The journal works out sums only with an Almanac at hand: held, in a pocket, or lying within a few steps. It takes your word for everything, so name the wrong star or side and you get the wrong position.

##### Controls
- J, or right click / R with it in hand: *opens or closes the journal*
- Escape: *closes it*

##### Config Options
- Journal Key: *Default = J*

#### Star Map
The Star Map is a map of the heavens: a chart of each pole and a band along the equator, with every constellation drawn in and the bright stars named. It can be found at all of the capital cities but not Kicia Bay Observatory.

##### Config Options
- Constellation Star Chart: *use the star chart with the constellation figures instead of the original one. Default = true*

##### Controls
- Click: *this opens the star map*

#### The Navigator's Companion and The Modern Navigator
Two illustrated manuals that teach the instruments step by step:
- **The Navigator's Companion** (about 150 crowns, every difficulty): the Almanac, the journal, the glasses, the quadrant, the astrolabe, the cross-staff, lunar distances and the nocturnal.
- **The Modern Navigator** (about 200 crowns, not sold in Magellan): the quintant, the pocket watch, the chronometers and the watchmaker.

#### Sea Legends
A scroll of the sailors' sky, about 280 crowns: every constellation with an engraving of its figure, how to find it and where it points, the names of the stars and planets, and the legends behind them.

##### Controls (all three scrolls)
- Click: *opens the scroll*
- Scroll while open: *turns the pages*
- Right click / R while holding it: *rolls it up or opens it again*

### Taking the height

#### Quintant
The Quintant measures heights and lunar distances precisely. It can be found at all of the locations (but not in Magellan). The Quintant is safe to put in your inventory with a reading; it only loses the reading when you start the game again.

##### Controls
- Scroll: *this raises or lowers the crosshairs and declutter "shades"*
- Click: *this takes a reading/inspects the quintant*
- Scroll while inspecting: *this raises or lowers the actual shades*
- L: *lunar distance mode: click a star or the Sun to lock onto it, then bring the edge of the Moon to it and click*

##### Config Options
- Quintant Help: *how much of the reading is shown in text: None, Arc (arc-minutes), Sec (minutes and seconds) or Full (angle and time). Default = Arc*
- Quintant Clock: *(Custom only) the quintant also shows the time with every reading. Default = false*

#### Quadrant
The game's quadrant, now with a light plumb line that swings with the ship and settles slowly. Right click to pinch the line against the scale, and the height goes into the journal. It reads to a quarter of a degree: a fair instrument in harbour or a calm, a rough one at sea.

#### Cross-staff
Jacob's staff, about 70 crowns: a rod with a cross-piece that slides along it. The nearer the cross-piece is to your eye, the wider the angle between its ends. Use it for heights and lunar distances. It is exactly as true as you lay its ends.

##### Controls
- Scroll: *slide the cross-piece (Shift: finely)*
- Hold right click: *bring it to your eye. Lay the lower end on the horizon and the upper on the body, and let go*
- L: *lunar distance: right click the Sun or a star, then lay one end on it and the other on the Moon's near edge*

#### Mariner's astrolabe
About 90 crowns: a heavy brass ring that hangs straight from your thumb, with a turning arm and a pinhole at each end. It needs no horizon, so it works on the darkest night. At sea it swings with the ship: catch the body as the swing passes the middle. Its scale counts from the top, as the old pilots counted; the journal writes down the height. It reads to ten minutes of arc.

##### Controls
- Hold right click: *raise it to your eye and bring the star into the holes, then let go*
- The Sun: *never look at it. Turn your back to the Sun and look down at the instrument. The sunlight through the upper hole falls on the lower vane as a bright spot: bring it onto the lower hole, with an even ring of light around it, and let go. It works like a mirror: look lower and the spot climbs*

#### Sun compass
The game's sun compass reads your latitude from the noon shadow, but its scale assumes the Sun stands on the equator. With the seasons on, scroll while holding it to set it for the day's declination of the Sun from the Almanac (Shift: finely), and it reads true again.

### Keeping time

#### Nocturnal
The night clock, about 50 crowns: a brass plate with a toothed hour disc and a long arm. Sight the North Star through the hole in its middle, swing the arm onto **Corvin**, the brightest star of Corvin's Sloop, and read your local time where the arm crosses the disc. You need no horizon and no tables. Compare it with the time at the zero meridian and you have a rough longitude every clear night. Far to the south Corvin sets for part of the night, and then you have to wait for it.

##### Controls
- Scroll: *set the night's date (after midnight it is still the evening's date)*
- Hold right click: *raise it, handle down, with the North Star in the hole*
- Scroll while raised: *swing the arm until its edge touches Corvin (Shift: finely), then let go*

#### Half-Hour Glass and Hour Glass
About 100 and 150 crowns. Turn one over and the sand runs for half an hour or an hour of game time, and a bell strikes when it runs out. Take a sight, turn the glass at once, and work out the sums at your leisure: you know exactly how much later the bell rang. A glass lying on its side stops; in your pocket it keeps running.

##### Controls
- Right click / R while holding it: *turns the glass over*

#### Watch Glass
About 300 crowns: an hour glass on a heavy stand that turns itself every hour and strikes the bells of the watch: 2, 4, 6, then 8 bells at the end of the watch. Set it on deck (nail it down if the sea is up) and start it at a known hour, or set it by a lunar distance from the journal. It is no clock, though: it wanders a few minutes a day, so set it again from the Moon every few days.

##### Controls
- Right click: *starts it, or stops it*

##### Config Options
- Watch Glass Wakes Captain: *wake up at eight bells when you sleep aboard. Default = false*

#### Pocket Watch
The lunarian's deck watch, about 220 crowns, readable to the minute. Every watch gains or loses at its own rate, a few minutes a day, and it stops when it runs down (a winding lasts a day and a half). Find its error from a lunar distance and it carries the time to your next sights.

Model: [Pocket Watch](https://polyhaven.com/a/pocket_watch) by PierreB3D, Poly Haven (CC0).

##### Controls
- Scroll while holding it: *winds the watch*
- Hold right click / R: *brings it to your eye*
- Scroll while at your eye: *sets the minute hand (Shift: the hour hand)*

#### Chronometers
Every chronometer has its own number (look at it to see which), its own rate and a little wander. Carry two or three and compare them in the journal. Only the watchmaker can regulate one.

#### Planisphere
The Planisphere is a more advanced star map, showing you what is visible and any current time in an easier to read projection. It is only found at Kicia Bay Observatory.\
**Only works with default star set and the 92 day year** (with any year length but the day markers and scroll increment becomes inaccurate)

##### Controls
- Scroll: *this advances or decreases the rotation by exactly one hour*
- Click: *this rotates the planisphere to the south side*
- Scroll while rotated: *this advances or decreases the rotation by a fine tuning amount that can reach any day or hour combination*

### A steady hand
While an instrument is at your eye, the view turns slower and the mouse's jitter is smoothed out, so you can hold a star or the Moon's edge.

##### Config Options
- Aim Sensitivity: *how fast the view turns while aiming. Default = 0.5*
- Aim Smoothing: *how much the mouse is smoothed while aiming; 0 = none. Default = 0.06*

### Lamp hooks
The quintant, the cross-staff, the astrolabe and the nocturnal can hang on lamp hooks.

<br/>
<br/>
<br/>
