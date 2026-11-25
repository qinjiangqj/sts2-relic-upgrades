using BaseLib.Utils;
using MegaCrit.Sts2.Core.Runs;

namespace AaaCode.RelicForge;

internal static class ForgeState
{
    private static readonly SavedSpireField<RunState, int> FragmentsField = new(() => 0, "AAcodeRelicForgeFragments");
    private static readonly SavedSpireField<RunState, int> PaidFloorField = new(() => -1, "AAcodeRelicForgePaidFloor");
    private static readonly SavedSpireField<RunState, int> OfferField = new(() => 0, "AAcodeRelicForgeChapterOffer");
    // Touch these fields during mod initialization, before any run is loaded.
    internal static void Register() => _ = FragmentsField;

    internal static int Fragments(RunState state) => FragmentsField.Get(state);
    // The existing paid-floor field also covers drops already credited by older versions.
    internal static bool CanClaimCombatDrop(RunState state) =>
        state.TotalFloor > 0 && PaidFloorField.Get(state) < state.TotalFloor;

    internal static bool ChapterOfferIsOpen(RunState state) => OfferField.Get(state) != 0 &&
        OfferField.Get(state) == state.CurrentActIndex;
    internal static void OpenChapterOffer(RunState state) => OfferField.Set(state, state.CurrentActIndex + 1);
    internal static void SettleChapterOffer(RunState state) => OfferField.Set(state, 0);

    internal static bool ClaimCombatDrop(RunState state, int amount)
    {
        if (amount <= 0 || !CanClaimCombatDrop(state))
            return false;

        FragmentsField.Set(state, Fragments(state) + amount);
        PaidFloorField.Set(state, state.TotalFloor);
        return true;
    }

    internal static void Pay(RunState state, int cost)
    {
        if (cost > Fragments(state))
            throw new InvalidOperationException("Insufficient forge fragments.");
        FragmentsField.Set(state, Fragments(state) - cost);
    }
}
