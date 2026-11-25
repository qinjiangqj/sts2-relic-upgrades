using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace AaaCode.RelicForge;

[ModInitializer(nameof(Initialize))]
public static class ForgeMod
{
    public static void Initialize()
    {
        ForgeState.Register();
        CatalogRelic.Register();
        new Harmony("AAcode.STS2.RelicForge").PatchAll(typeof(ForgeMod).Assembly);
        Log.Info($"[RelicForge] {typeof(ForgeMod).Assembly.GetName().Version} initialized. 171 upgrade recipes registered for single-player standard runs.");
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatVictory))]
internal static class ForgeVictoryPatch
{
    private static void Postfix(IRunState runState, CombatRoom room, ref Task __result)
    {
        if (runState is RunState state && ForgeService.IsSupported(state) && ForgeRules.IsChapterBoss(state, room))
            __result = OpenChapterOfferAfterVictory(__result, state);
    }

    private static async Task OpenChapterOfferAfterVictory(Task original, RunState state)
    {
        await original;
        ForgeState.OpenChapterOffer(state);
    }
}

[HarmonyPatch(typeof(RestSiteOption), nameof(RestSiteOption.Generate))]
internal static class ForgeRestSitePatch
{
    private static void Postfix(Player player, List<RestSiteOption> __result)
    {
        if (player.RunState is RunState state && ForgeService.IsSupported(state))
        {
            ForgeText.AddRestSiteLabels();
            __result.Add(new ForgeRestSiteOption(player));
        }
    }
}

[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.Proceed))]
internal static class ForgeChapterPatch
{
    private static bool _prompting;

    private static bool Prefix(ref Task __result)
    {
        if (RunManager.Instance.DebugOnlyGetState() is not RunState state ||
            state.CurrentRoom is not EventRoom { CanonicalEvent: AncientEventModel } eventRoom ||
            !ForgeService.IsSupported(state) || !ForgeState.ChapterOfferIsOpen(state) ||
            LocalContext.GetMe(state) is not { } player)
            return true;

        __result = _prompting ? Task.CompletedTask : OfferThenProceed(state, eventRoom, player);
        return false;
    }

    private static async Task OfferThenProceed(RunState state, EventRoom eventRoom, Player player)
    {
        _prompting = true;
        try
        {
            ForgeService.Choice? choice = await ForgeInterface.PickRecipe(player, state, chapter: true);
            if (!ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), state) || state.CurrentRoom != eventRoom)
                return;

            if (choice.HasValue)
                await ForgeService.TryCraft(player, choice.Value, chapter: true);
            ForgeState.SettleChapterOffer(state);
            await SaveManager.Instance.SaveRun(eventRoom, saveProgress: false);
            RunManager.Instance.Proceed();
        }
        finally
        {
            _prompting = false;
        }
    }
}

// Preserve a bounded part of Block after the game's normal turn-start handling.
[HarmonyPatch(typeof(Creature), nameof(Creature.AfterTurnStart))]
internal static class AnchorRetentionPatch
{
    private static void Prefix(Creature __instance, CombatSide side, out int __state)
    {
        __state = 0;
        if (side != CombatSide.Player || __instance.Player is not { PlayerCombatState: { } combat } player)
            return;
        foreach (var relic in player.Relics.Where(relic => !relic.IsMelted))
        {
            int cap = (relic, combat.TurnNumber) switch
            {
                (DoubleFlukeAnchor, 2) => 5,
                (ForgedHornCleat, 3) => 4,
                (ForgedCaptainsWheel, 4) => 6,
                _ => 0,
            };
            __state = Math.Max(__state, Math.Min(cap, __instance.Block));
        }
    }

    private static void Postfix(Creature __instance, int __state, ref Task __result)
    {
        if (__state > 0)
            __result = RestoreBlock(__result, __instance, __state);
    }

    private static async Task RestoreBlock(Task original, Creature creature, int minimum)
    {
        await original;
        int missing = minimum - creature.Block;
        if (missing > 0)
            await CreatureCmd.GainBlock(creature, missing, ValueProp.Unpowered, null);
    }
}
