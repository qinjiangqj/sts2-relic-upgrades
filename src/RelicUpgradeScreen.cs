using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace AaaCode.RelicForge;

internal sealed class RelicUpgradeScreen(IReadOnlyList<ForgeService.Choice> choices, int fragments, bool chapter)
{
    private const int TileWidth = 208;
    private const int TileSpacing = 22;
    private const string TextFontPath = "res://themes/kreon_regular_shared.tres";
    private const string CheckboxPath = "res://images/atlases/ui_atlas.sprites/checkbox_";

    private static readonly Color Gold = new("EFC851");
    private static readonly Color Paper = new("FFF6E2");
    private static readonly Color Upgrade = new("ACD29C");

    private readonly TaskCompletionSource<int?> _selection = new();
    private readonly List<(Button Button, Label Label, TextureRect Art)> _tiles = new();
    private readonly CanvasLayer _layer = new() { Name = "RelicUpgradeLayer", Layer = 30 };
    private readonly Label _prompt = TextLabel("", 26, Paper);
    private readonly GridContainer _grid = new() { Name = "RelicGrid", Columns = 5 };
    private readonly ScrollContainer _picker = new()
    {
        Name = "RelicPicker",
        HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        FollowFocus = true,
    };
    private readonly CenterContainer _comparison = new() { Name = "RelicComparison" };
    private readonly HBoxContainer _comparisonCards = new() { Alignment = BoxContainer.AlignmentMode.Center };
    private readonly Label _starterNote = TextLabel("", 17, Gold);
    private readonly NBackButton _back = PreloadManager.Cache.GetScene("res://scenes/ui/back_button.tscn").Instantiate<NBackButton>();
    private readonly NConfirmButton _confirm = PreloadManager.Cache.GetScene("res://scenes/ui/confirm_button.tscn").Instantiate<NConfirmButton>();
    private readonly Button _preview = new() { Name = "RelicUpgradeToggle", ToggleMode = true };
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

        var shade = new ColorRect { Color = new Color(0.008f, 0.014f, 0.017f, 0.95f), MouseFilter = Control.MouseFilterEnum.Ignore };
        Place(root, shade, 0, 0.075f, 1, 1);
        Place(root, BuildPicker(), 0.105f, 0.15f, 0.895f, 0.87f);
        Place(root, BuildComparison(), 0.16f, 0.15f, 0.84f, 0.87f);
        var footer = BuildFooter();
        Place(root, footer, 0.21f, 1, 0.79f, 1);
        footer.OffsetTop = -103;
        footer.OffsetBottom = -21;

        Place(root, BuildPreview(), 0, 1, 0, 1);
        _preview.OffsetLeft = 24;
        _preview.OffsetRight = 246;
        _preview.OffsetTop = -68;
        _preview.OffsetBottom = -24;

        _back.Name = "RelicUpgradeBack";
        _confirm.Name = "RelicUpgradeConfirm";
        _confirm.TooltipText = ForgeText.Pick("确认升级", "Confirm upgrade");
        root.AddChild(_back);
        root.AddChild(_confirm);
        _back.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => Back()));
        _confirm.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ =>
        {
            if (_index >= 0)
                Close(_index);
        }));
        _back.Enable();
        _confirm.GuiInput += HandleCancel;
        _back.GuiInput += HandleCancel;
        _preview.GuiInput += HandleCancel;
        ShowPicker(0);

        return RestoreFocusWhenClosed(previousFocus);
    }

    private Control BuildPicker()
    {
        _picker.Resized += () =>
        {
            _grid.Columns = Math.Clamp((int)((_picker.Size.X - 18 + TileSpacing) / (TileWidth + TileSpacing)), 1, Math.Min(5, choices.Count));
        };
        _picker.GuiInput += HandleCancel;
        _grid.AddThemeConstantOverride("h_separation", TileSpacing);
        _grid.AddThemeConstantOverride("v_separation", 22);
        for (int i = 0; i < choices.Count; i++)
        {
            int index = i;
            var tile = new Button { Name = $"RelicChoice{index}", CustomMinimumSize = new Vector2(TileWidth, 205) };
            StyleTransparentButton(tile, showHover: true);
            var cell = new VBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            cell.AddThemeConstantOverride("separation", 6);
            tile.AddChild(cell);
            cell.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            cell.OffsetLeft = 10;
            cell.OffsetRight = -10;
            var icon = Artwork(132);
            var name = TextLabel("", 23, Paper);
            name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            cell.AddChild(icon);
            cell.AddChild(name);
            tile.Pressed += () => ShowComparison(index);
            tile.GuiInput += HandleCancel;
            AddFocusMotion(tile, cell);
            _grid.AddChild(tile);
            _tiles.Add((tile, name, icon));
        }
        var centeredRows = new CenterContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        centeredRows.AddChild(_grid);
        _picker.AddChild(centeredRows);
        return _picker;
    }

    private Control BuildComparison()
    {
        _comparisonCards.AddThemeConstantOverride("separation", 25);
        _comparison.AddChild(_comparisonCards);
        _comparison.Resized += SizeComparison;
        _starterNote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return _comparison;
    }

    private Control BuildFooter()
    {
        var footer = new VBoxContainer
        {
            Name = "RelicUpgradeActions",
            Alignment = BoxContainer.AlignmentMode.End,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        footer.AddThemeConstantOverride("separation", 3);
        string cost = chapter ? ForgeText.Pick("本次免费", "Free upgrade") :
            ForgeText.Pick($"消耗 {ForgeRules.CampCost} 个碎片 · 持有 {fragments}", $"Cost: {ForgeRules.CampCost} fragments · {fragments} available");
        footer.AddChild(_prompt);
        footer.AddChild(TextLabel(cost, 20, Gold));
        footer.AddChild(_starterNote);
        return footer;
    }

    private Control BuildPreview()
    {
        StyleTransparentButton(_preview, showHover: false);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        _preview.AddChild(row);
        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        row.OffsetLeft = 4;
        var mark = Artwork(34);
        mark.CustomMinimumSize = new Vector2(34, 34);
        mark.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        var uncheckedArt = ResourceLoader.Load<Texture2D>(CheckboxPath + "unticked.tres");
        var checkedArt = ResourceLoader.Load<Texture2D>(CheckboxPath + "ticked.tres");
        mark.Texture = uncheckedArt;
        row.AddChild(mark);
        var caption = TextLabel(ForgeText.Pick("查看升级", "View Upgrades"), 24, Gold);
        caption.AddThemeFontOverride("font", ResourceLoader.Load<Font>("res://themes/kreon_bold_glyph_space_one.tres"));
        row.AddChild(caption);
        _preview.Toggled += upgraded =>
        {
            mark.Texture = upgraded ? checkedArt : uncheckedArt;
            RenderTiles(upgraded);
        };
        return _preview;
    }

    private void ShowPicker(int focus)
    {
        _index = -1;
        _comparison.Hide();
        _confirm.Disable();
        _confirm.Hide();
        _starterNote.Hide();
        _picker.Show();
        _preview.Show();
        _prompt.Text = ForgeText.Pick("选择一件遗物升级。", "Choose a relic to upgrade.");
        _back.TooltipText = chapter ? ForgeText.Pick("跳过", "Skip") : ForgeText.Pick("返回火堆", "Back to Rest Site");
        RenderTiles(_preview.ButtonPressed);
        LinkFocus(_tiles.Select(tile => (Control)tile.Button).Append(_back).Append(_preview).ToArray());
        _tiles[focus].Button.GrabFocus();
    }

    private void RenderTiles(bool upgraded)
    {
        for (int i = 0; i < _tiles.Count; i++)
        {
            var (button, name, icon) = _tiles[i];
            var choice = choices[i];
            name.Text = upgraded ? choice.Result.ForgeName : choice.Source.Title.GetFormattedText();
            name.Modulate = upgraded ? Upgrade : Colors.White;
            icon.Texture = upgraded ? choice.Result.BigIcon : choice.Source.BigIcon;
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
            choice.Source.Title.GetFormattedText(), choice.Source.DynamicDescription.GetFormattedText(), choice.Source.BigIcon, upgraded: false));
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
        _confirm.Enable();
        _prompt.Text = chapter ? ForgeText.Pick("所选遗物将替换为升级形态", "Replace the selected relic with its upgraded form") :
            ForgeText.Pick("确认后消耗本次火堆行动", "Confirming uses this rest-site action");
        _back.TooltipText = ForgeText.Pick("返回重选", "Choose Again");
        SizeComparison();
        LinkFocus([_back, _confirm]);
        _back.GrabFocus();
    }

    private void SizeComparison()
    {
        float width = Math.Clamp((_comparison.Size.X - 90) / 2, 144, 374);
        float height = Math.Clamp(_comparison.Size.Y - 44, 244, 342);
        foreach (PanelContainer card in _comparisonCards.GetChildren().OfType<PanelContainer>())
            card.CustomMinimumSize = new Vector2(width, height);
    }

    private PanelContainer ComparisonCard(string nodeName, string caption, string name, string description, Texture2D icon, bool upgraded)
    {
        var panel = new PanelContainer { Name = nodeName, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", Border(new Color(0.075f, 0.095f, 0.105f, 0.96f),
            upgraded ? Upgrade.Darkened(0.44f) : new Color("788C91"), 1, 18));
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 5);
        column.AddChild(TextLabel(caption, 18, upgraded ? Upgrade : Gold));
        var art = Artwork(122);
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
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 54),
        };
        effect.AddThemeFontSizeOverride("normal_font_size", 18);
        effect.AddThemeFontOverride("normal_font", ResourceLoader.Load<Font>(TextFontPath));
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

    private static void LinkFocus(Control[] buttons)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].FocusNext = buttons[i].GetPathTo(buttons[(i + 1) % buttons.Length]);
            buttons[i].FocusPrevious = buttons[i].GetPathTo(buttons[(i + buttons.Length - 1) % buttons.Length]);
        }
    }

    private static Label TextLabel(string text, int size, Color color)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontOverride("font", ResourceLoader.Load<Font>(TextFontPath));
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.79f));
        label.AddThemeConstantOverride("outline_size", 6);
        return label;
    }

    private static TextureRect Artwork(int height) => new()
    {
        CustomMinimumSize = new Vector2(0, height),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    private static void Place(Control root, Control child, float left, float top, float right, float bottom)
    {
        root.AddChild(child);
        child.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        child.AnchorLeft = left;
        child.AnchorTop = top;
        child.AnchorRight = right;
        child.AnchorBottom = bottom;
    }

    private static void StyleTransparentButton(Button button, bool showHover)
    {
        var clear = new StyleBoxEmpty();
        button.AddThemeStyleboxOverride("normal", clear);
        button.AddThemeStyleboxOverride("pressed", clear);
        button.AddThemeStyleboxOverride("hover_pressed", clear);
        button.AddThemeStyleboxOverride("hover", showHover
            ? Border(new Color(1, 1, 1, 0.045f), Colors.Transparent, 0, 0) : clear);
        button.AddThemeStyleboxOverride("focus", Border(Colors.Transparent, new Color(Gold, 0.78f), 2, 0));
    }

    private static void AddFocusMotion(Button tile, Control art)
    {
        Tween? motion = null;
        void Update()
        {
            motion?.Kill();
            float scale = tile.IsHovered() || tile.HasFocus() ? 1.09f : 1;
            motion = art.CreateTween();
            motion.TweenProperty(art, "scale", new Vector2(scale, scale), 0.13).SetEase(Tween.EaseType.Out);
        }
        art.Resized += () => art.PivotOffset = art.Size / 2;
        tile.FocusEntered += Update;
        tile.FocusExited += Update;
        tile.MouseEntered += Update;
        tile.MouseExited += Update;
    }

    private static StyleBoxFlat Border(Color fill, Color edge, int width, int margin) => new()
    {
        BgColor = fill, BorderColor = edge,
        BorderWidthLeft = width, BorderWidthRight = width, BorderWidthTop = width, BorderWidthBottom = width,
        CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
        ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin, ContentMarginBottom = margin,
    };
}
