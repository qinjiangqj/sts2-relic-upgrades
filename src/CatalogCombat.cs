using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.FoolArrows;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace AaaCode.RelicForge;

public abstract partial class CatalogRelic
{
    private Creature Me => Owner.Creature;
    private IReadOnlyList<Creature> Enemies => Me.CombatState?.HittableEnemies ?? [];
    private bool IsMe(Player player) => player == Owner;
    private bool OurSide(CombatSide side, IEnumerable<Creature> participants) => side == CombatSide.Player && participants.Contains(Me);
    private int HandCount => Owner.PlayerCombatState is null ? 0 : PileType.Hand.GetPile(Owner).Cards.Count;
    private int Energy => Owner.PlayerCombatState?.Energy ?? 0;
    private bool Low => Me.CurrentHp * 2 <= Me.MaxHp;
    private Task Block(int quantity) => quantity > 0 ? CreatureCmd.GainBlock(Me, quantity, ValueProp.Unpowered, null) : Task.CompletedTask;
    private Task Heal(int quantity) => CreatureCmd.Heal(Me, quantity);
    private Task Draw(int count, PlayerChoiceContext? context = null) => CardPileCmd.Draw(context ?? new ThrowingPlayerChoiceContext(), count, Owner);
    private Task Apply<T>(Creature creature, int count, PlayerChoiceContext? context = null) where T : PowerModel =>
        PowerCmd.Apply<T>(context ?? new ThrowingPlayerChoiceContext(), creature, count, Me, null);
    private Task Damage(int amount, PlayerChoiceContext? context = null, params Creature[] targets) =>
        targets.Length == 0 ? Task.CompletedTask : CreatureCmd.Damage(context ?? new ThrowingPlayerChoiceContext(),
            targets, amount, ValueProp.Unpowered, Me);
    private Task DamageAll(int amount, PlayerChoiceContext? context = null) => Damage(amount, context, Enemies.ToArray());
    private Creature? Lowest => Enemies.OrderBy(enemy => enemy.CurrentHp).FirstOrDefault();
    private Creature? RandomEnemy => Enemies.Count == 0 ? null : Owner.RunState.Rng.CombatTargets.NextItem(Enemies);
    private Task DamageOne(int amount, Creature? target, PlayerChoiceContext? context = null) =>
        target == null ? Task.CompletedTask : Damage(amount, context, target);

    public override async Task BeforeCombatStart()
    {
        if (!_combat) StartCombatTracking();
        if (MaterialId == "PUMPKIN_CANDLE" && Progress[2] > 0)
        {
            Progress[2] = 0;
            await Block(5);
        }
        if (MaterialId == "REGAL_PILLOW" && Progress[2] > 0 ||
            MaterialId == "STONE_HUMIDIFIER" && Progress[2] > 0)
        {
            Progress[2] = 0;
            await Block(MaterialId == "REGAL_PILLOW" ? 5 : 4);
        }
        if (MaterialId == "PANTOGRAPH" && Low) _combatFlags.Add("boss-low");
        await Native.BeforeCombatStart();
    }

    public override async Task BeforeCombatStartLate()
    {
        int potions = Owner.Potions.Count();
        await Native.BeforeCombatStartLate();
        if (MaterialId == "PETRIFIED_TOAD" && Owner.Potions.Count() == potions)
            await Block(5);
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (OurSide(side, participants)) ResetTurnTracking();
        int hand = HandCount;
        await Native.BeforeSideTurnStart(choiceContext, side, participants, combatState);
        if (!OurSide(side, participants)) return;
        if (MaterialId == "POWER_CELL" && Turn == 1) MarkHandTail(HandCount - hand);
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        int flash = _nativeFlashes;
        int hand = HandCount;
        if (OurSide(side, participants) && MaterialId == "CHANDELIER" && Turn is 2 or 3)
        {
            await PlayerCmd.GainEnergy(Turn == 2 ? 1 : _combatFlags.Contains("spent-two") ? 3 : 2, Owner);
            Flash();
            return;
        }

        if (OurSide(side, participants) && MaterialId == "CANDELABRA" && Turn == 2)
            await PlayerCmd.GainEnergy(1, Owner);
        else if (OurSide(side, participants) && MaterialId == "DIAMOND_DIADEM" && Turn == 1)
        {
            await Block(18);
            await Apply<BlurPower>(Me, 1);
        }
        else
            await Native.AfterSideTurnStart(side, participants, combatState);
        if (!OurSide(side, participants)) return;
        switch (MaterialId)
        {
            case "SAI" when Turn >= 2 && _combatFlags.Remove("sai-no-attack"): await Block(2); break;
            case "DIAMOND_DIADEM" when Turn == 2 && Me.Block == 0: await Block(5); break;
            case "HAPPY_FLOWER" when _nativeFlashes > flash && CombatOnce("flower"): await Block(3); break;
            case "CANDELABRA" when Turn == 2 && _combatFlags.Contains("spent-one"): await Draw(1); break;
            case "SEAL_OF_GOLD" when _nativeFlashes > flash && CombatOnce("gold-seal"): await Block(4); break;
            case "PAELS_TEARS" when _nativeFlashes > flash && CombatOnce("tear-guard"): await Block(4); break;
            case "PANTOGRAPH" when Turn == 1 && _combatFlags.Contains("boss-low") &&
                 Owner.RunState.CurrentRoom?.RoomType == RoomType.Boss: await Block(6); break;
            case "SLING_OF_COURAGE" when Turn == 1 && Low && Owner.RunState.CurrentRoom?.RoomType == RoomType.Elite: await Block(6); break;
            case "BOOMING_CONCH" when Turn == 2 && Owner.RunState.CurrentRoom?.RoomType == RoomType.Elite && Enemies.Count >= 2: await Block(5); break;
            case "BREAD" or "VERY_HOT_COCOA" when Turn == 2 && (MaterialId == "BREAD" || _combatFlags.Contains("spent-one")):
                await Draw(1); break;
            case "PAELS_FLESH" when Turn == 3 && _combatFlags.Contains("spent-one") && _combatFlags.Contains("spent-two"):
                await Draw(1); break;
            case "VENERABLE_TEA_SET" when Turn == 2 && _combatFlags.Contains("tea"): await Block(4); break;
            case "ART_OF_WAR" when _combatFlags.Remove("art-block"): await Block(3); break;
            case "POCKETWATCH" when _combatFlags.Remove("watch-block"): await Block(3); break;
            case "AKABEKO" when Turn == 2 && !_combatFlags.Contains("attacked-one"): await Apply<VigorPower>(Me, 3); break;
            case "EMOTION_CHIP" when _combatFlags.Remove("chip-guard"):
                if (DistinctOrbs() >= 2 && CombatOnce("chip")) await Block(3);
                break;
        }
        if (MaterialId is "ORANGE_DOUGH" or "BIG_HAT" or "CROSSBOW") MarkHandTail(HandCount - hand);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (IsMe(player) && MaterialId == "STONE_CALENDAR")
        {
            if (Turn == 7) await DamageAll(10, choiceContext);
            return;
        }
        int flash = _nativeFlashes;
        if (IsMe(player) && MaterialId == "TOASTY_MITTENS") _turnFlags.Add("toast-choosing");
        await Native.AfterPlayerTurnStart(choiceContext, player);
        if (!IsMe(player)) return;
        _turnFlags.Remove("toast-choosing");

        switch (MaterialId)
        {
            case "BAG_OF_PREPARATION" when Turn == 2 && _combatFlags.Contains("empty-one"): await Draw(1, choiceContext); break;
            case "RING_OF_THE_SNAKE" when Turn == 2 && _combatFlags.Contains("discarded-one"): await Draw(1, choiceContext); break;
            case "MERCURY_HOURGLASS" when Turn == 3: await DamageAll(3, choiceContext); break;
            case "STURDY_CLAMP" when Turn == 2 && _combatFlags.Contains("clamp-ten"): await Block(4); break;
            case "VEXING_PUZZLEBOX" or "CHOICES_PARADOX" when Turn == 1: MarkHandTail(1); break;
            case "BELLOWS" when Turn == 1:
                foreach (CardModel card in PileType.Hand.GetPile(Owner).Cards) _markedCards.Add(card);
                break;
            case "GAMBLING_CHIP" when Turn == 1 && CountTurn("discards") >= 3 && _nativeFlashes > flash:
                await Draw(1, choiceContext); break;
            case "MR_STRUGGLES" when Turn == 3: await Block(4); break;
            case "ICE_CREAM" when _combatFlags.Remove("ice-draw"): await Draw(1, choiceContext); break;
            case "PAELS_BLOOD" when _combatFlags.Remove("blood-draw"): await Draw(1, choiceContext); break;
            case "CENTENNIAL_PUZZLE" when _combatFlags.Remove("puzzle-draw"): await Draw(3, choiceContext); await Block(3); break;
            case "CONDUCTORS_BATON" when _combatFlags.Remove("conducted-draw"): await Draw(1, choiceContext); break;
        }
    }

    public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
    {
        bool bathGuard = IsMe(player) && Turn == 1 && Me.CurrentHp * 4 <= Me.MaxHp;
        await Native.AfterPlayerTurnStartLate(choiceContext, player);
        if (MaterialId == "BATH_TOWEL" && bathGuard) await Block(4);
    }

    public override async Task BeforeSideTurnEndVeryEarly(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        await Native.BeforeSideTurnEndVeryEarly(choiceContext, side, participants);
        if (OurSide(side, participants) && MaterialId == "ORICHALCUM")
        {
            if (CountTurn("skills") == 0) _turnFlags.Add("ori-without-skill");
        }
    }

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!OurSide(side, participants))
        {
            await Native.BeforeSideTurnEnd(choiceContext, side, participants);
            return;
        }

        int flash = _nativeFlashes;
        int hand = HandCount;
        if (MaterialId == "RIPPLE_BASIN" && CountTurn("attacks") == 0)
        {
            await Block(CountTurn("skills") >= 2 ? 6 : 4);
        }
        else if (MaterialId == "STONE_CALENDAR")
        {
            if (Turn == 6) await DamageAll(42, choiceContext);
        }
        else if (MaterialId != "PAELS_TEARS" || Energy == 1)
            await Native.BeforeSideTurnEnd(choiceContext, side, participants);

        if (Energy == 0 && Turn is 1 or 2) _combatFlags.Add(Turn == 1 ? "spent-one" : "spent-two");
        if (hand == 0 && Turn == 1) _combatFlags.Add("empty-one");

        switch (MaterialId)
        {
            case "GORGET" when Turn == 1 && (Me.GetPower<PlatingPower>()?.Amount ?? 0) >= 4:
                await Apply<PlatingPower>(Me, 2, choiceContext); break;
            case "ORICHALCUM" when _nativeFlashes > flash && _turnFlags.Contains("ori-without-skill"): await Block(2); break;
            case "CLOAK_CLASP" when hand >= 3: await Block(2); break;
            case "SCREAMING_FLAGON" when hand == 0 && CountTurn("plays") >= 5 && CombatOnce("empty-flagon"): await Block(5); break;
            case "OBSIDIAN_VICE" when hand >= 4: await DamageOne(1, RandomEnemy, choiceContext); break;
            case "ICE_CREAM" when Energy >= 2 && CombatOnce("ice-unused"): _combatFlags.Add("ice-draw"); break;
            case "PAELS_BLOOD" when Turn >= 3 && hand == 0 && CombatOnce("blood-empty"): _combatFlags.Add("blood-draw"); break;
            case "POCKETWATCH" when CountTurn("plays") <= 1: _combatFlags.Add("watch-block"); break;
            case "ART_OF_WAR" when CountTurn("attacks") == 0 && CountTurn("skills") >= 2: _combatFlags.Add("art-block"); break;
            case "SAI" when CountTurn("attacks") == 0: _combatFlags.Add("sai-no-attack"); break;
            case "SPARKLING_ROUGE" when Turn == 2 && _combatFlags.Contains("rouge-second"):
                _combatFlags.Add("rouge-early");
                await Apply<StrengthPower>(Me, 1, choiceContext);
                await Apply<DexterityPower>(Me, 1, choiceContext);
                break;
            case "RINGING_TRIANGLE" when Turn == 1:
                MarkHandTail(HandCount);
                break;
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        bool ours = OurSide(side, participants);
        bool noStars = Owner.PlayerCombatState?.Stars == 0;
        if (ours && MaterialId == "PARRYING_SHIELD" && Me.Block >= 20)
            await DamageOne(10, RandomEnemy, choiceContext);
        else
            await Native.AfterSideTurnEnd(choiceContext, side, participants);
        if (ours && MaterialId == "LUNAR_PASTRY" && noStars && CombatOnce("zero-stars"))
            await PlayerCmd.GainStars(1, Owner);
    }

    public override async Task AfterEnergyReset(Player player)
    {
        int flash = _nativeFlashes;
        await Native.AfterEnergyReset(player);
        if (IsMe(player) && MaterialId == "VENERABLE_TEA_SET" && _nativeFlashes > flash)
            _combatFlags.Add("tea");
    }

    public override async Task AfterBlockCleared(Creature creature)
    {
        if (creature == Me && MaterialId == "CAPTAINS_WHEEL")
        {
            if (Turn == 3) await Block(15);
            return;
        }
        if (creature == Me && MaterialId == "SPARKLING_ROUGE" && Turn == 3 && _combatFlags.Contains("rouge-early"))
            return;
        await Native.AfterBlockCleared(creature);
    }

    public override async Task AfterPreventingBlockClear(AbstractModel preventer, Creature creature)
    {
        await Native.AfterPreventingBlockClear(preventer, creature);
        if (creature == Me && MaterialId == "STURDY_CLAMP" && Turn == 2 && Me.Block == 10)
            _combatFlags.Add("clamp-ten");
    }

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        int oldHand = HandCount;
        await Native.BeforeHandDraw(player, choiceContext, combatState);
        if (!IsMe(player) || Turn != 1) return;
        if (MaterialId is "TOOLBOX" or "NINJA_SCROLL" or "OCARINA" or "RADIANT_PEARL" or "BIZARRE_LAMP" or "JEWELED_MASK")
            MarkHandTail(Math.Max(0, HandCount - oldHand));
    }

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        decimal result = Native.ModifyHandDraw(player, count);
        if (IsMe(player) && MaterialId == "PENDULUM" && result > count && HandCount <= 2)
            return result + 1;
        return result;
    }

    public override async Task AfterModifyingHandDraw()
    {
        await Native.AfterModifyingHandDraw();
        if (MaterialId == "POLLINOUS_CORE" && Me.Block == 0 && CombatOnce("flower-block"))
            await Block(4);
    }

    private void MarkHandTail(int count)
    {
        if (Owner.PlayerCombatState is null || count <= 0) return;
        foreach (CardModel card in PileType.Hand.GetPile(Owner).Cards.TakeLast(count))
            _markedCards.Add(card);
    }

    private int DistinctOrbs() => Owner.PlayerCombatState?.OrbQueue.Orbs.Select(orb => orb.GetType()).Distinct().Count() ?? 0;
}
