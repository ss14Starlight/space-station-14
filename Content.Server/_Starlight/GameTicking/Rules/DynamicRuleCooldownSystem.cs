using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._Starlight.GameTicking;
using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.GameTicking.Presets;
using Content.Shared.GameTicking;
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

    private ISawmill _sawmill = default!;
    private bool _roundInitialized;
    private bool _dynamicRound;

    /// <summary>
    /// Rules blocked by cooldowns which were already active at the start of this round.
    /// Cooldowns activated during this round apply starting next round.
    /// </summary>
    public IReadOnlySet<EntProtoId> CurrentRuleCooldowns => _currentRuleCooldowns;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("dynamic.cooldown");
        SubscribeLocalEvent<DynamicRuleCooldownRoundInitializingEvent>(OnRoundInitializing);
        SubscribeLocalEvent<DynamicRuleCooldownRoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundInitializing(DynamicRuleCooldownRoundInitializingEvent ev) => BeginRound(ev.Preset);

    private void OnRoundStarted(DynamicRuleCooldownRoundStartedEvent ev) => ApplyPresetCooldown(ev.Preset);

    /// <summary>
    /// Initializes the cooldown snapshot for the current round.
    /// </summary>
    public void BeginRound(GamePresetPrototype preset) => EnsureRoundInitialized(IsDynamicPreset(preset));

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
        foreach (var (owner, component) in EnumerateCooldownDefinitions())
        {
            if (owner != selectedRule && !component.Rules.Contains(selectedRule))
                continue;

            ActivateCooldown(owner, component, component.Cooldown, $"rule {selectedRule}");
        }
    }

    /// <summary>
    /// Applies a selected preset's vote cooldown to every cooldown group linked to that preset.
    /// </summary>
    public void ApplyPresetCooldown(GamePresetPrototype preset)
    {
        EnsureRoundInitialized(IsDynamicPreset(preset));

        if (preset.VoteCooldown <= 0)
            return;

        var presetId = new ProtoId<GamePresetPrototype>(preset.ID);
        foreach (var (owner, component) in EnumerateCooldownDefinitions())
        {
            if (!component.Presets.Contains(presetId))
                continue;

            ActivateCooldown(owner, component, preset.VoteCooldown, $"preset {preset.ID}");
        }
    }

    /// <summary>
    /// Returns whether a preset is blocked this round by a linked Dynamic rule cooldown.
    /// </summary>
    public bool TryGetPresetCooldown(ProtoId<GamePresetPrototype> preset, out int remaining) => _currentPresetCooldowns.TryGetValue(preset, out remaining);

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

            foreach (var preset in component.Presets)
            {
                if (!_currentPresetCooldowns.TryGetValue(preset, out var current) || current < state.Remaining)
                    _currentPresetCooldowns[preset] = state.Remaining;
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
        foreach (var prototype in _prototypeManager.EnumeratePrototypes<EntityPrototype>())
        {
            if (prototype.Abstract ||
                !prototype.TryComp<DynamicRuleCooldownComponent>(out var component, EntityManager.ComponentFactory))
            {
                continue;
            }

            yield return (prototype.ID, component);
        }
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
