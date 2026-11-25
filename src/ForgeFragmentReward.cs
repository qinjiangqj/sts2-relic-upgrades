using BaseLib.Abstracts;
using BaseLib.Patches.Content;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace AaaCode.RelicForge;

internal sealed class ForgeFragmentReward(Player player, int amount) : CustomReward(player)
{
    internal const string FragmentTexturePath = "res://aaacode_relic_forge/art/ui/forge_fragments.res";

    [CustomEnum]
    public static RewardType ForgeFragments = default;

    private readonly int _floor = player.RunState.TotalFloor;
    private readonly AbstractRoom? _room = player.RunState.CurrentRoom;

    protected override RewardType RewardType => ForgeFragments;
    public override bool IsPopulated => true;
    public override CreateRewardFromSave<CustomReward> DeserializeMethod => FromSave;

    public override LocString Description
    {
        get
        {
            const string table = "gameplay_ui";
            const string key = "COMBAT_REWARD_AAACODE_FORGE_FRAGMENTS";
            LocManager.Instance.GetTable(table).MergeWith(new()
            {
                [key] = ForgeText.Pick("{Fragments} 个锻造碎片", "{Fragments} Forge Fragments"),
            });
            var description = new LocString(table, key);
            description.Add("Fragments", amount);
            return description;
        }
    }

    public override void Populate() { }
    public override void MarkContentAsSeen() { }

    public override Control CreateIcon()
    {
        var icon = new TextureRect
        {
            Name = "ForgeIcon",
            Texture = ResourceLoader.Load<Texture2D>(FragmentTexturePath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return icon;
    }

    protected override Task<bool> OnSelect() => Task.FromResult(
        Player.RunState is RunState state && state.TotalFloor == _floor &&
        state.CurrentRoom is CombatRoom room && room == _room && ForgeState.ClaimCombatDrop(state, amount));

    public override SerializableReward ToSerializable() => new()
    {
        RewardType = RewardType,
        GoldAmount = amount,
    };

    private static ForgeFragmentReward FromSave(SerializableReward save, Player owner) =>
        new(owner, save.GoldAmount);
}

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.WithRewardsFromRoom))]
internal static class ForgeCombatRewards
{
    private static void Postfix(RewardsSet __instance, AbstractRoom room)
    {
        if (room is not CombatRoom { IsPreFinished: true } combat || !combat.Encounter.ShouldGiveRewards ||
            __instance.Player.RunState is not RunState state || state.CurrentRoom != room ||
            !ForgeService.IsSupported(state) || !ForgeState.CanClaimCombatDrop(state) ||
            __instance.Rewards.OfType<ForgeFragmentReward>().Any())
            return;

        int minimum = combat.RoomType switch
        {
            RoomType.Monster => 0,
            RoomType.Elite => 1,
            RoomType.Boss when ForgeRules.IsChapterBoss(state, combat) => 2,
            _ => -1,
        };
        if (minimum < 0)
            return;

        int amount = ForgeRules.CombatDrop(state.Rng.Seed, state.TotalFloor, minimum);
        if (amount > 0)
            __instance.Rewards.Add(new ForgeFragmentReward(__instance.Player, amount));
    }
}
