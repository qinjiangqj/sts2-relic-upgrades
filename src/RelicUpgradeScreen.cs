using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Nodes;

namespace AaaCode.RelicForge;

internal sealed class RelicUpgradeScreen(IReadOnlyList<ForgeService.Choice> choices, int fragments, bool chapter)
{
    private static readonly Color Shell = new("18282C");
    private static readonly Color Stone = new("203438");
    private static readonly Color BrightStone = new("2C494D");
    private static readonly Color Empty = new("111F24");
    private static readonly Color Edge = new("42676C");
    private static readonly Color Gold = new("E5BD76");
    private static readonly Color Paper = new("F3E8D6");
    private static readonly Color Muted = new("AFC8C7");
    private static readonly Color Upgrade = new("ACD29C");

    private readonly TaskCompletionSource<int?> _selection = new();
    private readonly List<(Button Button, Label Label, TextureRect Art)> _tiles = new();
    private readonly CanvasLayer _layer = new() { Name = "RelicUpgradeLayer", Layer = 30 };
    private readonly Label _prompt = TextLabel("", 18, Muted);
    private readonly GridContainer _grid = new() { Name = "RelicGrid", Columns = 5, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
    private readonly VBoxContainer _picker = new() { Name = "RelicPicker", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
    private readonly VBoxContainer _comparison = new() { Name = "RelicComparison", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
    private readonly HBoxContainer _comparisonCards = new() { Alignment = BoxContainer.AlignmentMode.Center };
    private readonly Label _starterNote = TextLabel("", 16, Gold);
    private readonly Button _back = ActionButton("RelicUpgradeBack");
    private readonly Button _preview = ActionButton("RelicUpgradeToggle");
    private readonly Button _confirm = ActionButton("RelicUpgradeConfirm", primary: true);
    private int _index = -1;

    internal Task<int?> Open(NRun run)
    {
        Control? previousFocus = run.GetViewport().GuiGetFocusOwner();
        run.AddChild(_layer);
        var root = new Control { Name = "RelicUpgradeScreen", MouseFilter = Control.MouseFilterEnum.Stop };
        _layer.AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.GuiInput += HandleCancel;
        root.TreeExiting += () => _selection.TrySetResult(null);

        var shade = new ColorRect { Color = new Color(0.025f, 0.042f, 0.047f, 0.76f), MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        shade.AnchorTop = 0.102f;

        var frame = new PanelContainer { Name = "RelicUpgradeBoard", MouseFilter = Control.MouseFilterEnum.Stop };
        frame.AddThemeStyleboxOverride("panel", Border(new Color(Shell, 0.24f), Colors.Transparent, 0, 22));
        root.AddChild(frame);
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        frame.AnchorLeft = 0.085f;
        frame.AnchorRight = 0.915f;
        frame.AnchorTop = 0.135f;
        frame.AnchorBottom = 0.921f;
        frame.GuiInput += HandleCancel;

        var board = new VBoxContainer();
        board.AddThemeConstantOverride("separation", 12);
        frame.AddChild(board);
        board.AddChild(BuildHeader());
        board.AddChild(BuildPicker());
        board.AddChild(BuildComparison());
        board.AddChild(BuildFooter());

        _back.Pressed += Back;
        _preview.Toggled += RenderTiles;
        _confirm.Pressed += () =>
        {
            if (_index >= 0)
                Close(_index);
        };
        _confirm.GuiInput += HandleCancel;
        _back.GuiInput += HandleCancel;
        _preview.GuiInput += HandleCancel;
        ShowPicker(0);

        return RestoreFocusWhenClosed(previousFocus);
    }

    private Control BuildHeader()
    {
        var header = new HBoxContainer();
        var labels = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        labels.AddChild(TextLabel(chapter ? ForgeText.Pick("章末免费升级", "Free Chapter Upgrade") :
            ForgeText.Pick("遗物升级", "Relic Upgrade"), 28, Paper));
        labels.AddChild(_prompt);
        header.AddChild(labels);
        string cost = chapter ? ForgeText.Pick("本次免费", "Free") :
            ForgeText.Pick($"消耗 {ForgeRules.CampCost} 碎片 · 持有 {fragments}", $"{ForgeRules.CampCost} fragments · {fragments} available");
        header.AddChild(TextLabel(cost, 19, Gold));
        return header;
    }

    private Control BuildPicker()
    {
        var scroll = new ScrollContainer
        {
            Name = "RelicListScroll",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        scroll.Resized += () =>
        {
            int columns = Math.Clamp((int)((scroll.Size.X - 16) / 181), 1, Math.Min(7, choices.Count));
            if (_grid.Columns != columns)
                _grid.Columns = columns;
        };
        scroll.GuiInput += HandleCancel;
        _grid.AddThemeConstantOverride("h_separation", 11);
        _grid.AddThemeConstantOverride("v_separation", 10);
        for (int i = 0; i < choices.Count; i++)
        {
            int index = i;
            var tile = ActionButton($"RelicChoice{index}");
            tile.CustomMinimumSize = new Vector2(169, 143);
            var cell = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            tile.AddChild(cell);
            cell.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            cell.OffsetLeft = 5;
            cell.OffsetRight = -5;
            cell.OffsetTop = 12;
            var icon = Artwork(79);
            var name = TextLabel("", 17, Paper);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            cell.AddChild(icon);
            cell.AddChild(name);
            tile.Pressed += () => ShowComparison(index);
            tile.GuiInput += HandleCancel;
            _grid.AddChild(tile);
            _tiles.Add((tile, name, icon));
        }
        var centeredRows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        centeredRows.AddChild(_grid);
        scroll.AddChild(centeredRows);
        _picker.AddChild(scroll);
        return _picker;
    }

    private Control BuildComparison()
    {
        _comparison.AddThemeConstantOverride("separation", 8);
        _comparisonCards.AddThemeConstantOverride("separation", 13);
        var center = new CenterContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        center.AddChild(_comparisonCards);
        _comparison.AddChild(center);
        _comparison.Resized += SizeComparison;
        _starterNote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _comparison.AddChild(_starterNote);
        return _comparison;
    }

    private Control BuildFooter()
    {
        var footer = new HBoxContainer { Name = "RelicUpgradeActions" };
        footer.AddThemeConstantOverride("separation", 10);
        _preview.ToggleMode = true;
        footer.AddChild(_back);
        footer.AddChild(_preview);
        footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        _confirm.Text = ForgeText.Pick("确认升级", "Upgrade");
        footer.AddChild(_confirm);
        return footer;
    }

    private void ShowPicker(int focus)
    {
        _index = -1;
        _comparison.Hide();
        _confirm.Hide();
        _picker.Show();
        _preview.Show();
        _prompt.Text = ForgeText.Pick("选择一件遗物查看升级前后效果", "Choose a relic to compare its effects");
        _back.Text = chapter ? ForgeText.Pick("跳过", "Skip") : ForgeText.Pick("返回火堆", "Back to Rest Site");
        RenderTiles(_preview.ButtonPressed);
        LinkFocus(_tiles.Select(tile => tile.Button).Append(_back).Append(_preview).ToArray());
        _tiles[focus].Button.GrabFocus();
    }

    private void RenderTiles(bool upgraded)
    {
        _preview.Text = upgraded ? ForgeText.Pick("查看升级前", "Show Originals") : ForgeText.Pick("查看升级后", "Preview Upgrades");
        for (int i = 0; i < _tiles.Count; i++)
        {
            var (button, name, icon) = _tiles[i];
            var choice = choices[i];
            name.Text = upgraded ? choice.Result.ForgeName : choice.Source.Title.GetFormattedText();
            name.Modulate = upgraded ? Upgrade : Colors.White;
            icon.Texture = upgraded ? choice.Result.Icon : choice.Source.Icon;
            button.TooltipText = name.Text;
        }
    }

    private void ShowComparison(int index)
    {
        _index = index;
        var choice = choices[index];
        foreach (Node child in _comparisonCards.GetChildren())
        {
            _comparisonCards.RemoveChild(child);
            child.QueueFree();
        }
        _comparisonCards.AddChild(ComparisonCard("RelicBefore", ForgeText.Pick("升级前", "Before"),
            choice.Source.Title.GetFormattedText(), choice.Source.DynamicDescription.GetFormattedText(), choice.Source.Icon, upgraded: false));
        var arrow = TextLabel("→", 30, Gold);
        arrow.VerticalAlignment = VerticalAlignment.Center;
        _comparisonCards.AddChild(arrow);
        _comparisonCards.AddChild(ComparisonCard("RelicAfter", ForgeText.Pick("升级后", "After"),
            choice.Result.ForgeName, choice.Result.ForgeEffect, choice.Result.BigIcon, upgraded: true));
        bool starter = choice.Source.Rarity == RelicRarity.Starter;
        _starterNote.Text = starter ? ForgeText.Pick("初始遗物升级后，本局无法再为这件遗物选择先古升级。",
            "Upgrading this starter relic uses up its Ancient upgrade route for this run.") : "";
        _starterNote.Visible = starter;
        _picker.Hide();
        _preview.Hide();
        _comparison.Show();
        _confirm.Show();
        _prompt.Text = chapter ? ForgeText.Pick("所选遗物将替换为升级形态", "Replace the selected relic with its upgraded form") :
            ForgeText.Pick("确认后消耗本次火堆行动", "Confirming uses this rest-site action");
        _back.Text = ForgeText.Pick("返回重选", "Choose Again");
        SizeComparison();
        LinkFocus([_back, _confirm]);
        _back.GrabFocus();
    }

    private void SizeComparison()
    {
        float width = Math.Clamp((_comparison.Size.X - 66) / 2, 144, 386);
        float height = Math.Clamp(_comparison.Size.Y - 44, 244, 338);
        foreach (PanelContainer card in _comparisonCards.GetChildren().OfType<PanelContainer>())
            card.CustomMinimumSize = new Vector2(width, height);
    }

    private PanelContainer ComparisonCard(string nodeName, string caption, string name, string description, Texture2D icon, bool upgraded)
    {
        var panel = new PanelContainer { Name = nodeName, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", Border(upgraded ? new Color("20352D") : Empty, upgraded ? Upgrade.Darkened(0.38f) : Edge, 1, 11));
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 5);
        column.AddChild(TextLabel(caption, 17, upgraded ? Upgrade : Muted));
        var art = Artwork(102);
        art.Texture = icon;
        column.AddChild(art);
        var heading = TextLabel(name, 23, upgraded ? Upgrade : Paper);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        heading.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(heading);
        var effect = new RichTextLabel
        {
            Name = "RelicEffect",
            Text = FormatEffect(description),
            BbcodeEnabled = true,
            ScrollActive = true,
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 54),
        };
        effect.AddThemeFontSizeOverride("normal_font_size", 18);
        effect.AddThemeColorOverride("default_color", Paper);
        column.AddChild(effect);
        panel.AddChild(column);
        return panel;
    }

    private static string FormatEffect(string text) => text
        .Replace("[gold]", "[color=#e5bd76]", StringComparison.Ordinal).Replace("[/gold]", "[/color]", StringComparison.Ordinal)
        .Replace("[blue]", "[color=#9acbdc]", StringComparison.Ordinal).Replace("[/blue]", "[/color]", StringComparison.Ordinal)
        .Replace("[purple]", "[color=#d3b3e9]", StringComparison.Ordinal).Replace("[/purple]", "[/color]", StringComparison.Ordinal)
        .Replace("[red]", "[color=#e4a08e]", StringComparison.Ordinal).Replace("[/red]", "[/color]", StringComparison.Ordinal)
        .Replace("[green]", "[color=#acd29c]", StringComparison.Ordinal).Replace("[/green]", "[/color]", StringComparison.Ordinal);

    private void HandleCancel(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } ||
            input.IsActionPressed("ui_cancel") || input.IsActionPressed(MegaInput.cancel))
        {
            _layer.GetViewport().SetInputAsHandled();
            Back();
        }
    }

    private void Back()
    {
        if (_index >= 0)
            ShowPicker(_index);
        else
            Close(null);
    }

    private void Close(int? index)
    {
        if (_selection.Task.IsCompleted)
            return;
        _layer.Hide();
        _layer.QueueFree();
        _selection.TrySetResult(index);
    }

    private async Task<int?> RestoreFocusWhenClosed(Control? previous)
    {
        int? picked = await _selection.Task;
        if (GodotObject.IsInstanceValid(previous) && previous!.IsInsideTree() && previous.IsVisibleInTree())
            previous.CallDeferred(Control.MethodName.GrabFocus);
        return picked;
    }

    private static void LinkFocus(Button[] buttons)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].FocusNext = buttons[i].GetPathTo(buttons[(i + 1) % buttons.Length]);
            buttons[i].FocusPrevious = buttons[i].GetPathTo(buttons[(i + buttons.Length - 1) % buttons.Length]);
        }
    }

    private static Label TextLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static TextureRect Artwork(int height) => new()
    {
        CustomMinimumSize = new Vector2(0, height),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    private static Button ActionButton(string name, bool primary = false)
    {
        var button = new Button { Name = name, CustomMinimumSize = new Vector2(134, 38) };
        button.AddThemeFontSizeOverride("font_size", 18);
        button.AddThemeColorOverride("font_color", primary ? Empty : Paper);
        button.AddThemeColorOverride("font_hover_color", primary ? Empty : Paper);
        button.AddThemeColorOverride("font_pressed_color", primary ? Empty : Paper);
        button.AddThemeColorOverride("font_focus_color", primary ? Empty : Paper);
        button.AddThemeStyleboxOverride("normal", Border(primary ? Gold : Stone, primary ? Gold : Edge, 1, 6));
        button.AddThemeStyleboxOverride("hover", Border(primary ? Gold.Lightened(0.16f) : BrightStone, Gold, 1, 6));
        button.AddThemeStyleboxOverride("pressed", Border(primary ? Gold.Darkened(0.08f) : BrightStone, Gold, 1, 6));
        button.AddThemeStyleboxOverride("focus", Border(Colors.Transparent, Gold, 2, 6));
        return button;
    }

    private static StyleBoxFlat Border(Color fill, Color edge, int width, int margin) => new()
    {
        BgColor = fill, BorderColor = edge,
        BorderWidthLeft = width, BorderWidthRight = width, BorderWidthTop = width, BorderWidthBottom = width,
        CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
        ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin, ContentMarginBottom = margin,
    };
}
