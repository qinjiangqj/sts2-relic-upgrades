using BaseLib.Utils;
using MegaCrit.Sts2.Core.Runs;

namespace AaaCode.RelicForge;

internal static class ForgeState
{
    private static readonly SavedSpireField<RunState, int> FragmentsField = new(() => 0, "AAcodeRelicForgeFragments");
    private static readonly SavedSpireField<RunState, int> PaidFloorField = new(() => -1, "AAcodeRelicForgePaidFloor");
    private static readonly SavedSpireField<RunState, int> OfferField = new(() => 0, "AAcodeRelicForgeChapterOffer");
    private static readonly SavedSpireField<RunState, string> ReceiptEncounterField = new(() => "", "AAcodeRelicForgeReceiptEncounter");
    private static readonly SavedSpireField<RunState, int> ReceiptAmountField = new(() => 0, "AAcodeRelicForgeReceiptAmount");

    // Touch these fields during mod initialization, before any run is loaded.
    internal static void Register() => _ = FragmentsField;

    internal static int Fragments(RunState state) => FragmentsField.Get(state);
    internal static int LastPaidFloor(RunState state) => PaidFloorField.Get(state);
    internal static bool ChapterOfferIsOpen(RunState state) => OfferField.Get(state) != 0 &&
        OfferField.Get(state) == state.CurrentActIndex;
    internal static void SettleChapterOffer(RunState state) => OfferField.Set(state, 0);

    internal static void Award(RunState state, int amount, bool openedChapterOffer)
    {
        FragmentsField.Set(state, Fragments(state) + amount);
        PaidFloorField.Set(state, state.TotalFloor);
        if (openedChapterOffer)
            OfferField.Set(state, state.CurrentActIndex + 1);
    }

    internal static void RecordCombatDrop(RunState state, string encounterId, int amount)
    {
        ReceiptEncounterField.Set(state, encounterId);
        ReceiptAmountField.Set(state, amount);
    }

    internal static int? CombatDropAtCurrentFloor(RunState state, string encounterId) =>
        LastPaidFloor(state) == state.TotalFloor && ReceiptEncounterField.Get(state) == encounterId
            ? ReceiptAmountField.Get(state) : null;

    internal static void Pay(RunState state, int cost)
    {
        if (cost > Fragments(state))
            throw new InvalidOperationException("Insufficient forge fragments.");
        FragmentsField.Set(state, Fragments(state) - cost);
    }
}
