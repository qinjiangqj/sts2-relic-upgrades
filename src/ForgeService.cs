using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AaaCode.RelicForge;

internal static class ForgeService
{
    private static bool _crafting;

    internal readonly record struct Choice(RelicModel Source, ForgedRelic Result);

    internal static bool IsSupported(RunState state) =>
        state.Players.Count == 1 && state.GameMode == GameMode.Standard &&
        RunManager.Instance.IsSingleplayerOrFakeMultiplayer;

    internal static List<Choice> GetChoices(Player player)
    {
        var choices = new List<Choice>(3);
        foreach (RelicModel relic in player.Relics)
        {
            if (relic.IsMelted || relic.IsUsedUp)
                continue;

            ForgedRelic? result = ForgeCatalog.Result(relic.Id.Entry);
            if (result != null && !player.Relics.Any(existing => existing.Id == result.Id))
                choices.Add(new Choice(relic, result));
        }
        return choices;
    }

    internal static async Task<bool> TryCraft(Player player, Choice choice, bool chapter)
    {
        if (_crafting || player.RunState is not RunState state || !IsSupported(state))
            return false;
        int cost = chapter ? 0 : ForgeRules.CampCost;
        if (chapter && (!ForgeState.ChapterOfferIsOpen(state) ||
            state.CurrentRoom is not EventRoom { CanonicalEvent: AncientEventModel }) ||
            !chapter && state.CurrentRoom is not RestSiteRoom ||
            ForgeState.Fragments(state) < cost ||
            !GetChoices(player).Any(candidate => ReferenceEquals(candidate.Source, choice.Source) && candidate.Result.Id == choice.Result.Id))
            return false;

        _crafting = true;
        try
        {
            int index = Array.IndexOf(player.Relics.ToArray(), choice.Source);
            RelicModel replacement = choice.Result.ToMutable();
            if (replacement is CatalogRelic catalogue)
                catalogue.Inherit(choice.Source);
            try
            {
                await RelicCmd.Replace(choice.Source, replacement);
            }
            catch (Exception error)
            {
                Log.Error($"[RelicForge] Replacement failed: {error}");
                if (!player.Relics.Contains(replacement))
                {
                    if (!player.Relics.Contains(choice.Source))
                        await RelicCmd.Obtain(choice.Source, player, index);
                    return false;
                }
                // The item was added. Commit the price even if an animation or notification failed.
            }

            ForgeState.Pay(state, cost);
            if (chapter)
                ForgeState.SettleChapterOffer(state);
            return true;
        }
        finally
        {
            _crafting = false;
        }
    }
}
