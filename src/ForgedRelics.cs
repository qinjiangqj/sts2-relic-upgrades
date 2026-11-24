using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace AaaCode.RelicForge;

public sealed class ForgeRelicPool : CustomRelicPoolModel
{
    public override bool IsShared => true;
    public override string EnergyColorName => "colorless";
}

[Pool(typeof(ForgeRelicPool))]
public abstract class ForgedRelic : CustomRelicModel
{
    private const string ArtRoot = "res://aaacode_relic_forge/art";
    public override RelicRarity Rarity => RelicRarity.Event;
    protected abstract string PreviewIcon { get; }
    protected override string IconBaseName => PreviewIcon;
    protected virtual string FallbackPackedIconPath => base.PackedIconPath;
    public override string PackedIconPath => FindArt("small") ?? FallbackPackedIconPath;
    protected override string BigIconPath => FindArt("detail") ?? base.BigIconPath;
    protected override string PackedIconOutlinePath => FindArt("outline") ?? base.PackedIconOutlinePath;
    internal abstract string ChineseName { get; }
    internal abstract string EnglishName { get; }
    internal abstract string ChineseEffect { get; }
    internal abstract string EnglishEffect { get; }
    internal string ForgeName => ForgeText.Pick(ChineseName, EnglishName);
    internal string ForgeEffect => ForgeText.Pick(ChineseEffect, EnglishEffect);
    public override List<(string, string)> Localization => new RelicLoc(ForgeName, ForgeEffect, "");

    private string? FindArt(string size)
    {
        string path = $"{ArtRoot}/{size}/{PreviewIcon}.res";
        if (this is CatalogRelic relic && PreviewIcon is
            "bound_phylactery" or "juzu_bracelet" or "venerable_tea_set" or "blood_vial" or
            "ornamental_fan" or "paper_phrog" or "pantograph" or "akabeko" or "tingsha")
            return RelicOverlayArt.Resolve(size, path, relic.Original);
        return ResourceLoader.Exists(path, "Texture2D") ? path : null;
    }
}

public sealed class DoubleFlukeAnchor : ForgedRelic
{
    protected override string PreviewIcon => "anchor";
    internal override string ChineseName => "双爪锚";
    internal override string EnglishName => "Double-Fluke Anchor";
    internal override string ChineseEffect => "开局获得8点格挡。第2回合开始时，最多保留5点尚未消耗的格挡。";
    internal override string EnglishEffect => "Start combat with 8 Block. Retain up to 5 remaining Block at the start of turn 2.";

    public override async Task BeforeCombatStart()
    {
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, 8m, ValueProp.Unpowered, null);
    }
}

public sealed class EmberLantern : ForgedRelic
{
    private bool _playedAttack;
    private bool _playedSkill;

    protected override string PreviewIcon => "lantern";
    internal override string ChineseName => "余烬灯";
    internal override string EnglishName => "Ember Lantern";
    internal override string ChineseEffect => "第1回合获得1点能量。首回合各打出至少1张攻击牌和1张技能牌，第2回合再获得1点能量。";
    internal override string EnglishEffect => "Gain 1 Energy on turn 1. If you play an Attack and a Skill that turn, gain 1 Energy on turn 2.";

    public override Task BeforeCombatStart()
    {
        _playedAttack = false;
        _playedSkill = false;
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Player == Owner && Owner.PlayerCombatState?.TurnNumber == 1)
        {
            _playedAttack |= cardPlay.Card.Type == CardType.Attack;
            _playedSkill |= cardPlay.Card.Type == CardType.Skill;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner.Creature))
            return;

        int? turn = Owner.PlayerCombatState?.TurnNumber;
        if (turn == 1 || turn == 2 && _playedAttack && _playedSkill)
        {
            Flash();
            await PlayerCmd.GainEnergy(1, Owner);
        }
    }
}

public sealed class ChainPopper : ForgedRelic
{
    private bool _opened;
    private bool _detonated;

    protected override string PreviewIcon => "festive_popper";
    internal override string ChineseName => "连锁拉炮";
    internal override string EnglishName => "Chain Popper";
    internal override string ChineseEffect => "开局对全体敌人造成6点伤害。每场首次击杀非爪牙敌人时，对其他敌人再造成6点伤害。开场击杀也能触发。";
    internal override string EnglishEffect => "Deal 6 damage to all enemies at the start of combat. The first non-minion kill each combat deals 6 to the other enemies. The opening blast can trigger this.";

    public override Task BeforeCombatStart()
    {
        _opened = false;
        _detonated = false;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || _opened || Owner.PlayerCombatState?.TurnNumber != 1)
            return;

        _opened = true;
        await Blast(choiceContext, Owner.Creature.CombatState?.HittableEnemies ?? []);
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature target, CardModel? source, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (_detonated || wasRemovalPrevented || target.Side == Owner.Creature.Side || target.HasPower<MinionPower>())
            return;

        _detonated = true;
        await Blast(choiceContext, (Owner.Creature.CombatState?.HittableEnemies ?? []).Where(enemy => enemy != target));
    }

    private async Task Blast(PlayerChoiceContext context, IEnumerable<Creature> enemies)
    {
        var targets = enemies.Where(enemy => enemy.IsAlive).ToArray();
        if (targets.Length == 0)
            return;
        Flash();
        await CreatureCmd.Damage(context, targets, 6m, ValueProp.Unpowered, Owner.Creature);
    }
}
