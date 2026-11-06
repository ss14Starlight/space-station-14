using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._Starlight.GameTicking;
using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.GameTicking.Presets;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking.Rules;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.GameTicking.Rules;

/// <summary>
/// Tracks cooldown groups shared by Dynamic rules and game presets between rounds.
/// </summary>
public sealed partial class DynamicRuleCooldownSystem : EntitySystem
{
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private readonly Dictionary<EntProtoId, CooldownState> _cooldowns = [];
    private readonly HashSet<EntProtoId> _currentRuleCooldowns = [];
    private readonly Dictionary<ProtoId<GamePresetPrototype>, int> _currentPresetCooldowns = [];
    private readonly HashSet<EntProtoId> _roundStartCooldowns = [];
    private readonly HashSet<EntProtoId> _advancedCooldowns = [];
    private static readonly HashSet<EntProtoId> _emptyRuleCooldowns = [];

    private ISawmill _sawmill = default!;
    private bool _roundInitialized;
    private bool _dynamicRound;
    private List<(EntProtoId Owner, DynamicRuleCooldownComponent Component)>? _cooldownDefinitions;

    /// <summary>
    /// Rules blocked by cooldowns which were already active at the start of this round.
    /// Cooldowns activated during this round apply starting next round.
    /// </summary>
    public IReadOnlySet<EntProtoId> CurrentRuleCooldowns => BypassCooldowns ? _emptyRuleCooldowns : _currentRuleCooldowns;

    /// <summary>
    /// Whether the resolved preset ignores incoming cooldowns for this round.
    /// </summary>
    public bool BypassCooldowns { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("dynamic.cooldown");
        SubscribeLocalEvent<DynamicRuleCooldownRoundInitializingEvent>(OnRoundInitializing);
        SubscribeLocalEvent<DynamicRuleCooldownRoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<GameRuleComponent, GameRuleAddedEvent>(OnRuleAdded);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnRoundInitializing(DynamicRuleCooldownRoundInitializingEvent ev) => BeginRound(ev.Preset);

    private void OnRoundStarted(DynamicRuleCooldownRoundStartedEvent ev) => ApplyPresetCooldown(ev.Preset);

    private void OnRuleAdded(EntityUid uid, GameRuleComponent component, ref GameRuleAddedEvent args) => ApplyRuleCooldown(args.RuleId);

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>())
            _cooldownDefinitions = null;
    }

    /// <summary>
    /// Initializes the cooldown snapshot for the current round.
    /// </summary>
    public void BeginRound(GamePresetPrototype preset)
    {
        BypassCooldowns = preset.BypassCooldowns;
        EnsureRoundInitialized(IsDynamicPreset(preset));
    }

    /// <summary>
    /// Ensures cooldowns have advanced for this round. Calls may upgrade a round to Dynamic,
    /// but initialization and each cooldown decrement happen at most once per round.
    /// </summary>
    public void EnsureRoundInitialized(bool dynamicRound)
    {
        if (!_roundInitialized)
        {
            _roundInitialized = true;
            _dynamicRound = dynamicRound;
            BuildRoundSnapshot();
            AdvanceEligibleCooldowns();
            return;
        }

        if (!dynamicRound || _dynamicRound)
            return;

        _dynamicRound = true;
        AdvanceEligibleCooldowns();
    }

    /// <summary>
    /// Activates every cooldown group containing the selected rule.
    /// </summary>
    public void ApplyRuleCooldown(EntProtoId selectedRule)
    {
        EnsureRoundInitialized(dynamicRound: false);

        foreach (var (owner, component) in EnumerateCooldownDefinitions())
        {
            if (owner != selectedRule && !component.Rules.Contains(selectedRule))
                continue;

            ActivateCooldown(owner, component, component.Cooldown, $"rule {selectedRule}");
        }
    }

    /// <summary>
    /// Applies cooldowns for a preset's rules as well as any explicitly linked cooldown groups.
    /// Dynamic's future rolls are recorded individually when their rules are added.
    /// </summary>
    public void ApplyPresetCooldown(GamePresetPrototype preset)
    {
        EnsureRoundInitialized(IsDynamicPreset(preset));

        // Secret resolves its actual preset after the outer Secret preset initializes the round.
        // The outer preset's later round-start event must not undo the resolved preset's bypass.
        BypassCooldowns |= preset.BypassCooldowns;

        foreach (var (owner, component) in EnumerateCooldownDefinitions())
        {
            if (!ContainsPreset(owner, component, preset))
                continue;

            ActivateCooldown(owner, component, Math.Max(component.Cooldown, preset.VoteCooldown), $"preset {preset.ID}");
        }
    }

    /// <summary>
    /// Returns whether a preset is blocked this round by a linked Dynamic rule cooldown.
    /// </summary>
    public bool TryGetPresetCooldown(ProtoId<GamePresetPrototype> preset, out int remaining)
    {
        remaining = 0;
        return !BypassCooldowns && _currentPresetCooldowns.TryGetValue(preset, out remaining);
    }

    private static bool ContainsPreset(EntProtoId owner, DynamicRuleCooldownComponent component, GamePresetPrototype preset) => component.Presets.Contains(new ProtoId<GamePresetPrototype>(preset.ID)) ||
                preset.Rules.Any(rule => rule == owner || component.Rules.Contains(rule));

    private void BuildRoundSnapshot()
    {
        _currentRuleCooldowns.Clear();
        _currentPresetCooldowns.Clear();
        _roundStartCooldowns.Clear();
        _advancedCooldowns.Clear();

        foreach (var (owner, state) in _cooldowns.ToArray())
        {
            if (state.Remaining <= 0 || !TryGetCooldownDefinition(owner, out var component))
            {
                _cooldowns.Remove(owner);
                continue;
            }

            _roundStartCooldowns.Add(owner);
            _currentRuleCooldowns.Add(owner);
            _currentRuleCooldowns.UnionWith(component.Rules);

            foreach (var preset in _prototypeManager.EnumeratePrototypes<GamePresetPrototype>())
            {
                if (preset.BypassCooldowns || !ContainsPreset(owner, component, preset))
                    continue;

                var presetId = new ProtoId<GamePresetPrototype>(preset.ID);
                if (!_currentPresetCooldowns.TryGetValue(presetId, out var current) || current < state.Remaining)
                    _currentPresetCooldowns[presetId] = state.Remaining;
            }
        }
    }

    private void AdvanceEligibleCooldowns()
    {
        foreach (var owner in _roundStartCooldowns)
        {
            if (_advancedCooldowns.Contains(owner) || !_cooldowns.TryGetValue(owner, out var state))
                continue;

            if (!_dynamicRound && !state.DecrementOnNonDynamicRounds)
                continue;

            _advancedCooldowns.Add(owner);
            state.Remaining--;

            if (state.Remaining <= 0)
                _cooldowns.Remove(owner);
        }
    }

    private void ActivateCooldown(
        EntProtoId owner,
        DynamicRuleCooldownComponent component,
        int duration,
        string source)
    {
        if (duration <= 0)
            return;

        if (!_cooldowns.TryGetValue(owner, out var state))
        {
            state = new CooldownState();
            _cooldowns.Add(owner, state);
        }

        state.Remaining = Math.Max(state.Remaining, duration);
        state.DecrementOnNonDynamicRounds = component.DecrementOnNonDynamicRounds;
        _sawmill.Info(
            $"Cooldown group {owner} activated by {source} for {state.Remaining} rounds " +
            $"(decrement on non-Dynamic: {state.DecrementOnNonDynamicRounds}).");
    }

    private bool IsDynamicPreset(GamePresetPrototype preset)
    {
        foreach (var rule in preset.Rules)
        {
            if (!_prototypeManager.TryIndex(rule, out var prototype))
                continue;

            if (prototype.HasComp<DynamicRuleComponent>(EntityManager.ComponentFactory))
                return true;
        }

        return false;
    }

    private IEnumerable<(EntProtoId Owner, DynamicRuleCooldownComponent Component)> EnumerateCooldownDefinitions()
    {
        if (_cooldownDefinitions != null)
            return _cooldownDefinitions;

        _cooldownDefinitions = [];
        foreach (var prototype in _prototypeManager.EnumeratePrototypes<EntityPrototype>())
        {
            if (prototype.Abstract ||
                !prototype.TryComp<DynamicRuleCooldownComponent>(out var component, EntityManager.ComponentFactory))
            {
                continue;
            }

            _cooldownDefinitions.Add((prototype.ID, component));
        }

        return _cooldownDefinitions;
    }

    private bool TryGetCooldownDefinition(
        EntProtoId owner,
        [NotNullWhen(true)] out DynamicRuleCooldownComponent? component)
    {
        component = null;
        return _prototypeManager.TryIndex(owner, out var prototype) &&
                    prototype.TryComp<DynamicRuleCooldownComponent>(out component, EntityManager.ComponentFactory);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent _)
    {
        _roundInitialized = false;
        _dynamicRound = false;
        BypassCooldowns = false;
        _currentRuleCooldowns.Clear();
        _currentPresetCooldowns.Clear();
        _roundStartCooldowns.Clear();
        _advancedCooldowns.Clear();
    }

    private sealed class CooldownState
    {
        public int Remaining;
        public bool DecrementOnNonDynamicRounds;
    }
}
