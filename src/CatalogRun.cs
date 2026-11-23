using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AaaCode.RelicForge;

public abstract partial class CatalogRelic
{
    private static readonly ConditionalWeakTable<CardReward, HashSet<CatalogRelic>> RewardSources = new();
    private static readonly ConditionalWeakTable<PotionReward, CatalogRelic> PotionChoiceSources = new();
    private int _sacrificed;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom && !_combat)
            StartCombatTracking();
        if (MaterialId == "MEAL_TICKET") Progress[3] = room is MerchantRoom && Low ? 1 : 0;
        bool enteringLow = Low;
        var unupgraded = MaterialId == "STONE_CRACKER" && room is CombatRoom
            ? PileType.Draw.GetPile(Owner).Cards.Where(card => card.IsUpgradable).ToArray() : [];

        await Native.AfterRoomEntered(room);

        if (MaterialId == "STONE_CRACKER")
        {
            foreach (CardModel card in unupgraded.Where(card => card.IsUpgraded))
                _markedCards.Add(card);
        }
        if (MaterialId == "ETERNAL_FEATHER" && room is RestSiteRoom && enteringLow)
            await Heal(3);
        if (MaterialId == "JUZU_BRACELET" && Owner.RunState.CurrentMapPoint?.PointType == MapPointType.Unknown && FirstThisAct())
            await Heal(3);
        if (MaterialId == "PLANISPHERE" && Progress[4] == 1 && room is not EventRoom && FirstThisAct())
        {
            Progress[4] = 0;
            await Heal(3);
        }
        if (MaterialId == "PLANISPHERE" && room is EventRoom { CanonicalEvent: not AncientEventModel } &&
            Owner.RunState.CurrentMapPoint?.PointType == MapPointType.Unknown)
            Progress[4] = 1;
    }

    public override async Task AfterItemPurchased(Player player, MerchantEntry entry, int goldSpent)
    {
        if (IsMe(player) && MaterialId == "MEAL_TICKET" && Progress[3] == 1)
        {
            Progress[3] = 0;
            await Heal(4);
        }
    }

    public override async Task AfterRestSiteHeal(Player player, bool isMimicked)
    {
        await Native.AfterRestSiteHeal(player, isMimicked);
        if (!IsMe(player)) return;
        if (MaterialId is "REGAL_PILLOW" or "STONE_HUMIDIFIER") Progress[2] = 1;
        if (MaterialId == "TINY_MAILBOX" && !Owner.HasOpenPotionSlots) await Heal(4);
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        int flash = _nativeFlashes;
        CardModel[] potential = MaterialId == "FISHING_ROD"
            ? Owner.Deck.Cards.Where(card => card.IsUpgradable && !card.Id.Entry.StartsWith("STRIKE_", StringComparison.Ordinal) &&
                !card.Id.Entry.StartsWith("DEFEND_", StringComparison.Ordinal)).ToArray()
            : [];
        await Native.AfterCombatEnd(room);
        if (MaterialId == "FISHING_ROD" && _nativeFlashes > flash && potential.Any(card => card.IsUpgraded))
            await PlayerCmd.GainGold(10, Owner);
        _combat = false;
    }

    public override async Task AfterCombatVictoryEarly(CombatRoom room)
    {
        bool quarter = Me.CurrentHp * 4 <= Me.MaxHp;
        await Native.AfterCombatVictoryEarly(room);
        if (MaterialId == "MEAT_ON_THE_BONE" && quarter) await Heal(3);
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        int upgradable = MaterialId == "WAR_HAMMER" ? Owner.Deck.Cards.Count(card => card.IsUpgradable) : 4;
        await Native.AfterCombatVictory(room);
        switch (MaterialId)
        {
            case "BURNING_BLOOD" when _combatFlags.Contains("own-card-loss"): await Heal(3); break;
            case "BLOOD_VIAL" when Low: await Heal(3); break;
            case "LAVA_LAMP" when !((MegaCrit.Sts2.Core.Models.Relics.LavaLamp)Native).TookDamageThisCombat && FirstThisAct():
                await PlayerCmd.GainGold(10, Owner); break;
            case "WAR_HAMMER" when room.RoomType == RoomType.Elite && upgradable < 4:
                await PlayerCmd.GainGold(10, Owner); break;
        }
    }

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        Reward[] before = rewards.ToArray();
        bool result = Native.TryModifyRewards(player, rewards, room);
        if (!IsMe(player) || room is not CombatRoom) return result;

        if (MaterialId == "AMETHYST_AUBERGINE" && result && Low)
            rewards.Add(new GoldReward(10, Owner));
        if (MaterialId == "BOWLER_HAT" && before.OfType<GoldReward>().Sum(reward => reward.Amount) >= 30 && FirstThisAct())
        {
            rewards.Add(new GoldReward(10, Owner));
            result = true;
        }
        if (MaterialId == "WHITE_STAR" && result)
            foreach (CardReward reward in rewards.Except(before).OfType<CardReward>())
                RewardSources.GetOrCreateValue(reward).Add(this);
        if (MaterialId == "WHITE_BEAST_STATUE" && room.RoomType == RoomType.Elite)
        {
            var potion = rewards.OfType<PotionReward>().FirstOrDefault();
            if (potion != null && (FirstThisAct() || Progress[5] == Owner.RunState.TotalFloor))
            {
                Progress[5] = Owner.RunState.TotalFloor;
                PotionChoiceSources.AddOrUpdate(potion, this);
            }
        }
        return result;
    }

    public override bool TryModifyRestSiteHealRewards(Player player, List<Reward> rewards, bool isMimicked)
    {
        Reward[] before = rewards.ToArray();
        bool result = Native.TryModifyRestSiteHealRewards(player, rewards, isMimicked);
        if (MaterialId == "DREAM_CATCHER" && IsMe(player))
            foreach (CardReward reward in rewards.Except(before).OfType<CardReward>())
                RewardSources.GetOrCreateValue(reward).Add(this);
        return result;
    }

    public override bool TryModifyCardRewardOptions(Player player, List<CardCreationResult> cardRewards, CardCreationOptions creationOptions)
    {
        int oldCount = cardRewards.Count;
        bool changed = Native.TryModifyCardRewardOptions(player, cardRewards, creationOptions);
        if (MaterialId == "LASTING_CANDY" && IsMe(player) && cardRewards.Count > oldCount)
            _markedCards.Add(cardRewards[^1].Card);
        return changed;
    }

    public override bool TryModifyCardRewardOptionsLate(Player player, List<CardCreationResult> cardRewards, CardCreationOptions options)
    {
        var eligible = FresnelCandidates(player, cardRewards);
        bool changed = Native.TryModifyCardRewardOptionsLate(player, cardRewards, options);
        StrengthenFirstNimble(eligible?.Select(result => result.Card).FirstOrDefault(card => card.Enchantment is Nimble));
        return changed;
    }

    public override void ModifyMerchantCardCreationResults(Player player, List<CardCreationResult> cards)
    {
        var eligible = FresnelCandidates(player, cards);
        Native.ModifyMerchantCardCreationResults(player, cards);
        StrengthenFirstNimble(eligible?.Select(result => result.Card).FirstOrDefault(card => card.Enchantment is Nimble));
    }

    public override bool TryModifyCardBeingAddedToDeck(CardModel card, out CardModel? newCard)
    {
        bool changed = Native.TryModifyCardBeingAddedToDeck(card, out newCard);
        if (MaterialId == "FRESNEL_LENS" && changed)
            StrengthenFirstNimble(newCard);
        return changed;
    }

    private CardCreationResult[]? FresnelCandidates(Player player, List<CardCreationResult> cards) =>
        MaterialId == "FRESNEL_LENS" && IsMe(player) && Progress[0] != Owner.RunState.CurrentActIndex + 1
            ? cards.Where(result => result.Card.Enchantment == null).ToArray() : null;

    private void StrengthenFirstNimble(CardModel? card)
    {
        if (card?.Enchantment is Nimble nimble && FirstThisAct())
            nimble.Amount += 1;
    }

    public override bool TryModifyCardRewardAlternatives(Player player, CardReward reward, List<CardRewardAlternative> alternatives)
    {
        if (MaterialId == "PAELS_WING" && IsMe(player))
            _sacrificed = ((MegaCrit.Sts2.Core.Models.Relics.PaelsWing)Native).RewardsSacrificed;
        return Native.TryModifyCardRewardAlternatives(player, reward, alternatives);
    }

    internal async Task RecordSelectedCard(CardReward reward, CardModel? picked, bool rerolled)
    {
        if (MaterialId == "PAELS_WING" && picked == null && ((MegaCrit.Sts2.Core.Models.Relics.PaelsWing)Native).RewardsSacrificed > _sacrificed &&
            ((MegaCrit.Sts2.Core.Models.Relics.PaelsWing)Native).RewardsSacrificed % 2 == 0 && FirstThisAct())
            await PlayerCmd.GainGold(10, Owner);
        if (picked == null) return;
        switch (MaterialId)
        {
            case "WHITE_STAR" or "DREAM_CATCHER" when RewardSources.TryGetValue(reward, out var sources) &&
                 sources.Contains(this) && FirstThisAct():
                if (picked.IsUpgradable) CardCmd.Upgrade(picked);
                break;
            case "LASTING_CANDY" when _markedCards.Any(card => card.Id == picked.Id):
                await Heal(3); break;
            case "LUCKY_FYSH" when picked.IsUpgraded && FirstThisAct(): await PlayerCmd.GainGold(15, Owner); break;
            case "DRIFTWOOD" when rerolled && FirstThisAct(): await Heal(3); break;
            case "PRISMATIC_GEM" when picked.Pool != Owner.Character.CardPool && picked.Pool != ModelDb.CardPool<MegaCrit.Sts2.Core.Models.CardPools.ColorlessCardPool>() && FirstThisAct():
                await Heal(4); break;
            case "GLITTER" when picked.Enchantment is Glam && FirstThisAct(): await Heal(3); break;
            case "WING_CHARM" when picked.Enchantment is Swift && FirstThisAct(): await PlayerCmd.GainGold(4, Owner); break;
            case "DINGY_RUG" when picked.Pool == ModelDb.CardPool<MegaCrit.Sts2.Core.Models.CardPools.ColorlessCardPool>() && FirstThisAct():
                if (picked.IsUpgradable) CardCmd.Upgrade(picked);
                break;
        }
    }

    [HarmonyPatch(typeof(CardReward), "OnSelect")]
    private static class CardRewardSelectionPatch
    {
        private static readonly AccessTools.FieldRef<CardReward, bool> HasRerolled =
            AccessTools.FieldRefAccess<CardReward, bool>("_hasBeenRerolled");

        private static void Prefix(CardReward __instance, out HashSet<CardModel>? __state)
        {
            Player player = __instance.Player;
            __state = player.Relics.Any(relic => relic is CatalogRelic && !relic.IsMelted)
                ? new HashSet<CardModel>(player.Deck.Cards, ReferenceEqualityComparer.Instance) : null;
        }

        private static void Postfix(CardReward __instance, HashSet<CardModel>? __state, ref Task<bool> __result)
        {
            if (__state != null)
                __result = RecordSelection(__result, __instance, __state);
        }

        private static async Task<bool> RecordSelection(Task<bool> original, CardReward reward, HashSet<CardModel> before)
        {
            bool result = await original;
            if (!result) return false;
            CardModel? picked = reward.Player.Deck.Cards.FirstOrDefault(card => !before.Contains(card));
            foreach (var relic in reward.Player.Relics.OfType<CatalogRelic>().Where(model => !model.IsMelted).ToArray())
                await relic.RecordSelectedCard(reward, picked, HasRerolled(reward));
            return true;
        }
    }
}
