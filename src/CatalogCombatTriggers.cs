using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.FoolArrows;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace AaaCode.RelicForge;

public abstract partial class CatalogRelic
{
    public override async Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? card)
    {
        int flash = _nativeFlashes;
        await Native.AfterDamageGiven(context, dealer, result, props, target, card);
        if (_combat && MaterialId == "BOUND_PHYLACTERY" && target.PetOwner == Owner &&
            target.Monster is Osty && result.UnblockedDamage > 0 && CombatOnce("osty-hit"))
        {
            // A lethal hit is reported before the game finishes removing Osty.
            if (result.WasTargetKilled && target.IsDead)
                _combatFlags.Add("osty-lethal-hit");
            else
                await OstyCmd.Summon(context, Owner, 2m, this);
        }
        if (dealer != Me || !target.IsEnemy || card?.Type != CardType.Attack) return;
        if (result.UnblockedDamage > 0) _turnFlags.Add("attack-hit");
        if (MaterialId == "SHURIKEN") _combatFlags.Remove("shuriken-hit");
        if (MaterialId == "MIGHTY_WINGS" && _nativeFlashes > flash && CombatOnce("wings"))
            await Block(4);
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext context, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? card)
    {
        if (target != Me)
        {
            await Native.AfterDamageReceived(context, target, result, props, dealer, card);
            return;
        }
        bool hpLost = result.UnblockedDamage > 0;
        if (MaterialId == "CENTENNIAL_PUZZLE" && hpLost)
        {
            if (CombatOnce("puzzle-hit"))
            {
                if (Me.CombatState?.CurrentSide == CombatSide.Player) await Draw(3, context);
                else _combatFlags.Add("puzzle-draw");
            }
            return;
        }
        await Native.AfterDamageReceived(context, target, result, props, dealer, card);
        if (MaterialId == "BURNING_BLOOD" && hpLost && card?.Owner == Owner)
            _combatFlags.Add("own-card-loss");
        if (dealer?.IsEnemy != true) return;
        if (MaterialId == "BRONZE_SCALES" && result.WasFullyBlocked && result.BlockedDamage > 0 &&
            !props.HasFlag(ValueProp.Unpowered) && CombatOnce("scales")) await Apply<ThornsPower>(Me, 2, context);
        if (MaterialId == "PHILOSOPHERS_STONE" && hpLost && props.IsPoweredAttack() && CombatOnce("stone-hit"))
            await Apply<StrengthPower>(Me, 1, context);
        if (MaterialId == "EMOTION_CHIP" && hpLost) _combatFlags.Add("chip-guard");
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        await Native.AfterCurrentHpChanged(creature, delta);
        if (creature.IsDead || !_combat || creature != Me || delta >= 0) return;
        if (MaterialId == "RED_SKULL" && Low && (Me.CurrentHp - delta) * 2 > Me.MaxHp)
            _combatFlags.Add("skull-low");
    }

    public override async Task AfterBlockBroken(PlayerChoiceContext context, Creature target, Creature? breaker)
    {
        await Native.AfterBlockBroken(context, target, breaker);
        if (breaker != Me || !target.IsEnemy || !target.IsAlive) return;
        switch (MaterialId)
        {
            case "VAJRA" when CombatOnce("vajra-break"): await DamageOne(4, target, context); break;
            case "PAPER_PHROG" when target.HasPower<VulnerablePower>() && CombatOnce("phrog-break"):
                await Apply<VulnerablePower>(target, 1, context); break;
            case "HAND_DRILL" when TurnOnce("drill-break"): await Apply<WeakPower>(target, 1, context); break;
        }
    }

    public override async Task AfterDeath(PlayerChoiceContext context, Creature creature, CardModel? source, bool wasRemovalPrevented, float deathAnimLength)
    {
        await Native.AfterDeath(context, creature, source, wasRemovalPrevented, deathAnimLength);
        if (!_combat) return;
        if (MaterialId == "BOUND_PHYLACTERY" && creature.PetOwner == Owner && creature.Monster is Osty &&
            _combatFlags.Remove("osty-lethal-hit"))
        {
            await OstyCmd.Summon(context, Owner, 2m, this);
            return;
        }
        if (wasRemovalPrevented || !creature.IsEnemy || creature.HasPower<MinionPower>()) return;
        switch (MaterialId)
        {
            case "GREMLIN_HORN": await Block(4); break;
            case "STRIKE_DUMMY" when source?.Owner == Owner && source.Id.Entry.Contains("STRIKE", StringComparison.Ordinal)
                 && CombatOnce("strike-kill"): await Draw(1, context); break;
            case "MINIATURE_CANNON" when source?.Owner == Owner && source.Type == CardType.Attack
                 && source.IsUpgraded && CombatOnce("cannon-kill"): await PlayerCmd.GainEnergy(1, Owner); break;
            case "MYSTIC_LIGHTER" when source?.Owner == Owner && source.Type == CardType.Attack
                 && source.Enchantment != null && CombatOnce("lighter-kill"): await Draw(1, context); break;
            case "PEN_NIB" when source?.Owner == Owner && _turnFlags.Contains("pen-armed") && CombatOnce("pen-kill"):
                _turnFlags.Add("pen-reset"); break;
            case "CROSSBOW" when source is not null && _markedCards.Contains(source) && CombatOnce("crossbow-kill"):
                await PlayerCmd.GainEnergy(1, Owner);
                await Draw(1, context);
                break;
        }
    }

    public override async Task AfterAttack(PlayerChoiceContext context, AttackCommand command)
    {
        int flash = _nativeFlashes;
        await Native.AfterAttack(context, command);
        if (MaterialId == "BONE_FLUTE" && _nativeFlashes > flash && TurnOnce("flute") &&
            Me.Pets.Any(pet => pet.IsAlive && pet.CurrentHp * 2 <= pet.MaxHp))
            await OstyCmd.Summon(context, Owner, 1m, this);
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power, decimal amount, Creature? applier, CardModel? card)
    {
        if (!_combat || amount <= 0) return;
        if (MaterialId == "EXERCISE_MAT" && power.Owner == Me && Me.HasPower<AimPower>() &&
            Me.HasPower<StretchPower>() && CombatOnce("mat-mixed"))
            await Apply<StretchPower>(Me, 2, context);
        if (applier != Me || !power.Owner.IsEnemy) return;
        switch (MaterialId)
        {
            case "TWISTED_FUNNEL" when power is PoisonPower && power.Owner.IsAlive && power.Amount >= 8 &&
                 power.Amount - amount < 8 && CombatOnce("funnel-overflow"):
                Flash();
                foreach (Creature enemy in Enemies.ToArray())
                    await Apply<PoisonPower>(enemy, 2, context);
                break;
            case "SNECKO_SKULL" when power is PoisonPower && power.Amount >= 10 && power.Amount - amount < 10 && CombatOnce("venom-ten"):
                await Apply<WeakPower>(power.Owner, 1, context); break;
            case "PAPER_KRANE" when power is WeakPower && power.Amount - amount > 0 && CombatOnce("double-weak"):
                await Block(4); break;
            case "UNDYING_SIGIL" when power is DoomPower && power.Amount >= power.Owner.CurrentHp &&
                 power.Amount - amount < power.Owner.CurrentHp && CombatOnce("first-doom"):
                await Block(6); break;
            case "UNSETTLING_LAMP" when card is not null &&
                 power.Type == MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff:
                if (_tally.GetValueOrDefault("lamp-card") != card.GetHashCode())
                {
                    _tally["lamp-card"] = card.GetHashCode();
                    _turnTally["lamp-debuffs"] = 0;
                }
                IncTurn("lamp-debuffs"); break;
        }
    }

    public override async Task AfterOrbChanneled(PlayerChoiceContext context, Player player, OrbModel orb)
    {
        await Native.AfterOrbChanneled(context, player, orb);
        if (player != Owner) return;
        if (MaterialId == "DATA_DISK" && DistinctOrbs() >= 2 && CombatOnce("disk-mix")) await Block(4);
        if (MaterialId == "METRONOME" && Inc("orbs") == 4) await Block(5);
        if (MaterialId == "RUNIC_CAPACITOR" && Owner.PlayerCombatState?.OrbQueue is { } queue &&
            queue.Orbs.Count >= queue.Capacity && CombatOnce("charge-full")) await Block(7);
    }

    public override async Task AfterOrbEvoked(PlayerChoiceContext context, OrbModel orb, IEnumerable<Creature> targets)
    {
        if (orb.Owner != Owner) return;
        if (MaterialId == "SYMBIOTIC_VIRUS" && orb is DarkOrb && CombatOnce("dark")) await Block(6);
        if (MaterialId == "CRACKED_CORE" && orb is LightningOrb && CombatOnce("lightning") &&
            Owner.PlayerCombatState?.OrbQueue is { } queue && queue.Orbs.Count < queue.Capacity)
            await OrbCmd.Channel<FrostOrb>(context, Owner);
    }

    public override async Task AfterStarsSpent(int amount, Player player)
    {
        int flash = _nativeFlashes;
        await Native.AfterStarsSpent(amount, player);
        if (!IsMe(player)) return;
        if (MaterialId == "DIVINE_RIGHT" && amount >= 2 && CombatOnce("royal-rebate")) await PlayerCmd.GainStars(1, Owner);
        if (MaterialId == "GALACTIC_DUST" && _nativeFlashes > flash && CombatOnce("dust-rebate")) await PlayerCmd.GainStars(1, Owner);
        if (MaterialId == "MINI_REGENT" && amount >= 3 && _nativeFlashes > flash) await Block(4);
    }

    public override async Task AfterArrowDislodged(ImpaledPower power, ArrowSlot arrow, PlayerChoiceContext context, CardModel? source)
    {
        int flash = _nativeFlashes;
        await Native.AfterArrowDislodged(power, arrow, context, source);
        if (source?.Owner != Owner && _nativeFlashes == flash) return;
        if (MaterialId == "SONG_BOW" && CombatOnce("first-arrow")) await Apply<AimPower>(Me, 3, context);
        if (MaterialId == "TWINE" && IncTurn("loose-arrows") == 2) await Draw(1, context);
    }

    public override async Task AfterShuffle(PlayerChoiceContext context, Player player)
    {
        bool hadAim = (Me.GetPower<AimPower>()?.Amount ?? 0) >= 5;
        await Native.AfterShuffle(context, player);
        if (!IsMe(player)) return;
        if (MaterialId == "WHITTLING_KNIFE" && hadAim && TurnOnce("knife")) await Block(3);
        if (MaterialId == "THE_ABACUS" && CombatOnce("abacus-draw")) await Draw(1, context);
    }

    public override async Task AfterPotionUsed(PotionModel potion, Creature? target)
    {
        await Native.AfterPotionUsed(potion, target);
        if (potion.Owner != Owner) return;
        if (MaterialId == "REPTILE_TRINKET" && TurnOnce("first-potion")) await Block(4);
        if (MaterialId == "BELT_BUCKLE" && !Owner.Potions.Any() && CombatOnce("last-potion")) await Block(4);
    }
}
