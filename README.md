# Realistic Skies 2.3.0F

A fork of KingCam77's Realistic Skies, made with his permission. You can navigate by a sun and moon now, a way to find longitude without a clock added, sailors' constellations with their legends, and a scroll that tells it all.

## Navigating by the sun and the moon
- **Sun and Moon tables in the Almanac.** Every page now lists the sun's and the moon's hour angle (AHA) and declination for every hour, next to the bright stars, and how much of the moon is lit. Use them like the planet columns.
- **Daylight fixes.** A noon sight of the sun gives your latitude, and the moon is often up during the day: take the sun and the moon together for a full position without waiting for night.
- **A steadier moon.** The moon no longer drifts while you are in a shop or at the shipyard. Its orbit is tilted and slowly turns, so full moons ride high or low depending on the season, and the Almanac predicts all of it.

## Longitude without a clock: lunar distances
- Hold the quintant and press **L** to switch it to lunar distance mode. Click a star (or the sun) to lock onto it, then click the moon: the quintant shows the angle between them.
- The Almanac has a new **Lunar Distances** page after the planets: the distance from the moon to the sun and to up to 8 bright stars, for every hour. Find your two hours, split the difference, and you have the time at the zero meridian, and with it your longitude.
- Turn off **Quintant Clock** in the config if you want the full challenge: the quintant no longer tells you the time, so a chronometer or the moon has to.

## Seasons (optional)
- **Axial Tilt**: tilt the planet's axis and the sun climbs higher in summer and lower in winter, days grow and shrink, and the planets' path tilts against the stars. Off by default (Earth is 23.4).
- **Equation of Time**: noon drifts up to about 20 minutes before or after 12:00 over the year, as on a real planet. The Almanac's sun table already includes it.

## Learning the sky
- **21 constellations** drawn by sailors across the whole sky: Corvin's Sloop, Morvath's Maw, the Widow's Grief, the Northern Bow and the Arrowhead around the North Star; the Harpoon, Ship's Cat, the Mouse, the Gull, the Pipe, the Whirlpool, the Fisherman and his Net and the Lug Sail around Oriens; the Scorpion, the Hunting Horn, the Witch's Hat, the Harp and the Haymaker on the other half of the sky; and the Bull, the Cobra and the Fiddler Crab in the far south.
- **Names for the stars and planets.** 32 stars have sailors' names (Willow, Corvin, Lantern, Mouser, Oboshi, Lulu...), and the seven planets are Vesta, Lux, Ember, Sister, Leviathan, Pilgrim and Teardrop. The Almanac shows the names with the old letters in brackets, so nothing you knew is lost (turn off **Star Names** to go back to letters).
- **New Star Map.** Both poles and the equator band with every constellation drawn in and the bright stars named.
- **Sea Legends**, a new scroll sold in the capitals, alongside the Star Map (about 280 crowns). Every constellation with an engraving of its figure, how to find it and what it points to (the Arrowhead and the Northern Bow to the North Star, the Cobra to the southern pole, Oriens to Milnead), the names of the stars and planets, and the old tales. Scroll to turn the pages; right click or **R** rolls it up while you hold it.
- **Star Labels** (training, off by default): at night, look at a star or a planet and its name, atlas designation, SHA and Dec fade in next to it, with its constellation drawn on the sky.

## Lore
- Each region named the part of the sky it knows best: the sailors of **Aestrin** gave plain sea names to the stars of the north and around Oriens, the stargazers of **Al'Ankh** named the evening and southern stars in their own tongue, and the stars of the Cobra carry the names of the **Emerald Archipelago**, where her legend is told.
- Six legends: Corvin's Sloop (the captain, his wife Willow and the sea monster Morvath), the Witch's Hat, the Ship's Cat and the Mouse, the Fisherman and the Gull, the Haymaker and the Bull, and the Cobra.

## Fixes
- Planets follow their orbits more accurately (the innermost planet could be off by several degrees).

## Config
- New options: Moon Orbit Rework, Moon Inclination, Moon Node Period, Moon Phase Offset, Axial Tilt, Equation of Time, Quintant Clock, Lunar Mode Key, Star Labels, Constellation Lines, Constellation Star Chart, Star Names, Planet Names. The defaults keep the vanilla sun and the quintant clock.
- Your existing config carries over.
