using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;

namespace AaaCode.RelicForge;

public abstract partial class CatalogRelic
{
    private sealed record ExtraPotion(PotionModel Potion);
    private static readonly ConditionalWeakTable<PotionReward, ExtraPotion> PotionAlternatives = new();

    [HarmonyPatch(typeof(PotionReward), "OnSelect")]
    private static class PotionRewardSelectionPatch
    {
        private static readonly MethodInfo SelectNative = AccessTools.Method(typeof(PotionReward), "OnSelect");
        private static readonly MethodInfo SetPotion = AccessTools.PropertySetter(typeof(PotionReward), nameof(PotionReward.Potion));

        private static bool Prefix(PotionReward __instance, ref Task<bool> __result)
        {
            if (!PotionChoiceSources.TryGetValue(__instance, out CatalogRelic? source) || __instance.Potion == null)
                return true;
            __result = PickThenClaim(__instance, source);
            return false;
        }

        private static async Task<bool> PickThenClaim(PotionReward reward, CatalogRelic source)
        {
            if (!reward.Player.HasOpenPotionSlots) return false;
            if (!PotionAlternatives.TryGetValue(reward, out ExtraPotion? alternative))
            {
                var next = new PotionReward(reward.Player);
                next.Populate();
                for (int i = 0; i < 4 && next.Potion?.Id == reward.Potion?.Id; i++)
                {
                    next = new PotionReward(reward.Player);
                    next.Populate();
                }
                if (next.Potion != null) PotionAlternatives.Add(reward, alternative = new(next.Potion));
            }

            PotionModel original = reward.Potion!;
            if (alternative != null && alternative.Potion.Id != original.Id)
            {
                PotionModel second = alternative.Potion;
                int? selection = await ForgeInterface.ChooseIndex(
                    ForgeText.Pick("兽纹药瓶架", "Beast Potion Rack"), ForgeText.Pick("从两瓶药水中领取一瓶。", "Choose one of the two potions."),
                    ForgeText.Pick("领取", "Claim"), ForgeText.Pick("返回", "Back"),
                    [(original.Title.GetFormattedText(), ""), (second.Title.GetFormattedText(), "")]);
                if (selection == null) return false;
                if (selection == 1) SetPotion.Invoke(reward, [second]);
            }
            PotionChoiceSources.Remove(reward);
            bool taken = await (Task<bool>)SelectNative.Invoke(reward, null)!;
            if (!taken)
            {
                SetPotion.Invoke(reward, [original]);
                PotionChoiceSources.AddOrUpdate(reward, source);
            }
            return taken;
        }
    }
}
