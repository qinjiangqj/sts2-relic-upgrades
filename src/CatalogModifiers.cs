using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;

namespace AaaCode.RelicForge;

public abstract partial class CatalogRelic
{
    public override async Task AfterModifyingBlockAmount(decimal amount, CardModel? source, CardPlay? play)
    {
        await Native.AfterModifyingBlockAmount(amount, source, play);
        if (source?.Owner != Owner) return;
        if (MaterialId is "VAMBRACE" or "PAELS_LEGION")
            _turnFlags.Add(amount >= (MaterialId == "VAMBRACE" ? 16 : 20) ? "strong-block" : "small-block");
    }

    public override async Task AfterModifyingCardPlayCount(CardModel card)
    {
        await Native.AfterModifyingCardPlayCount(card);
        if (MaterialId == "THROWING_AXE" && card.Owner == Owner && card.Type == CardType.Attack && CombatOnce("axe-mark"))
            _turnFlags.Add("axe-block");
    }

    public override async Task AfterModifyingPowerAmountReceived(PowerModel power)
    {
        await Native.AfterModifyingPowerAmountReceived(power);
        if (MaterialId == "RUINED_HELMET" && power.Owner == Me && power is StrengthPower && _turnFlags.Remove("helmet-block"))
            await Block(5);
    }

    public override async Task AfterModifyingHpLostAfterOsty()
    {
        await Native.AfterModifyingHpLostAfterOsty();
        if (MaterialId == "THE_BOOT" && TurnOnce("boot-more")) await Block(2);
    }

    public override decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? source)
        => Native.ModifyHpLostAfterOstyLate(target, amount, props, dealer, source);

    public override async Task AfterModifyingOrbPassiveTriggerCount(OrbModel orb)
    {
        await Native.AfterModifyingOrbPassiveTriggerCount(orb);
        if (MaterialId == "GOLD_PLATED_CABLES" && orb is FrostOrb && orb.Owner == Owner && CombatOnce("gold-frost"))
            await Block(4);
    }

    public override async Task AfterDiedToDoom(PlayerChoiceContext context, IReadOnlyList<Creature> creatures)
    {
        int flash = _nativeFlashes;
        await Native.AfterDiedToDoom(context, creatures);
        if (MaterialId == "BOOK_REPAIR_KNIFE" && _nativeFlashes > flash && CombatOnce("doom-spread"))
        {
            var target = Enemies.Where(c => !creatures.Contains(c)).OrderBy(c => c.GetPower<DoomPower>()?.Amount ?? 0).FirstOrDefault();
            if (target != null) await Apply<DoomPower>(target, 4, context);
        }
    }

    public override Task AfterFlush(PlayerChoiceContext context, Player player, IReadOnlyCollection<CardModel> flushed, IReadOnlyCollection<CardModel> retained)
    {
        if (MaterialId != "BOOKMARK" || !IsMe(player)) return Native.AfterFlush(context, player, flushed, retained);
        var candidates = retained.Where(card => !card.EnergyCost.CostsX && card.EnergyCost.GetWithModifiers(CostModifiers.Local) > 0).ToArray();
        var first = candidates.Where(card => !_markedCards.Contains(card)).ToArray();
        var card = Owner.RunState.Rng.CombatCardSelection.NextItem(first.Length > 0 ? first : candidates);
        if (card != null)
        {
            card.EnergyCost.AddUntilPlayed(-1);
            _markedCards.Add(card);
        }
        return Task.CompletedTask;
    }

    public override bool TryModifyPowerAmountReceived(PowerModel power, Creature target, decimal amount, Creature? applier, out decimal modified)
    {
        bool changed = Native.TryModifyPowerAmountReceived(power, target, amount, applier, out modified);
        if (MaterialId == "RUINED_HELMET" && changed && power is StrengthPower && target == Me && amount >= 3)
            _turnFlags.Add("helmet-block");
        return changed;
    }

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (MaterialId != "BRILLIANT_SCARF")
            return Native.TryModifyEnergyCostInCombatLate(card, originalCost, out modifiedCost);
        modifiedCost = originalCost;
        if (card.Owner != Owner || card.Pile?.Type != PileType.Hand || CountTurn("hand-plays") != 4) return false;
        modifiedCost = 0;
        return originalCost > 0;
    }

    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (MaterialId != "BRILLIANT_SCARF")
            return Native.TryModifyStarCost(card, originalCost, out modifiedCost);
        modifiedCost = originalCost;
        if (card.Owner != Owner || card.Pile?.Type != PileType.Hand || CountTurn("hand-plays") != 4) return false;
        modifiedCost = 0;
        return originalCost > 0;
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? source, CardPlay? play)
    {
        decimal result = Native.ModifyDamageAdditive(target, amount, props, dealer, source, play);
        if (MaterialId == "SHURIKEN" && dealer == Me && source?.Type == CardType.Attack && _combatFlags.Contains("shuriken-hit"))
            result += 3;
        return result;
    }

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (MaterialId != "PUMPKIN_CANDLE" || !IsMe(player)) return Native.TryModifyRestSiteOptions(player, options);
        options.Add(new ForgeKindleOption(player, this));
        return true;
    }

    private sealed class ForgeKindleOption(Player owner, CatalogRelic relic) : CustomRestSiteOption(owner)
    {
        public override string OptionId => "KINDLE";

        public override Task<bool> OnSelect()
        {
            ((PumpkinCandle)relic.Native).Rekindle();
            relic.Progress[2] = 1;
            return Task.FromResult(true);
        }
    }
}
