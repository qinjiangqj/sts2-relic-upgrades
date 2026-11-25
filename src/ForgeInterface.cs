using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace AaaCode.RelicForge;

internal sealed class ForgeRestSiteOption(Player owner) : CustomRestSiteOption(owner)
{
    public override string OptionId => "AAACODE_RELIC_FORGE";
    public override string CustomIconPath => "res://aaacode_relic_forge/art/ui/forge_rest_site.res";
    public override bool IsEnabled => Owner.RunState is RunState state &&
        ForgeState.Fragments(state) >= ForgeRules.CampCost && ForgeService.GetChoices(Owner).Count > 0;

    public override LocString Description
    {
        get
        {
            LocString description = base.Description;
            description.Add("Fragments", Owner.RunState is RunState state ? ForgeState.Fragments(state) : 0);
            return description;
        }
    }

    public override async Task<bool> OnSelect()
    {
        if (!IsEnabled || Owner.RunState is not RunState state)
            return false;
        ForgeService.Choice? choice = await ForgeInterface.PickRecipe(Owner, state, chapter: false);
        if (choice == null || !await ForgeService.TryCraft(Owner, choice.Value, chapter: false))
            return false;

        ForgeInterface.ShowNotice(ForgeText.Pick(
            $"已升级为{choice.Value.Result.ForgeName}。剩余碎片 {ForgeState.Fragments(state)}。",
            $"Upgraded to {choice.Value.Result.ForgeName}. {ForgeState.Fragments(state)} fragments left."));
        return true;
    }
}

internal static class ForgeInterface
{
    internal static async Task<ForgeService.Choice?> PickRecipe(Player player, RunState state, bool chapter)
    {
        if (NRun.Instance is not { } run)
            return null;
        List<ForgeService.Choice> recipes = ForgeService.GetChoices(player);
        if (recipes.Count == 0)
            return null;
        int? picked = await new RelicUpgradeScreen(recipes, ForgeState.Fragments(state), chapter).Open(run);
        return picked.HasValue ? recipes[picked.Value] : null;
    }

    internal static Task<int?> ChooseIndex(string title, string terms, string confirmText, string cancelText,
        IReadOnlyList<(string Title, string Detail)> choices)
    {
        if (choices.Count == 0 || NRun.Instance is not { } run)
            return Task.FromResult<int?>(null);
        var result = new TaskCompletionSource<int?>();
        var dialog = new ConfirmationDialog
        {
            Title = title,
            OkButtonText = confirmText,
            CancelButtonText = cancelText,
            Exclusive = true,
            Transient = true,
            DialogHideOnOk = false,
        };
        var content = new VBoxContainer { CustomMinimumSize = new Vector2(630, 205) };
        var explanation = new Label { Text = terms, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        var select = new OptionButton { FitToLongestItem = false };
        var preview = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        content.AddThemeConstantOverride("separation", 14);
        content.AddChild(explanation);
        content.AddChild(select);
        content.AddChild(preview);
        foreach (var selection in choices) select.AddItem(selection.Title);
        select.Select(0);
        preview.Text = choices[0].Detail;
        select.ItemSelected += index => preview.Text = choices[(int)index].Detail;
        dialog.AddChild(content);
        run.AddChild(dialog);


        void Finish(int? choice)
        {
            if (result.Task.IsCompleted)
                return;
            dialog.Hide();
            if (!dialog.IsQueuedForDeletion())
                dialog.QueueFree();
            result.TrySetResult(choice);
        }

        dialog.Confirmed += () => Finish(select.Selected >= 0 ? select.Selected : null);
        dialog.Canceled += () => Finish(null);
        dialog.CloseRequested += () => Finish(null);
        dialog.TreeExiting += () => result.TrySetResult(null);
        dialog.PopupCentered(new Vector2I(730, 345));
        dialog.GetCancelButton().GrabFocus();
        return result.Task;
    }

    internal static void ShowNotice(string message)
    {
        if (NRun.Instance is not { } run || !run.IsInsideTree())
            return;

        var layer = new CanvasLayer { Layer = 12 };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        var panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            GrowHorizontal = Control.GrowDirection.Begin,
            Modulate = new Color(1f, 0.94f, 0.79f),
        };
        var label = new Label { Text = message, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 20);
        run.AddChild(layer);
        layer.AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(panel);
        panel.AnchorLeft = 1;
        panel.AnchorRight = 1;
        panel.OffsetLeft = -28;
        panel.OffsetRight = -28;
        panel.OffsetTop = 114;
        panel.AddChild(label);

        SceneTreeTimer timer = run.GetTree().CreateTimer(4.5);
        timer.Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(layer))
                layer.QueueFree();
        };
    }
}
