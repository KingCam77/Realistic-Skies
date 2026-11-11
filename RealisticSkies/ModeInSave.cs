using System;
using HarmonyLib;

namespace RealisticSkies
{
    /// <summary>
    /// The Difficulty belongs to the saved game, not to the mod's settings: it is written into the
    /// save (GameState.modData) when a game begins, and read from there ever after. So a Lost at Sea
    /// game in Magellan's age does not change the age of another save.
    ///
    /// The Difficulty setting is what a new game begins with. Changed while a game is being played,
    /// it changes that game (and stays the default for new ones). A save made before this was kept
    /// takes the setting the first time it is played, and keeps it.
    /// </summary>
    internal static class ModeInSave
    {
        private const string Key = "RealisticSkies.Difficulty";

        private static string cachedText;
        private static Difficulty cached;

        internal static Difficulty Level
        {
            get
            {
                var data = GameState.modData;
                if (data != null && data.TryGetValue(Key, out string text))
                {
                    if (!ReferenceEquals(text, cachedText))
                    {
                        cachedText = text;
                        try { cached = (Difficulty)Enum.Parse(typeof(Difficulty), text); }
                        catch (Exception) { cached = Plugin.configDifficulty.Value; }
                    }
                    return cached;
                }
                Difficulty d = Plugin.configDifficulty.Value;
                // an older save, played for the first time since: it keeps what it was played with
                if (data != null && GameState.playing && !GameState.currentlyLoading) Set(d);
                return d;
            }
        }

        internal static void Set(Difficulty d)
        {
            if (GameState.modData == null) return;
            GameState.modData[Key] = d.ToString();
        }

        /// <summary>The setting was changed: in a running game, that game changes with it.</summary>
        internal static void SettingChanged()
        {
            if (GameState.playing) Set(Plugin.configDifficulty.Value);
        }

        /// <summary>A new game begins with the setting; the Lost at Sea start with the era chosen in its menu.</summary>
        [HarmonyPatch(typeof(StartMenu), "StartNewGame")]
        private static class NewGame
        {
            private static void Postfix()
            {
                Set(LostAtSeaMenu.Armed ? LostAtSeaMenu.Era : Plugin.configDifficulty.Value);
            }
        }
    }
}
