using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AaaCode.RelicForge;

[HarmonyPatch(typeof(NRewardsScreen), nameof(NRewardsScreen.ShowScreen))]
internal static class ForgeFragmentReceipt
{
    private static void Postfix(RewardsSet set, bool isTerminal, IRunState runState, NRewardsScreen __result)
    {
        if (!isTerminal || runState is not RunState state || !ForgeService.IsSupported(state) ||
            set.Room is not CombatRoom room || room != state.CurrentRoom ||
            ForgeState.CombatDropAtCurrentFloor(state, room.Encounter.Id.Entry) is not int amount)
            return;

        int total = ForgeState.Fragments(state);
        // The game immediately hides the loot window when there are no selectable rewards.
        if (set.Rewards.Count == 0 || __result.GetNodeOrNull<VBoxContainer>("%RewardsContainer") is not { } list)
        {
            if (amount > 0) ShowNotice(amount, total);
            return;
        }

        list.AddChild(CreateReceipt(amount, total));
    }

    internal static void ShowNotice(int amount, int total) =>
        ForgeInterface.ShowNotice(ForgeText.Pick(
            $"锻造碎片 +{amount} · 现有 {total}",
            $"Forge fragments +{amount} · {total} total"));

    private static PanelContainer CreateReceipt(int amount, int total)
    {
        var panel = new PanelContainer
        {
            Name = "ForgeFragmentReceipt",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("18282C"),
            BorderColor = new Color("B68E56"),
            BorderWidthLeft = 3,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 17,
            ContentMarginRight = 14,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        });
        var text = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        text.AddThemeConstantOverride("separation", 1);
        text.AddChild(Line(ForgeText.Pick($"锻造碎片 +{amount}", $"Forge fragments +{amount}"), 23, new Color("E5BD76")));
        string status = amount > 0
            ? ForgeText.Pick($"已自动入账 · 现有 {total} 个", $"Credited automatically · {total} total")
            : ForgeText.Pick($"本场未掉落 · 现有 {total} 个", $"No fragments dropped · {total} total");
        text.AddChild(Line(status, 18, new Color("AFC8C7")));
        panel.AddChild(text);
        return panel;
    }

    private static Label Line(string value, int size, Color color)
    {
        var label = new Label { Text = value, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
}
