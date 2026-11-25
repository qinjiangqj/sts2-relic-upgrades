using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;

namespace AaaCode.RelicForge;

public abstract partial class CatalogRelic
{
    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player == Owner && MaterialId == "BRILLIANT_SCARF" && cardPlay.Card.Pile?.Type == PileType.Hand)
        {
            if (IncTurn("hand-plays") == 5 && cardPlay.Card.EnergyCost.GetWithModifiers(CostModifiers.Local) >= 2)
                _turnFlags.Add("scarf-block");
        }
        if (cardPlay.Player == Owner && MaterialId == "PEN_NIB" && cardPlay.Card.Type == CardType.Attack &&
            ((PenNib)Native).AttacksPlayed % 10 == 9)
            _turnFlags.Add("pen-armed");
        if (cardPlay.Player == Owner && MaterialId == "NUNCHAKU" && cardPlay.Card.Type == CardType.Attack)
            _turnFlags.Remove("attack-hit");
        if (cardPlay.Player == Owner && MaterialId == "RAZOR_TOOTH" && cardPlay.Card.IsUpgradable)
            _markedCards.Add(cardPlay.Card);
        if (cardPlay.Player == Owner && MaterialId == "INTIMIDATING_HELMET" &&
            cardPlay.Resources.EnergySpent >= 3 && TurnOnce("heavy-helmet"))
        {
            await Block(7);
            return;
        }
        await Native.BeforeCardPlayed(cardPlay);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Player != Owner)
        {
            await Native.AfterCardPlayed(choiceContext, play);
            return;
        }
        CardModel card = play.Card;
        bool attack = card.Type == CardType.Attack;
        bool skill = card.Type == CardType.Skill;
        bool power = card.Type == CardType.Power;
        IncTurn("plays");
        if (attack)
        {
            int attackCount = IncTurn("attacks");
            IncTurn("consecutive-attacks");
            if (Turn == 1) _combatFlags.Add("attacked-one");
            if (attackCount % 3 == 1)
            {
                _previousAttackTarget = play.Target;
                _sameAttackTarget = play.Target != null;
            }
            else if (_previousAttackTarget != play.Target) _sameAttackTarget = false;
        }
        else _turnTally.Remove("consecutive-attacks");
        if (skill) IncTurn("skills");
        if (power) IncTurn("powers");
        int before = _nativeFlashes;
        int oldHand = HandCount;

        if (MaterialId == "KUSARIGAMA" && attack)
        {
            if (CountTurn("attacks") % 3 == 0)
                await DamageOne(_sameAttackTarget ? 8 : 6, _sameAttackTarget ? play.Target : RandomEnemy, choiceContext);
        }
        else
            await Native.AfterCardPlayed(choiceContext, play);

        bool nativeTriggered = _nativeFlashes > before;
        bool isMarked = _markedCards.Contains(card);
        bool wasPlayed = !_playedMarked.Add(card);
        switch (MaterialId)
        {
            case "PEN_NIB" when attack:
                _turnFlags.Remove("pen-armed");
                if (_turnFlags.Remove("pen-reset"))
                {
                    var nib = (PenNib)Native;
                    AccessTools.PropertySetter(typeof(PenNib), nameof(PenNib.AttacksPlayed))!.Invoke(nib, [nib.AttacksPlayed + 2]);
                }
                break;
            case "VAMBRACE" when _turnFlags.Remove("strong-block"): await Block(4); break;
            case "PAELS_LEGION" when nativeTriggered && _turnFlags.Remove("legion-small-block"): await Block(6); break;
            case "SPIKED_GAUNTLETS" when power && CombatOnce("gauntlet-power"): await Block(5); break;
            case "TORMENTOUS_COLLAR" when attack && Owner.RunState.CurrentRoom?.RoomType == MegaCrit.Sts2.Core.Rooms.RoomType.Elite &&
                 play.Target is { IsAlive: true } terror && terror.HasPower<WeakPower>() && CombatOnce("collar"):
                await Apply<VulnerablePower>(terror, 1, choiceContext); break;
            case "BRILLIANT_SCARF" when _turnFlags.Remove("scarf-block"): await Block(3); break;
            case "UNSETTLING_LAMP" when nativeTriggered && CountTurn("lamp-debuffs") >= 2 && CombatOnce("lamp-splash"):
                await Block(4); break;
            case "THROWING_AXE" when _turnFlags.Contains("axe-strength") && IncTurn("axe-plays") == 2:
                _turnFlags.Remove("axe-strength");
                await Apply<StrengthPower>(Me, 2, choiceContext);
                break;
            case "FENCING_MANUAL" when card.Id.Entry == "SOVEREIGN_BLADE" && CombatOnce("first-sword"):
                await Block(5); break;
            case "BAG_OF_MARBLES" when Turn == 1 && attack && play.Target is { IsAlive: true } vulnerable &&
                 vulnerable.HasPower<VulnerablePower>() && CombatOnce("marble"):
                await Apply<VulnerablePower>(vulnerable, 1, choiceContext); break;
            case "RED_MASK" when Turn == 1 && attack && play.Target is { IsAlive: true } weak &&
                 weak.HasPower<WeakPower>() && CombatOnce("mask"):
                await Apply<VulnerablePower>(weak, 1, choiceContext); break;
            case "ODDLY_SMOOTH_STONE" when _turnFlags.Remove("second-block") && CombatOnce("stone-dex"):
                await Apply<DexterityPower>(Me, 1, choiceContext); break;
            case "PERMAFROST" when power && nativeTriggered && play.Resources.EnergySpent >= 2:
                await Draw(1, choiceContext); break;
            case "LETTER_OPENER" when skill:
                _cardsThisTurn.Add(card.Id.Entry);
                if (CountTurn("skills") % 3 == 0)
                {
                    if (_cardsThisTurn.Count == 3 && TurnOnce("three-letters")) await Draw(1, choiceContext);
                    _cardsThisTurn.Clear();
                }
                break;
            case "ORNAMENTAL_FAN":
                if (skill && _turnFlags.Remove("fan-next-skill")) await Block(3);
                if (attack && nativeTriggered && TurnOnce("fan-trigger")) _turnFlags.Add("fan-next-skill");
                break;
            case "SPARKLING_ROUGE" when skill && Turn is 1 or 2:
                if (Turn == 1) _combatFlags.Add("rouge-first");
                else if (_combatFlags.Contains("rouge-first")) _combatFlags.Add("rouge-second");
                break;
            case "NUNCHAKU" when attack && nativeTriggered && _turnFlags.Remove("attack-hit"):
                await PlayerCmd.GainEnergy(1, Owner); break;
            case "TUNING_FORK" when skill && nativeTriggered && HandCount <= 2:
                await Draw(1, choiceContext); break;
            case "FUNERARY_MASK" when card.Id.Entry == "SOUL" && CombatOnce("first-soul"):
                if (Lowest is { } doomTarget) await Apply<DoomPower>(doomTarget, 3, choiceContext);
                break;
            case "REGALITE": break;
            case "ORANGE_DOUGH" or "OCARINA" or "RADIANT_PEARL" or "TOOLBOX" when isMarked && CombatOnce("generated-play"):
                await Block(MaterialId == "TOOLBOX" ? 3 : 4); break;
            case "BIG_HAT" when isMarked && CombatOnce("generated-play"): await Draw(1, choiceContext); break;
            case "JEWELED_MASK" when isMarked && Turn == 1 && CombatOnce("generated-play"): await Block(5); break;
            case "BIZARRE_LAMP" when isMarked && _markedCards.All(_playedMarked.Contains) && CombatOnce("both-moods"): await Block(5); break;
            case "NINJA_SCROLL" when isMarked && _markedCards.Count >= 3 && _markedCards.All(_playedMarked.Contains) && CombatOnce("three-shivs"):
                await Draw(1, choiceContext); break;
            case "VEXING_PUZZLEBOX" when isMarked && Turn == 1 && card.EnergyCost.GetWithModifiers(CostModifiers.None) >= 2 && CombatOnce("box"):
                await Block(5); break;
            case "BURNING_STICKS" when isMarked && CombatOnce("first-copy"): await Block(4); break;
            case "MUSIC_BOX" when isMarked: await Block(4); break;
            case "MUSIC_BOX" when attack && nativeTriggered: MarkHandTail(Math.Max(0, HandCount - oldHand)); break;
            case "CROSSBOW" when isMarked: _combatFlags.Add("fired-crossbow"); break;
            case "CHOICES_PARADOX" when isMarked && Turn >= 2 && CombatOnce("paradox"): await Block(6); break;
            case "RINGING_TRIANGLE" when Turn == 2 && isMarked && CombatOnce("retained"): await Block(4); break;
            case "STONE_CRACKER" when isMarked && CombatOnce("drilled"): await Block(4); break;
            case "BELLOWS" when isMarked && (attack || skill):
                _combatFlags.Add(attack ? "heated-attack" : "heated-skill");
                if (_combatFlags.Contains("heated-attack") && _combatFlags.Contains("heated-skill") && CombatOnce("bellows"))
                    await Block(4);
                break;
            case "RAINBOW_RING" when nativeTriggered && CombatOnce("first-rainbow"): await Draw(1, choiceContext); break;
            case "POWER_CELL" when isMarked && _markedCards.Count >= 2 && _markedCards.All(_playedMarked.Contains) && CombatOnce("cell"):
                await Draw(1, choiceContext); break;
            case "GAME_PIECE" or "LOST_WISP" when power && CountTurn("powers") == 2 && CombatOnce("second-power"):
                await Block(MaterialId == "GAME_PIECE" ? 5 : 4); break;
            case "KUNAI" when attack && nativeTriggered && CombatOnce("first-kunai"): _combatFlags.Add("kunai-block"); break;
            case "SHURIKEN" when attack && nativeTriggered && CombatOnce("first-shuriken"): _combatFlags.Add("shuriken-hit"); break;
            case "HELICAL_DART" when card.Id.Entry == "SHIV" && IncTurn("shivs") == 3: _turnFlags.Add("dart-block"); break;
            case "RAZOR_TOOTH" when isMarked && wasPlayed && CombatOnce("razor-replay"): await Draw(1, choiceContext); break;
            case "IVORY_TILE" when play.Resources.EnergySpent >= 3 && CombatOnce("first-heavy"): await Block(5); break;
            case "CONDUCTORS_BATON" when nativeTriggered && CombatOnce("conducted"): _combatFlags.Add("conducted-draw"); break;
            case "IRON_CLUB" when nativeTriggered && TurnOnce("club-block"): await Block(4); break;
            case "VITRUVIAN_MINION" when card.Id.Entry.Contains("MINION", StringComparison.Ordinal) && CombatOnce("first-minion"):
                await PlayerCmd.GainStars(1, Owner); break;
            case "DAUGHTER_OF_THE_WIND" when attack && CountTurn("consecutive-attacks") == 3 && TurnOnce("wind-chain"):
                await Block(3); break;
            case "BOOKMARK" when isMarked && CombatOnce("marked-bookmark"): await Draw(1, choiceContext); break;
        }
    }

    public override async Task AfterBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? source)
    {
        if (creature != Me || source?.Owner != Owner || amount <= 0) return;
        if (MaterialId == "ODDLY_SMOOTH_STONE" && IncTurn("blocking-cards") == 2)
            _turnFlags.Add("second-block");
        if (MaterialId == "KUNAI" && _combatFlags.Remove("kunai-block")) await Block(3);
        if (MaterialId == "HELICAL_DART" && _turnFlags.Remove("dart-block")) await Block(3);
    }

    public override async Task AfterCardDiscarded(PlayerChoiceContext context, CardModel card)
    {
        if (card.Owner != Owner)
        {
            await Native.AfterCardDiscarded(context, card);
            return;
        }
        int count = IncTurn("discards");
        if (Turn == 1) _combatFlags.Add("discarded-one");
        if (MaterialId == "TINGSHA" && count == 3)
            await DamageAll(3, context);
        else
            await Native.AfterCardDiscarded(context, card);
        if (MaterialId == "TOUGH_BANDAGES" && card.EnergyCost.GetWithModifiers(CostModifiers.None) >= 2 && TurnOnce("heavy-discard"))
            await Block(2);
    }

    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        int flash = _nativeFlashes;
        await Native.AfterCardGeneratedForCombat(card, creator);
        if (creator == Owner && _nativeFlashes > flash)
        {
            if (MaterialId == "REGALITE" && card.Pool is MegaCrit.Sts2.Core.Models.CardPools.ColorlessCardPool) await Block(4);
            if (MaterialId == "FERTILIZER" && TurnOnce("fertilizer")) await Block(2);
        }
    }

    public override async Task AfterCardExhausted(PlayerChoiceContext context, CardModel card, bool causedByEthereal)
    {
        if (card.Owner == Owner && MaterialId == "FORGOTTEN_SOUL" && IncTurn("exhausted") % 3 == 0)
            await DamageAll(2, context);
        else
        {
            int before = _nativeFlashes;
            int oldHand = HandCount;
            await Native.AfterCardExhausted(context, card, causedByEthereal);
            if (card.Owner != Owner) return;
            switch (MaterialId)
            {
                case "JOSS_PAPER" when _nativeFlashes > before && CombatOnce("joss-guard"): await Block(4); break;
                case "BLESSED_ANTLER" when causedByEthereal && card.Id.Entry == "DAZED" && CombatOnce("antler"):
                    await Block(4); break;
                case "CHARONS_ASHES" when card.DeckVersion != null && TurnOnce("base-exhaust"):
                    await DamageOne(2, Lowest, context); break;
                case "BURNING_STICKS" when HandCount > oldHand: MarkHandTail(HandCount - oldHand); break;
                case "TOASTY_MITTENS" when _turnFlags.Remove("toast-choosing"):
                    await Block(4); break;
            }
        }
    }

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        int before = _nativeFlashes;
        bool duringCombat = _combat;
        await Native.AfterCardChangedPiles(card, oldPileType, clonedBy);
        if (card.Owner != Owner || duringCombat || card.Pile?.Type != PileType.Deck || oldPileType == PileType.Deck)
            return;
        if (MaterialId == "BOOK_OF_FIVE_RINGS")
        {
            Progress[1] |= card.Type switch { CardType.Attack => 1, CardType.Skill => 2, CardType.Power => 4, _ => 0 };
            if (_nativeFlashes > before)
            {
                bool allTypes = Progress[1] == 7;
                Progress[1] = 0;
                if (allTypes) await Heal(5);
            }
        }
    }
}
