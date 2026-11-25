using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AaaCode.RelicForge;

internal static class ForgeRules
{
    internal const int CampCost = 4;
    internal const int FirstChapter = 1;
    internal const int LastChapter = 2;

    internal static bool IsChapterBoss(RunState state, CombatRoom room)
    {
        int nextAct = state.CurrentActIndex + 1;
        return room.RoomType == RoomType.Boss && nextAct is >= FirstChapter and <= LastChapter &&
            nextAct < state.Acts.Count;
    }

    internal static int CombatDrop(ulong runSeed, int floor, int minimum)
    {
        // Use a separate deterministic roll so the base game's random streams do not move.
        unchecked
        {
            ulong value = runSeed + 0x9E3779B97F4A7C15UL * (ulong)(floor + 1);
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return minimum + (int)((value ^ (value >> 31)) & 1UL);
        }
    }
}
