// Generated forwarding bindings for native relic hooks.
#nullable enable
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.FoolArrows;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models.Exceptions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.SourceGeneration;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System;
namespace AaaCode.RelicForge;
public abstract partial class CatalogRelic
{
    public override Task AfterCreatureAddedToCombat(Creature creature) => Native.AfterCreatureAddedToCombat(creature);
    public override Task AfterEnergyResetLate(Player player) => Native.AfterEnergyResetLate(player);
    public override Task AfterModifyingPowerAmountGiven(PowerModel power) => Native.AfterModifyingPowerAmountGiven(power);
    public override Task AfterModifyingRewards() => Native.AfterModifyingRewards();
    public override Task AfterPotionDiscarded(PotionModel potion) => Native.AfterPotionDiscarded(potion);
    public override Task AfterPotionProcured(PotionModel potion) => Native.AfterPotionProcured(potion);
    public override Task BeforeCombatRewardOffered(RewardsSet rewards, CombatRoom room) => Native.BeforeCombatRewardOffered(rewards, room);
    public override Task BeforePowerAmountChanged(PowerModel power, decimal amount, Creature target, Creature? applier, CardModel? cardSource) => Native.BeforePowerAmountChanged(power, amount, target, applier, cardSource);
    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay) => Native.ModifyBlockMultiplicative(target, block, props, cardSource, cardPlay);
    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount) => Native.ModifyCardPlayCount(card, target, playCount);
    public override CardCreationOptions ModifyCardRewardCreationOptions(Player player, CardCreationOptions options) => Native.ModifyCardRewardCreationOptions(player, options);
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) => Native.ModifyDamageMultiplicative(target, amount, props, dealer, cardSource, cardPlay);
    public override IReadOnlyList<LocString> ModifyExtraRestSiteHealText(Player player, IReadOnlyList<LocString> currentExtraText) => Native.ModifyExtraRestSiteHealText(player, currentExtraText);
    public override decimal ModifyGoldGained(Player player, decimal amount) => Native.ModifyGoldGained(player, amount);
    public override decimal ModifyMaxEnergy(Player player, decimal amount) => Native.ModifyMaxEnergy(player, amount);
    public override int ModifyOrbPassiveTriggerCounts(OrbModel orb, int triggerCount) => Native.ModifyOrbPassiveTriggerCounts(orb, triggerCount);
    public override decimal ModifyPowerAmountGivenAdditive(PowerModel power, Creature giver, decimal amount, Creature? target, CardModel? cardSource) => Native.ModifyPowerAmountGivenAdditive(power, giver, amount, target, cardSource);
    public override decimal ModifyPowerAmountGivenMultiplicative(PowerModel power, Creature giver, decimal amount, Creature? target, CardModel? cardSource) => Native.ModifyPowerAmountGivenMultiplicative(power, giver, amount, target, cardSource);
    public override decimal ModifyRestSiteHealAmount(Creature creature, decimal amount) => Native.ModifyRestSiteHealAmount(creature, amount);
    public override IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IReadOnlySet<RoomType> roomTypes) => Native.ModifyUnknownMapPointRoomTypes(roomTypes);
    public override bool ShouldClearBlock(Creature creature) => Native.ShouldClearBlock(creature);
    public override bool ShouldForcePotionReward(Player player, RoomType roomType) => Native.ShouldForcePotionReward(player, roomType);
    public override bool ShouldPlayerResetEnergy(Player player) => Native.ShouldPlayerResetEnergy(player);
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost) => Native.TryModifyEnergyCostInCombat(card, originalCost, out modifiedCost);
    public override bool TryModifyRewardsLate(Player player, List<Reward> rewards, AbstractRoom? room) => Native.TryModifyRewardsLate(player, rewards, room);
}
