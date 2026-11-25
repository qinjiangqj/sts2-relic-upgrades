using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.addons.mega_text;

namespace AaaCode.RelicForge;

[HarmonyPatch(typeof(NTopBar), nameof(NTopBar.Initialize))]
internal static class ForgeFragmentCounter
{
    private const string CounterName = "ForgeFragmentCounter";

    private static void Postfix(NTopBar __instance, IRunState runState)
    {
        var gold = __instance.Gold;
        var container = gold.GetParent();
        if (container.GetNodeOrNull<Control>(CounterName) is { } previous)
        {
            container.RemoveChild(previous);
            previous.QueueFree();
        }
        if (runState is not RunState state || !ForgeService.IsSupported(state))
            return;

        var counter = new HBoxContainer
        {
            Name = CounterName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        counter.AddThemeConstantOverride("separation", gold.GetThemeConstant("separation"));

        var icon = (TextureRect)gold.GetNode<TextureRect>("GoldIcon").Duplicate(0);
        icon.Name = "FragmentIcon";
        icon.Texture = ResourceLoader.Load<Texture2D>(ForgeFragmentReward.FragmentTexturePath);
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.MouseFilter = Control.MouseFilterEnum.Ignore;
        counter.AddChild(icon);

        var count = (MegaLabel)gold.GetNode<MegaLabel>("%GoldLabel").Duplicate((int)Node.DuplicateFlags.Scripts);
        count.Name = "FragmentCount";
        count.CustomMinimumSize = new Vector2(60, count.CustomMinimumSize.Y);
        count.MouseFilter = Control.MouseFilterEnum.Ignore;
        counter.AddChild(count);

        container.AddChild(counter);
        container.MoveChild(counter, gold.GetIndex() + 1);

        void Refresh(RunState changed)
        {
            if (ReferenceEquals(state, changed))
                count.SetTextAutoSize(ForgeState.Fragments(state).ToString());
        }

        Refresh(state);
        ForgeState.FragmentsChanged += Refresh;
        counter.TreeExiting += () => ForgeState.FragmentsChanged -= Refresh;
    }
}
