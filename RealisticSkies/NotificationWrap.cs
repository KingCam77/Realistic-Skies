using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The game's notification scroll holds one short line; longer messages ran off its edges. Long
    /// ones are broken into lines that fit, and if that would take more than two lines, the letters
    /// are made a little smaller as well.
    /// </summary>
    [HarmonyPatch(typeof(NotificationUi), "ShowNotification", new[] { typeof(string), typeof(float) })]
    internal static class NotificationWrap
    {
        // what fits between the scroll's curls, measured against a line that just runs over them
        private const string Yardstick = "Nocturnal set to the night of Autumn 1";
        private const float FitsOfYardstick = 0.78f;
        private const int MaxLines = 2;

        private static float baseSize = -1f;
        private static float baseSpacing = -1f;

        [HarmonyPostfix]
        private static void Wrap(NotificationUi __instance, string notification)
        {
            if (string.IsNullOrEmpty(notification)) return;
            TextMesh tm = Traverse.Create(__instance).Field("text").GetValue<TextMesh>();
            if (tm == null || tm.font == null) return;
            if (baseSize < 0f) { baseSize = tm.characterSize; baseSpacing = tm.lineSpacing; }
            tm.characterSize = baseSize;
            tm.lineSpacing = baseSpacing;

            float limit = Width(tm, Yardstick) * FitsOfYardstick;
            if (limit <= 0f || Width(tm, notification) <= limit) return;

            List<string> lines = Lines(tm, notification, limit);
            float scale = 1f;
            while (lines.Count > MaxLines && scale > 0.6f)
            {
                scale -= 0.08f;
                lines = Lines(tm, notification, limit / scale);
            }
            tm.characterSize = baseSize * scale;
            tm.lineSpacing = baseSpacing * 0.9f;
            tm.text = string.Join("\n", lines.ToArray());
        }

        private static List<string> Lines(TextMesh tm, string text, float limit)
        {
            var lines = new List<string>();
            foreach (string para in text.Split('\n'))
            {
                var line = new StringBuilder();
                foreach (string word in para.Split(' '))
                {
                    string tryLine = line.Length == 0 ? word : line + " " + word;
                    if (line.Length > 0 && Width(tm, tryLine) > limit)
                    {
                        lines.Add(line.ToString());
                        line.Length = 0;
                        line.Append(word);
                    }
                    else
                    {
                        line.Length = 0;
                        line.Append(tryLine);
                    }
                }
                lines.Add(line.ToString());
            }
            return lines;
        }

        /// <summary>The width of a line in the font's own units (enough to compare lines with).</summary>
        private static float Width(TextMesh tm, string s)
        {
            Font f = tm.font;
            int size = tm.fontSize > 0 ? tm.fontSize : 0;
            f.RequestCharactersInTexture(s, size, tm.fontStyle);
            float w = 0f;
            foreach (char c in s)
            {
                if (f.GetCharacterInfo(c, out CharacterInfo ci, size, tm.fontStyle)) w += ci.advance;
            }
            return w;
        }
    }
}
