# Realistic Skies
A complete overhaul of the skies in Sailwind adding more depth, realism, and interaction to stars.

# Features

## Sky

### Brand New Skybox
Realistic Skies features a completely brand new skybox build from real data to create a realistic, good looking, but unique sky. The new skybox also has some additional behaviors to improve the looks and bring it closer to reality.
The sky will rotate over the course of the year as the planet moves around the Sun, stars will rise and set through out the year, some constellations may only be visible later in the game.
Additionally the stars will dim when the moon is out, making fainter stars harder to see.

##### Config Options
- Disable Everything: *this is used to disable all changes to the sky, use if you only want the items. Default = false*
- Star Type: *this changes which skybox is displayed, New is the custom sky the mod focuses on, Real is the real world stars, Debug is a test pattern. Default = New*
- Year Length: *this changes how many days a year takes. Default = 92*
- Siderial Rotation: *this causes the stars to rotate over the year. Default = true*
- Star Dimming: *this causes the stars to dim when the moon is bright. Default = true*

### Moon Tweaks
The moon is resized to closer match the real size of the moon. To help with finding the much smaller moon, a glare is added to increase its apparent size without blocking the stars behind it.

##### Config Options
- Moon Size: *this setting shrinks the moon down to realistic size and enables the bloom. Default = true*

### Zodiacal Light
Realistic Skies also adds the zodiacal light, the band of brightness along the ecliptic.

##### Config Options
- Zodiacal Light: *this setting enables the zodiacal light. Default = true*

### Planets
Realistic Skies features a full solar system with 7 visible planets (to varying degrees of difficulty). These planets follow real paths through the stars and change brightness appropriately.

##### Config Options
- Planets: *this setting enables the planets. Default = true*

## Locations
Realistic Skies adds three merchants at each of the capital cities and one special location.

### Kicia Bay Observatory
Kicia Bay Observatory is a unique location built on the mountain next to Kicia Bay. Here is where the more niche instruments can be found that can be useful but not required.

#### Telescope
Kicia Bay Observatory also features a clock drive equitorial telescope that can be used to observe the heavens in detail. Its clock drive will track a particular spot in the sky throughout the sky without manual input. When interacting with the base **W**, **A**, **S** and **D** can be used to change the siderial hour angle and declination that its targeting.

## Items
Realistic Skies features a number of items which allow you to interact with the stars in more depth than the base game, primarily accurate and indepth celestial navigation mirroring real life practices.
**All items were designed with the new skybox in mind and may not work for real stars or differing year lengths**

### Quintant
The Quintant is by far the most important item added. It allows for precision measuring of heights and times simulateously. It can be found at all of the locations. The Quintant is safe to put in your inventory with a reading, it will only lose that reading upon a game start.

##### Controls
- Scroll: *this raises or lowers the crosshairs and declutter "shades"*
- Click: *this takes a reading/inspects the quintant*
- Scroll while inspecting: *this raises or lowers the actual shades*

##### Config Options
- Quintant Help: *This changes how much of the reading is displayed in text, None, Arc is just arc-minutes, Sec is arc-minutes and seconds, and full is both the full time and full angle. Default = Arc*

#### Almanac
The Almanac is just as useful as the Quintant and is required for preforming celestial navigation. It provides the necessary angles of the sky, the 51 brightest stars, and the 4 brightest planets. It can be found at all of the locations. Each Almanac is only good for a year and shows which one on the cover. (this only applies to planets, the stars dont change by year)\
**Only works with default star set but any year length**

##### Controls
- Click: *this opens the Almanac*
- Scroll while open: *this changes the current page*

#### Star Map
The Star Map is a simple map of the heavens. The y axis is the declination and the x axis is the siderial hour angle. While it can be used for identifiying stars, the projection can make it challenging. It can be found at all of the capital cities but not Kicia Bay Observatory.

##### Controls
- Click: *this opens the star map*

#### Planisphere
The Planisphere is a more advanced star map, showing you what is visible and any current time in an easier to read projection. It is only found at Kicia Bay Observatory.\
**Only works with default star set and the 92 day year** (with any year length but the day markers and scroll increment becomes inaccurate)

##### Controls
- Scroll: *this advances or decreases the rotation by exactly one hour*
- Click: *this rotates the planisphere to the south side*
- Scroll while rotated: *this advances or decreases the rotation by a fine tuning amount that can reach any day or hour combination*

