using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Starlight.Abstract.Extensions;
using Content.Shared._Starlight.Dice;
using Content.Shared.Administration.Logs;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Cargo;
using Content.Shared.Chat;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.EntityConditions;
using Content.Shared.EntityEffects;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Fluids;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Sprite;
using Content.Shared.Station;
using Content.Shared.Tabletop;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.DestinyDice;

public sealed partial class DestinyDiceSystem : EntitySystem
{
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedCargoSystem _cargo = default!;
    [Dependency] private SharedPuddleSystem _puddle = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private SharedGodmodeSystem _godmode = default!;
    [Dependency] private SharedTabletopSystem _tabletop = default!;
    [Dependency] private SharedExplosionSystem _explosion = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedAtmosphereSystem _atmos = default!;
    [Dependency] private SharedScaleVisualsSystem _scale = default!;
    [Dependency] private SharedEntityEffectsSystem _effects = default!;
    [Dependency] private SharedEntityConditionsSystem _conditions = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ISharedAdminLogManager _aLog = default!;
    // TODO: nuke system stuff on server

    /// List of all active destiny dice.
    private readonly List<Entity<DestinyDiceComponent>> _activeDice = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DestinyDiceComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<DestinyDiceComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<DestinyDiceComponent, ThrownEvent>(OnThrown);
        SubscribeLocalEvent<DestinyDiceComponent, LandEvent>(OnLand);
        SubscribeLocalEvent<DestinyDiceComponent, DestinyDiceEffectEndEvent>(OnEffectEnd);
        SubscribeLocalEvent<DestinyDiceComponent, DiceRolledEvent>(OnDiceRolled);
    }

    private void OnComponentStartup(Entity<DestinyDiceComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.PresetAdded || ent.Comp.Preset is null) return;
        if (!_proto.TryIndex(ent.Comp.Preset, out var preset)) return;

        List<DestinyDiceEffectGroup> groups = [];
        foreach (var groupProtoId in preset.Groups)
        {
            Log.Info($"{groupProtoId}");
            if (!_proto.TryIndex(groupProtoId, out var groupProto)) return;
            Log.Info($"exists");
            var group = ProtoToGroup(groupProto);
            foreach (var effectProtoId in groupProto.Effects)
            {
                Log.Info($"{effectProtoId}");
                if (!_proto.TryIndex(effectProtoId, out var effectProto)) return;
                Log.Info("exists");
                group.Effects.Add(ProtoToEffect(effectProto));
            }

            groups.Add(group);
        }

        ent.Comp.EffectGroups.AddRange(groups);
        ent.Comp.PresetAdded = true;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev) =>
        _activeDice.Clear();

    private void OnUseInHand(Entity<DestinyDiceComponent> ent, ref UseInHandEvent args) =>
        AssignActiveValues(ent, ent, args.User);

    private void OnThrown(Entity<DestinyDiceComponent> ent, ref ThrownEvent args)
    {
        if (ent.Comp.IsActive) return;
        ent.Comp.ActiveRoller = args.User;
    }

    private void OnLand(Entity<DestinyDiceComponent> ent, ref LandEvent args) =>
        AssignActiveValues(ent, ent, args.User);

    private void OnEffectEnd(Entity<DestinyDiceComponent> ent, ref DestinyDiceEffectEndEvent args) =>
        ent.Comp.WaitingForEffectEnd = false;

    private void OnDiceRolled(Entity<DestinyDiceComponent> ent, ref DiceRolledEvent args)
    {
        var (uid, comp) = ent;

        if (comp.ActiveRoller is null) return; // Needs to be intentionally rolled.
        if (comp.IsActive)
        {
            if (comp.BusyMessage is not null)
                _popup.PopupPredicted(Loc.GetString(comp.BusyMessage), uid, comp.ActiveRoller, comp.BusyPopupType);
            return;
        }
        if (_timing.CurTime < comp.NextAllowedRollTime)
        {
            if (comp.CooldownMessage is not null)
                _popup.PopupPredicted(Loc.GetString(comp.CooldownMessage), uid, comp.ActiveRoller, comp.CooldownPopupType);
            return;
        }

        comp.CurrentValue = args.Value;

        // Now we check which groups are eligible based on current value, and pick from the set.
        Dictionary<DestinyDiceEffectGroup, float> targetGroups = [];
        foreach (var group in comp.EffectGroups)
            foreach (var data in group.RollData)
            {
                if (data.TargetValue == comp.CurrentValue)
                {
                    targetGroups.Add(group, group.Weight ?? 1);
                    break;
                }

                if (!data.MinValue.HasValue && !data.MaxValue.HasValue)
                    continue;

                if (data.MinValue.HasValue != data.MaxValue.HasValue)
                    throw new Exception("MinMax is used for destiny die effect, but either min or max is not set.");

                if (comp.CurrentValue < data.MinValue!.Value || comp.CurrentValue > data.MaxValue!.Value)
                    continue;

                targetGroups.Add(group, group.Weight ?? 1);
                break;
            }

        if (targetGroups.Count == 0)
        {
            if (comp.NoEffectMessage is not null)
                _popup.PopupPredicted(Loc.GetString(comp.NoEffectMessage), uid, comp.ActiveRoller, comp.NoEffectPopupType);
            return;
        }

        var rolledGroup = _random.PickPredicted(_timing, targetGroups);
        rolledGroup.TimesRolled++;

        // Check for probability and conditions etc
        if ((rolledGroup.MaxRolls > -1 && rolledGroup.TimesRolled >= rolledGroup.MaxRolls) ||
            (rolledGroup.MaxTriggers > -1 && rolledGroup.TimesTriggered >= rolledGroup.MaxTriggers))
        {
            if (rolledGroup.ExhaustedMessage is not null)
                _popup.PopupPredicted(Loc.GetString(rolledGroup.ExhaustedMessage), uid, comp.ActiveRoller, rolledGroup.ExhaustedPopupType);
            return;
        }

        if (rolledGroup.Conditions.Count > 0)
            switch (rolledGroup.AllConditionsMustPass)
            {
                case true when !_conditions.TryConditions(uid, rolledGroup.Conditions.ToArray()):
                case false when !_conditions.TryAnyCondition(uid, rolledGroup.Conditions.ToArray()):
                    {
                        if (rolledGroup.FailureMessage is not null)
                            _popup.PopupPredicted(Loc.GetString(rolledGroup.FailureMessage), uid, comp.ActiveRoller, rolledGroup.FailurePopupType);
                        return;
                    }
            }

        if (!_random.ProbPredicted(_timing, rolledGroup.Probability))
        {
            if (rolledGroup.FailureMessage is not null)
                _popup.PopupPredicted(Loc.GetString(rolledGroup.FailureMessage), uid, comp.ActiveRoller, rolledGroup.FailurePopupType);
            return;
        }

        rolledGroup.TimesTriggered++;
        comp.CurrentEffectIndex = 0;
        comp.CurrentEffectGroup = rolledGroup;
        comp.IsActive = true;
        if (comp.GroupDelay.HasValue)
        {
            comp.GroupStartTime = _timing.CurTime + TimeSpan.FromSeconds(comp.GroupDelay.Value);
            comp.IsPending = true;
        }

        comp.NextAllowedRollTime = comp.RollDelay.HasValue
            ? _timing.CurTime + TimeSpan.FromSeconds(comp.RollDelay.Value)
            : TimeSpan.Zero;

        _activeDice.Add((uid, comp));
        _aLog.Add(LogType.Action, LogImpact.Low, $"Entity {ToPrettyString(uid)} rolled a Destiny Die and triggered an effect group.");
    }

    public override void Update(float delta)
    {
        foreach (var ent in _activeDice.ToList())
            ProcessDice(ent);
    }

    private void ProcessDice(Entity<DestinyDiceComponent> ent)
    {
        Log.Info($"CURRENT TIME: {_timing.CurTime}, FIRST TIME PREDICTED: {_timing.IsFirstTimePredicted}");
        var (uid, comp) = ent;
        var group = comp.CurrentEffectGroup;

        Log.Info($"GROUP: {(group is null ? "null" : "NOT NULL")}, ACTIVE: {comp.IsActive}, COUNT: {group?.Effects.Count.ToString() ?? "null"}, INDEX: {comp.CurrentEffectIndex}, IN PREDICTION: {_timing.InPrediction}, IN SIMULATION: {_timing.InSimulation}");
        if (group is null || !comp.IsActive || comp.CurrentEffectIndex >= group.Effects.Count)
        {
            _activeDice.Remove(ent);
            comp.IsActive = false;
            comp.IsPending = false;
            comp.CurrentEffectIndex = 0;
            comp.CurrentEffectGroup = null;
            comp.CurrentEffect = null;
            comp.EffectResults.Clear();
            Log.Info("CLEARING EVERYTHING!!!!");
            return;
        }

        // Skip if waiting for delay or for event.
        if (_timing.CurTime < comp.GroupStartTime) return;
        Log.Info("GROUP TIME PASS");

        if (comp is { IsPending: true, CurrentEffectGroup: not null })
        {
            comp.NextEffectTriggerTime = _timing.CurTime + TimeSpan.FromSeconds(comp.CurrentEffectGroup.Delay);

            if (comp.CurrentEffectGroup.SuccessMessage is not null)
                _popup.PopupPredicted(Loc.GetString(comp.CurrentEffectGroup.SuccessMessage), uid, comp.ActiveRoller,
                    comp.CurrentEffectGroup.SuccessPopupType);

            comp.IsPending = false;
        }

        if (_timing.CurTime < comp.NextEffectTriggerTime) return;
        if (comp.WaitingForEffectEnd) return;
        Log.Info("EFFECT TIME PASS");

        DestinyDiceEffect? effect = null;
        var earlyFinish = false;
        var foundValidEffect = false;

        // If one effect fails we want to try and start the next one immediately if possible.
        // Basically just going until we find an effect that passes checks or until we exhaust the list.
        var index = comp.CurrentEffectIndex;

        while (index < group.Effects.Count)
        {
            effect = group.Effects[index++];
            effect.TimesRolled++;

            if (effect.EntityEffect is null) continue; // Not valid.
            Log.Info("EFFECT NULL PASS");

            // Check for probability and conditions etc
            if ((effect.MaxRolls > -1 && effect.TimesRolled >= effect.MaxRolls) ||
                (effect.MaxTriggers > -1 && effect.TimesTriggered >= effect.MaxTriggers))
            {
                if (effect.ExhaustedMessage is not null)
                    _popup.PopupPredicted(Loc.GetString(effect.ExhaustedMessage), uid, comp.ActiveRoller,
                        effect.ExhaustedPopupType);
                comp.EffectResults.Add(effect, false);
                if (effect.RequiredTrigger)
                {
                    earlyFinish = true;
                    break;
                }

                continue;
            }

            if (effect.EntityEffect.Conditions is not null)
                switch (effect.AllConditionsMustPass)
                {
                    case true when !_conditions.TryConditions(uid, effect.EntityEffect.Conditions?.ToArray()):
                    case false when !_conditions.TryAnyCondition(uid, effect.EntityEffect.Conditions?.ToArray()):
                        {
                            comp.EffectResults.Add(effect, false);
                            if (effect.RequiredTrigger)
                            {
                                earlyFinish = true;
                                break;
                            }

                            continue;
                        }
                }

            var dependencyFail = false;
            foreach (var foundEffect in effect.DependsOnIds
                         .Select(id => group.Effects.FirstOrDefault(x => x.EffectId == id))
                         .OfType<DestinyDiceEffect>())
            {
                if (!comp.EffectResults.TryGetValue(foundEffect, out var effectResult)) continue;
                if (effectResult) continue;
                dependencyFail = true;
                comp.EffectResults.Add(effect, false);
                if (effect.RequiredTrigger)
                    earlyFinish = true;
                break;
            }

            if (dependencyFail) break;

            if (!_random.ProbPredicted(_timing, effect.Probability))
            {
                comp.EffectResults.Add(effect, false);
                if (effect.RequiredTrigger)
                {
                    earlyFinish = true;
                    break;
                }

                continue;
            }

            foundValidEffect = true;
            break;
        }

        if (earlyFinish || !foundValidEffect || effect?.EntityEffect is null)
        {
            if (effect?.FailureMessage is not null)
                _popup.PopupPredicted(Loc.GetString(effect.FailureMessage), uid, comp.ActiveRoller,
                    effect.FailurePopupType);
            comp.IsActive = false;
            _activeDice.Remove(ent);
            return;
        }

        effect.TimesTriggered++;
        comp.EffectResults.Add(effect, true);
        comp.CurrentEffect = effect;
        if (effect.EndOnEvent) comp.WaitingForEffectEnd = true;
        else comp.NextEffectTriggerTime = _timing.CurTime + TimeSpan.FromSeconds(effect.Delay);
        if (effect.SuccessMessage is not null)
            _popup.PopupPredicted(Loc.GetString(effect.SuccessMessage), uid, comp.ActiveRoller,
                effect.SuccessPopupType);
        // Effect is applied unconditionally here as effects are checked manually earlier.
        Log.Info("RUNNING EFFECT!!!!!!!");
        _effects.ApplyEffect(uid, effect.EntityEffect, user: uid);
        comp.CurrentEffectIndex = index;
    }

    /// Quick helper to assign active values to the component for the current effect to reference.
    private void AssignActiveValues(EntityUid uid, DestinyDiceComponent comp, EntityUid? roller)
    {
        if (comp.IsActive) return;
        comp.ActiveRoller = roller;
        comp.RollerGrid = roller is not null ? Transform(roller.Value).GridUid : EntityUid.Invalid;
        comp.ActiveGrid = Transform(uid).GridUid;
        comp.ActiveMap = Transform(uid).MapUid;
    }

    // ReSharper disable UseCollectionExpression - Sandboxing moment.
    /// <summary>
    /// Gets the effect targets for the current DD effect on the die.
    /// If you are using this from an entity effect, it should correspond to the DD effect that triggered it.
    /// </summary>
    /// <param name="ent">The destiny die entity.</param>
    /// <param name="targets">An <see cref="IEnumerable{EntityUid}"/> containing the target entities.</param>
    /// <returns>Boolean value based on if there are valid targets or not.</returns>
    public bool GetEffectTargets(Entity<DestinyDiceComponent> ent, [NotNullWhen(true)] out IEnumerable<EntityUid>? targets)
    {
        var (uid, comp) = ent;
        targets = null;

        switch (comp.CurrentEffect?.TargetData.TargetType)
        {
            case DestinyDiceTargetType.None:
                return false;
            case DestinyDiceTargetType.Self:
                targets = new [] { uid };
                return true;
            case DestinyDiceTargetType.Roller:
                targets = new[] { comp.ActiveRoller ?? EntityUid.Invalid };
                return true;
            case DestinyDiceTargetType.Filter:
                {
                    // This can very quickly get expensive so at least try to filter out the most things possible as early as possible.
                    var effect = comp.CurrentEffect;
                    targets = EntityManager.GetEntities();
                    if (effect.TargetData.TargetPrototypeId is not null)
                        targets = targets.Where(x =>
                            MetaData(x).EntityPrototype?.ID == effect.TargetData.TargetPrototypeId);
                    if (!effect.TargetData.AllowGhosts) targets = targets.Where(x => !HasComp<GhostComponent>(x));
                    if (!effect.TargetData.ActorControlled) targets = targets.Where(HasComp<ActorComponent>);
                    if (effect.TargetData.Whitelist is not null)
                        targets = targets.Where(x => _whitelist.IsValid(effect.TargetData.Whitelist, x));
                    if (effect.TargetData.SameMap)
                        targets = targets.Where(x => Transform(x).MapUid == comp.ActiveMap);
                    switch (effect.TargetData.GridFilter)
                    {
                        case DestinyDiceGridFilter.None: break;
                        case DestinyDiceGridFilter.SameGrid:
                            targets = targets.Where(x => Transform(x).GridUid == comp.ActiveGrid);
                            break;
                        case DestinyDiceGridFilter.OtherGrids:
                            targets = targets.Where(x => Transform(x).GridUid != comp.ActiveGrid);
                            break;
                        case DestinyDiceGridFilter.NoGrid:
                            targets = targets.Where(x => Transform(x).GridUid is null);
                            break;
                        default:
                            throw new Exception("How. ??????");
                    }

                    if (!effect.TargetData.Range.HasValue) return true;
                    var nearby = _lookup.GetEntitiesInRange(uid, effect.TargetData.Range.Value);
                    targets = targets.Where(x => nearby.Contains(x));
                    return true;
                }
            default:
                throw new Exception("Again, how. ??????");
        }
    }
    // ReSharper restore UseCollectionExpression

    private static DestinyDiceEffectGroup ProtoToGroup(DestinyDiceEffectGroupPrototype prototype) =>
        new()
        {
            RollData = prototype.RollData,
            Conditions = prototype.Conditions,
            AllConditionsMustPass = prototype.AllConditionsMustPass,
            Weight = prototype.Weight,
            Probability = prototype.Probability,
            Delay = prototype.Delay,
            MaxRolls = prototype.MaxRolls,
            MaxTriggers = prototype.MaxTriggers,
            TimesRolled = prototype.TimesRolled,
            TimesTriggered = prototype.TimesTriggered,
            SuccessMessage = prototype.SuccessMessage,
            FailureMessage = prototype.FailureMessage,
            ExhaustedMessage = prototype.ExhaustedMessage
        };

    private static DestinyDiceEffect ProtoToEffect(DestinyDiceEffectPrototype prototype) =>
        new()
        {
            TargetData = prototype.TargetData,
            EntityEffect = prototype.EntityEffect,
            AllConditionsMustPass = prototype.AllConditionsMustPass,
            DependsOnIds = prototype.DependsOnIds,
            Probability = prototype.Probability,
            EffectId = prototype.EffectId,
            Delay = prototype.Delay,
            RequiredTrigger = prototype.RequiredTrigger,
            MaxRolls = prototype.MaxRolls,
            MaxTriggers = prototype.MaxTriggers,
            TimesRolled = prototype.TimesRolled,
            TimesTriggered = prototype.TimesTriggered,
            SuccessMessage = prototype.SuccessMessage,
            FailureMessage = prototype.FailureMessage,
            ExhaustedMessage = prototype.ExhaustedMessage
        };
}
