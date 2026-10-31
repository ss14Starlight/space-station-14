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
using Content.Shared.Ghost;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
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
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.DestinyDice;

public sealed partial class DestinyDiceSystem : EntitySystem
{
    [Dependency] private SharedChatSystem _chat = null!;
    [Dependency] private SharedItemSystem _item = null!;
    [Dependency] private SharedPopupSystem _popup = null!;
    [Dependency] private SharedCargoSystem _cargo = null!;
    [Dependency] private SharedPuddleSystem _puddle = null!;
    [Dependency] private EntityLookupSystem _lookup = null!;
    [Dependency] private SharedStationSystem _station = null!;
    [Dependency] private SharedGodmodeSystem _godmode = null!;
    [Dependency] private SharedTabletopSystem _tabletop = null!;
    [Dependency] private SharedExplosionSystem _explosion = null!;
    [Dependency] private EntityWhitelistSystem _whitelist = null!;
    [Dependency] private SharedAtmosphereSystem _atmos = null!;
    [Dependency] private SharedScaleVisualsSystem _scale = null!;
    [Dependency] private SharedEntityEffectsSystem _effects = null!;
    [Dependency] private SharedEntityConditionsSystem _conditions = null!;
    [Dependency] private SharedSolutionContainerSystem _solution = null!;
    [Dependency] private INetManager _net = null!;
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private IRobustRandom _random = null!;
    [Dependency] private IPrototypeManager _proto = null!;
    [Dependency] private ISharedAdminLogManager _aLog = null!;

    [SubscribeLocalEvent]
    private void OnComponentStartup(Entity<DestinyDiceComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.PresetAdded || ent.Comp.Preset is null) return;
        if (!_proto.TryIndex(ent.Comp.Preset, out var preset)) return;

        List<DestinyDiceEffectGroup> groups = [];
        foreach (var groupProtoId in preset.Groups)
        {
            if (!_proto.TryIndex(groupProtoId, out var groupProto)) return;
            var group = ProtoToGroup(groupProto);
            foreach (var effectProtoId in groupProto.Effects)
            {
                if (!_proto.TryIndex(effectProtoId, out var effectProto)) return;
                group.Effects.Add(ProtoToEffect(effectProto));
            }

            groups.Add(group);
        }

        ent.Comp.EffectGroups.AddRange(groups);
        ent.Comp.PresetAdded = true;
    }

    [SubscribeLocalEvent]
    private void OnUseInHand(Entity<DestinyDiceComponent> ent, ref UseInHandEvent args) =>
        AssignActiveValues(ent, ent, args.User);

    [SubscribeLocalEvent]
    private void OnThrown(Entity<DestinyDiceComponent> ent, ref ThrownEvent args)
    {
        if (ent.Comp.IsActive) return;
        ent.Comp.ActiveRoller = args.User;
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnLand(Entity<DestinyDiceComponent> ent, ref LandEvent args) =>
        AssignActiveValues(ent, ent, args.User);

    [SubscribeLocalEvent]
    private void OnExecuteEffect(Entity<DestinyDiceComponent> ent, ref DestinyDiceExecuteEffectEvent args)
    {
        if (args.Effect.EntityEffect is null) return;
        var (uid, comp) = ent;
        if (args.Effect.SuccessMessage is not null)
            _popup.PopupPredicted(Loc.GetString(args.Effect.SuccessMessage), uid, comp.ActiveRoller,
                args.Effect.SuccessPopupType);
        // Effect is applied unconditionally here as effects are checked manually earlier.
        _effects.ApplyEffect(uid, args.Effect.EntityEffect, user: uid);
    }

    [SubscribeLocalEvent]
    private void OnEffectEnd(Entity<DestinyDiceComponent> ent, ref DestinyDiceEffectEndEvent args)
    {
        ent.Comp.WaitingForEffectEnd = false;
        Dirty(ent);
    }

    [SubscribeLocalEvent]
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
                _popup.PopupPredicted(Loc.GetString(comp.CooldownMessage), uid, comp.ActiveRoller,
                    comp.CooldownPopupType);
            return;
        }

        comp.CurrentValue = args.Value;
        Dirty(ent);

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
                _popup.PopupPredicted(Loc.GetString(comp.NoEffectMessage), uid, comp.ActiveRoller,
                    comp.NoEffectPopupType);
            return;
        }

        var rolledGroup = _random.PickPredicted(_timing, targetGroups);
        var groupIndex = comp.EffectGroups.IndexOf(rolledGroup);
        var groupTimesRolled = Increment(comp.GroupTimesRolled, groupIndex, rolledGroup.TimesRolled);
        var groupTimesTriggered = comp.GroupTimesTriggered.GetValueOrDefault(groupIndex, rolledGroup.TimesTriggered);

        // Check for probability and conditions etc
        if ((rolledGroup.MaxRolls > -1 && groupTimesRolled > rolledGroup.MaxRolls) ||
            (rolledGroup.MaxTriggers > -1 && groupTimesTriggered >= rolledGroup.MaxTriggers))
        {
            if (rolledGroup.ExhaustedMessage is not null)
                _popup.PopupPredicted(Loc.GetString(rolledGroup.ExhaustedMessage), uid, comp.ActiveRoller,
                    rolledGroup.ExhaustedPopupType);
            return;
        }

        if (rolledGroup.Conditions.Count > 0)
            switch (rolledGroup.AllConditionsMustPass)
            {
                case true when !_conditions.TryConditions(uid, rolledGroup.Conditions.ToArray()):
                case false when !_conditions.TryAnyCondition(uid, rolledGroup.Conditions.ToArray()):
                    {
                        if (rolledGroup.FailureMessage is not null)
                            _popup.PopupPredicted(Loc.GetString(rolledGroup.FailureMessage), uid, comp.ActiveRoller,
                                rolledGroup.FailurePopupType);
                        return;
                    }
            }

        if (!_random.ProbPredicted(_timing, rolledGroup.Probability))
        {
            if (rolledGroup.FailureMessage is not null)
                _popup.PopupPredicted(Loc.GetString(rolledGroup.FailureMessage), uid, comp.ActiveRoller,
                    rolledGroup.FailurePopupType);
            return;
        }

        Increment(comp.GroupTimesTriggered, groupIndex, rolledGroup.TimesTriggered);
        comp.CurrentGroupIndex = groupIndex;
        comp.CurrentEffectIndex = -1;
        comp.NextEffectIndex = 0;
        comp.EffectResults.Clear();
        comp.WaitingForEffectEnd = false;
        comp.IsActive = true;

        // Always go through pending so the group's success message and delay get applied.
        comp.GroupStartTime = _timing.CurTime + TimeSpan.FromSeconds(comp.LandDelay + (comp.GroupDelay ?? 0));
        comp.IsPending = true;

        comp.NextAllowedRollTime = comp.RollDelay.HasValue
            ? _timing.CurTime + TimeSpan.FromSeconds(comp.RollDelay.Value)
            : TimeSpan.Zero;

        Dirty(ent);
        _aLog.Add(LogType.Action, LogImpact.Low,
            $"Entity {ToPrettyString(uid)} rolled a Destiny Die and triggered an effect group.");
    }

    public override void Update(float delta)
    {
        var query = EntityQueryEnumerator<DestinyDiceComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsActive) continue;
            ProcessDice((uid, comp));
        }
    }

    private void ProcessDice(Entity<DestinyDiceComponent> ent)
    {
        var (uid, comp) = ent;
        var group = comp.CurrentEffectGroup;

        if (group is null || comp.NextEffectIndex >= group.Effects.Count)
        {
            FinishDice(ent);
            return;
        }

        // Skip if waiting for delay or for event.
        if (_timing.CurTime < comp.GroupStartTime) return;

        if (comp.IsPending)
        {
            comp.NextEffectTriggerTime = _timing.CurTime + TimeSpan.FromSeconds(group.Delay);

            if (group.SuccessMessage is not null)
                _popup.PopupPredicted(Loc.GetString(group.SuccessMessage), uid, comp.ActiveRoller,
                    group.SuccessPopupType);

            comp.IsPending = false;
            Dirty(ent);
        }

        if (_timing.CurTime < comp.NextEffectTriggerTime) return;
        if (comp.WaitingForEffectEnd) return;

        DestinyDiceEffect? effect = null;
        var effectIndex = -1;
        var earlyFinish = false;
        var foundValidEffect = false;

        // If one effect fails we want to try and start the next one immediately if possible.
        // Basically just going until we find an effect that passes checks or until we exhaust the list.

        while (comp.NextEffectIndex < group.Effects.Count)
        {
            effectIndex = comp.NextEffectIndex++;
            effect = group.Effects[effectIndex];
            var key = new DestinyDiceEffectKey(comp.CurrentGroupIndex, effectIndex);
            var effectTimesRolled = Increment(comp.EffectTimesRolled, key, effect.TimesRolled);
            var effectTimesTriggered = comp.EffectTimesTriggered.GetValueOrDefault(key, effect.TimesTriggered);

            if (effect.EntityEffect is null) continue; // Not valid.

            // Check for probability and conditions etc
            if ((effect.MaxRolls > -1 && effectTimesRolled > effect.MaxRolls) ||
                (effect.MaxTriggers > -1 && effectTimesTriggered >= effect.MaxTriggers))
            {
                if (effect.ExhaustedMessage is not null)
                    _popup.PopupPredicted(Loc.GetString(effect.ExhaustedMessage), uid, comp.ActiveRoller,
                        effect.ExhaustedPopupType);
                comp.EffectResults[effectIndex] = false;
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
                            comp.EffectResults[effectIndex] = false;
                            if (effect.RequiredTrigger)
                            {
                                earlyFinish = true;
                                break;
                            }

                            continue;
                        }
                }

            if (earlyFinish) break;

            var dependencyFail = false;
            foreach (var dependencyIndex in effect.DependsOnIds.Select(id => group.Effects.FindIndex(x => x.EffectId == id)).Where(dependencyIndex => dependencyIndex >= 0))
            {
                if (!comp.EffectResults.TryGetValue(dependencyIndex, out var effectResult)) continue;
                if (effectResult) continue;
                dependencyFail = true;
                comp.EffectResults[effectIndex] = false;
                if (effect.RequiredTrigger)
                    earlyFinish = true;
                break;
            }

            if (dependencyFail) break;

            if (!_random.ProbPredicted(_timing, effect.Probability))
            {
                comp.EffectResults[effectIndex] = false;
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

        Dirty(ent);

        if (earlyFinish || !foundValidEffect || effect?.EntityEffect is null)
        {
            if (effect?.FailureMessage is not null)
                _popup.PopupPredicted(Loc.GetString(effect.FailureMessage), uid, comp.ActiveRoller,
                    effect.FailurePopupType);
            FinishDice(ent);
            return;
        }

        Increment(comp.EffectTimesTriggered, new DestinyDiceEffectKey(comp.CurrentGroupIndex, effectIndex),
            effect.TimesTriggered);
        comp.CurrentEffectIndex = effectIndex;
        comp.EffectResults[effectIndex] = true;
        if (effect.EndOnEvent) comp.WaitingForEffectEnd = true;
        else comp.NextEffectTriggerTime = _timing.CurTime + TimeSpan.FromSeconds(effect.Delay);
        RaiseLocalEvent(uid, new DestinyDiceExecuteEffectEvent(effect));
    }

    private void FinishDice(Entity<DestinyDiceComponent> ent)
    {
        var comp = ent.Comp;
        comp.IsActive = false;
        comp.IsPending = false;
        comp.WaitingForEffectEnd = false;
        comp.CurrentGroupIndex = -1;
        comp.CurrentEffectIndex = -1;
        comp.NextEffectIndex = 0;
        comp.EffectResults.Clear();
        Dirty(ent);
    }

    private static int Increment<TKey>(Dictionary<TKey, int> counters, TKey key, int initial) where TKey : notnull =>
        counters[key] = counters.GetValueOrDefault(key, initial) + 1;

    /// <summary>
    /// Quick helper to assign active values to the component for the current effect to reference.
    /// </summary>
    private void AssignActiveValues(EntityUid uid, DestinyDiceComponent comp, EntityUid? roller)
    {
        if (comp.IsActive) return;
        comp.ActiveRoller = roller;
        comp.RollerGrid = roller is not null ? Transform(roller.Value).GridUid : EntityUid.Invalid;
        comp.ActiveGrid = Transform(uid).GridUid;
        comp.ActiveMap = Transform(uid).MapUid;
        Dirty(uid, comp);
    }

    // ReSharper disable UseCollectionExpression - Sandboxing moment.
    /// <summary>
    /// Gets the effect targets for the current DD effect on the die.
    /// If you are using this from an entity effect, it should correspond to the DD effect that triggered it.
    /// </summary>
    /// <param name="ent">The destiny die entity.</param>
    /// <param name="targets">An <see cref="IEnumerable{EntityUid}"/> containing the target entities.</param>
    /// <returns>Boolean value based on if there are valid targets or not.</returns>
    public bool GetEffectTargets(Entity<DestinyDiceComponent> ent,
        [NotNullWhen(true)] out IEnumerable<EntityUid>? targets)
    {
        var (uid, comp) = ent;
        targets = null;

        switch (comp.CurrentEffect?.TargetData.TargetType)
        {
            case DestinyDiceTargetType.None:
                return false;
            case DestinyDiceTargetType.Self:
                targets = new[] { uid };
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
