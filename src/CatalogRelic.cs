using System.Runtime.CompilerServices;
using System.Text.Json;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace AaaCode.RelicForge;

public abstract partial class CatalogRelic : ForgedRelic
{
    private static readonly SavedSpireField<CatalogRelic, string> NativeState = new(() => "", "AAcodeForgeMaterial");
    private static readonly SavedSpireField<CatalogRelic, int[]> Persistent = new(() => new int[8], "AAcodeForgeProgress");
    private static readonly ConditionalWeakTable<RelicModel, CatalogRelic> ActiveShadows = new();
    private static readonly JsonSerializerOptions ShadowJson = new() { IncludeFields = true };
    private RelicModel? _native;
    private HashSet<string> _combatFlags = new(StringComparer.Ordinal);
    private HashSet<string> _turnFlags = new(StringComparer.Ordinal);
    private Dictionary<string, int> _tally = new(StringComparer.Ordinal);
    private Dictionary<string, int> _turnTally = new(StringComparer.Ordinal);
    private HashSet<string> _cardsThisTurn = new(StringComparer.Ordinal);
    private HashSet<CardModel> _markedCards = new(ReferenceEqualityComparer.Instance);
    private HashSet<CardModel> _playedMarked = new(ReferenceEqualityComparer.Instance);
    private bool _combat;
    private Creature? _previousAttackTarget;
    private bool _sameAttackTarget = true;
    private int _nativeFlashes;
    public override bool SpawnsPets => Original.SpawnsPets;
    public override bool AddsPet => Original.AddsPet;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _native = null;
        _combatFlags = new(StringComparer.Ordinal);
        _turnFlags = new(StringComparer.Ordinal);
        _tally = new(StringComparer.Ordinal);
        _turnTally = new(StringComparer.Ordinal);
        _cardsThisTurn = new(StringComparer.Ordinal);
        _markedCards = new(ReferenceEqualityComparer.Instance);
        _playedMarked = new(ReferenceEqualityComparer.Instance);
    }

    internal abstract string MaterialId { get; }
    internal abstract RelicModel Original { get; }
    protected override string PreviewIcon => MaterialId.ToLowerInvariant();
    internal override string EnglishName => ChineseName;
    internal override string EnglishEffect => ChineseEffect;
    public override bool IsAllowedInShops => false;
    protected override bool IsAllowedInternal(IRunState runState) => false;
    public override bool IsAllowedAtNeow(MegaCrit.Sts2.Core.Entities.Players.Player player) => false;
    public override bool ShowCounter => IsMutable && Native.ShowCounter;
    public override int DisplayAmount => !IsMutable ? 0 :
        MaterialId == "KUSARIGAMA" ? CountTurn("attacks") % 3 : Native.DisplayAmount;
    protected override string FallbackPackedIconPath => Original.PackedIconPath;
    public override bool IsUsedUp => IsMutable && Native.IsUsedUp;

    internal static void Register()
    {
        _ = NativeState;
        _ = Persistent;
    }

    protected RelicModel Native
    {
        get
        {
            if (_native is not null)
                return _native;
            string? stored = NativeState.Get(this);
            if (!string.IsNullOrEmpty(stored))
            {
                _native = Original.ToMutable();
                JsonSerializer.Deserialize<SavedProperties>(stored, ShadowJson)?.Fill(_native);
            }

            _native ??= Original.ToMutable();
            _native.Owner = Owner;
            ActiveShadows.Add(_native, this);
            _native.DisplayAmountChanged += InvokeDisplayAmountChanged;
            _native.StatusChanged += () => Status = _native.Status;
            if (_native.Status != Status)
                Status = _native.Status;
            return _native;
        }
    }

    internal void Inherit(RelicModel original)
    {
        NativeState.Set(this, JsonSerializer.Serialize(SavedProperties.From(original), ShadowJson));
        IsWax = original.IsWax;
    }

    internal void SaveNative()
    {
        if (_native != null)
            NativeState.Set(this, JsonSerializer.Serialize(SavedProperties.From(_native), ShadowJson));
    }

    public override Task AfterObtained() => Task.CompletedTask;

    private int Turn => Owner.PlayerCombatState?.TurnNumber ?? 0;
    private bool CombatOnce(string key) => _combatFlags.Add(key);
    private bool TurnOnce(string key) => _turnFlags.Add(key);
    private int Inc(string key) => _tally[key] = _tally.GetValueOrDefault(key) + 1;
    private int IncTurn(string key) => _turnTally[key] = _turnTally.GetValueOrDefault(key) + 1;
    private int Count(string key) => _tally.GetValueOrDefault(key);
    private int CountTurn(string key) => _turnTally.GetValueOrDefault(key);
    private int[] Progress
    {
        get
        {
            if (Persistent.Get(this) is { Length: >= 8 } values)
                return values;
            var defaults = new int[8];
            Persistent.Set(this, defaults);
            return defaults;
        }
    }
    private bool FirstThisAct()
    {
        int act = Owner.RunState.CurrentActIndex + 1;
        if (Progress[0] == act) return false;
        Progress[0] = act;
        return true;
    }

    private void StartCombatTracking()
    {
        _combat = true;
        _combatFlags.Clear();
        _tally.Clear();
        _markedCards.Clear();
        _playedMarked.Clear();
        ResetTurnTracking();
    }

    private void ResetTurnTracking()
    {
        _turnFlags.Clear();
        _turnTally.Clear();
        _cardsThisTurn.Clear();
        _previousAttackTarget = null;
        _sameAttackTarget = true;
    }

    [HarmonyPatch(typeof(RelicModel), nameof(ToSerializable))]
    private static class PersistNativePatch
    {
        private static void Prefix(RelicModel __instance)
        {
            if (__instance is CatalogRelic recipe)
                recipe.SaveNative();
        }
    }

    [HarmonyPatch(typeof(RelicModel), nameof(Flash), new[] { typeof(IEnumerable<Creature>) })]
    private static class ShowNativeFlashPatch
    {
        private static bool Prefix(RelicModel __instance, IEnumerable<Creature> targets)
        {
            if (!ActiveShadows.TryGetValue(__instance, out var forged))
                return true;
            forged._nativeFlashes++;
            forged.Flash(targets);
            return false;
        }
    }
}
