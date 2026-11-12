using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace RealisticSkies
{
    /// <summary>
    /// The game counts days from nought and writes that number in its log book ("day 0" on the first
    /// day). The journal counts the days of the voyage from 1. Only what the log book WRITES is changed
    /// here: the day sheets, the trade receipts and the mission log show the day of the voyage, from 1,
    /// the same number as the journal. GameState.day and everything kept in the save stay as the game
    /// has them, so other mods and old saves are not touched.
    /// </summary>
    [HarmonyPatch]
    internal static class LogBookDays
    {
        /// <summary>A day as the game keeps it, as the log book shows it: the day of the voyage, from 1.</summary>
        public static int Shown(int gameDay) => gameDay - Calendar.StartDay + 1;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(DayLog), "UpdateTexts", new[] { typeof(int) });
            yield return AccessTools.Method(typeof(GPButtonDayLogDay), "UpdateText");
            yield return AccessTools.Method(typeof(TradeReceiptsUI), "ShowReceipt");
            yield return AccessTools.Method(typeof(TradeReceiptsUI), "RefreshList");
            yield return AccessTools.Method(typeof(MissionLog), "UpdateTexts");
        }

        /// <summary>Every "day" field that is read only to be written into a text goes through Shown().</summary>
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo shown = AccessTools.Method(typeof(LogBookDays), nameof(Shown));
            var list = new List<CodeInstruction>(instructions);
            for (int i = 0; i < list.Count; i++)
            {
                yield return list[i];
                if (list[i].opcode == OpCodes.Ldfld && list[i].operand is FieldInfo f && f.Name == "day" && f.FieldType == typeof(int) &&
                    i + 1 < list.Count && list[i + 1].opcode == OpCodes.Box)
                {
                    yield return new CodeInstruction(OpCodes.Call, shown);
                }
            }
        }
    }
}
