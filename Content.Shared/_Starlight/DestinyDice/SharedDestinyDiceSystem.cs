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
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Sprite;
using Content.Shared.Station;
using Content.Shared.Tabletop;
using Content.Shared.Throwing;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.DestinyDice;

public abstract partial class SharedDestinyDiceSystem : EntitySystem
{
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedCargoSystem _cargo = default!;
    [Dependency] private SharedPuddleSystem _puddle = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private SharedGodmodeSystem _godmode = default!;
    [Dependency] private SharedTabletopSystem _tabletop = default!;
    [Dependency] private SharedExplosionSystem _explosion = default!;
    [Dependency] private SharedAtmosphereSystem _atmos = default!;
    [Dependency] private SharedScaleVisualsSystem _scale = default!;
    [Dependency] private SharedEntityEffectsSystem _effects = default!;
    [Dependency] private SharedEntityConditionsSystem _conditions = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ISharedAdminLogManager _aLog = default!;
    // TODO: nuke system stuff on server

    // Opting to do this instead of entity query enumerator.
    private readonly List<Entity<DestinyDiceComponent>> _activeDice = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DestinyDiceComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<DestinyDiceComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<DestinyDiceComponent, ThrownEvent>(OnThrown);
        SubscribeLocalEvent<DestinyDiceComponent, LandEvent>(OnLand);
        SubscribeLocalEvent<DestinyDiceComponent, DiceRolledEvent>(OnDiceRolled);
    }

    private void AssignActiveValues(EntityUid uid, DestinyDiceComponent comp, EntityUid? roller)
    {
        if (comp.IsActive) return;
        comp.ActiveRoller = roller;
        comp.RollerGrid = roller is not null ? Transform(roller.Value).GridUid : EntityUid.Invalid;
        comp.ActiveGrid = Transform(uid).GridUid;
    }

    private void OnMapInit(Entity<DestinyDiceComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Preset is null) return;
        if (!_proto.TryIndex(ent.Comp.Preset, out var preset)) return;

        ent.Comp.EffectGroups.Clear();
        List<DestinyDiceEffectGroup> groups = [];
        foreach (var groupProtoId in preset.EffectGroupIds)
        {
            if (!_proto.TryIndex(groupProtoId, out var groupProto)) return;
            var group = (DestinyDiceEffectGroup)groupProto.Group.Clone();
            group.Effects.Clear(); // Don't set these manually in the prototype.
            foreach (var effectProtoId in groupProto.EffectIds)
            {
                if (!_proto.TryIndex(effectProtoId, out var effectProto)) return;
                group.Effects.Add(effectProto.Effect);
            }

            groups.Add(group);
        }

        ent.Comp.EffectGroups = groups;
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

        comp.PreviousValue = comp.CurrentValue;
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
            Log.Info("no targets");
            if (comp.NoEffectMessage is not null)
                _popup.PopupPredicted(Loc.GetString(comp.NoEffectMessage), uid, comp.ActiveRoller, comp.NoEffectPopupType);
            return;
        }

        var rolledGroup = _random.PickPredicted(_timing, targetGroups);
        Log.Info("picked target group");
        rolledGroup.TimesRolled++;

        // Check for probability and conditions etc
        if ((rolledGroup.MaxRolls > -1 && rolledGroup.TimesRolled >= rolledGroup.MaxRolls) ||
            (rolledGroup.MaxTriggers > -1 && rolledGroup.TimesTriggered >= rolledGroup.MaxTriggers))
        {
            Log.Info("minmax fail");
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
                        Log.Info("condition fail");
                        if (rolledGroup.FailureMessage is not null)
                            _popup.PopupPredicted(Loc.GetString(rolledGroup.FailureMessage), uid, comp.ActiveRoller, rolledGroup.FailurePopupType);
                        return;
                    }
            }

        if (!_random.ProbPredicted(_timing, rolledGroup.Probability))
        {
            Log.Info("prob fail");
            if (rolledGroup.FailureMessage is not null)
                _popup.PopupPredicted(Loc.GetString(rolledGroup.FailureMessage), uid, comp.ActiveRoller, rolledGroup.FailurePopupType);
            return;
        }

        Log.Info("triggering group");
        rolledGroup.TimesTriggered++;
        comp.CurrentEffectIndex = 0;
        comp.CurrentEffectGroup = rolledGroup;
        comp.NextEffectTriggerTime = _timing.CurTime + TimeSpan.FromSeconds(1) + TimeSpan.FromSeconds(rolledGroup.Delay); // Mandatory one-second delay before executing.
        comp.IsActive = true;

        comp.NextAllowedRollTime = comp.RollDelay.HasValue ? _timing.CurTime + TimeSpan.FromSeconds(comp.RollDelay.Value) : TimeSpan.Zero;

        if (rolledGroup.SuccessMessage is not null)
        {
            Log.Info("popup success message");
            _popup.PopupPredicted(Loc.GetString(rolledGroup.SuccessMessage), uid, comp.ActiveRoller, rolledGroup.SuccessPopupType);
        }
        _activeDice.Add(ent);

        _aLog.Add(LogType.Action, LogImpact.Low, $"Entity {ToPrettyString(uid)} rolled a Destiny Die and triggered an effect group.");
    }

    public override void Update(float delta)
    {
        foreach (var ent in _activeDice.ToList())
        {
            var (uid, comp) = ent;
            var group = comp.CurrentEffectGroup;

            Log.Info($"group: {group}, active: {comp.IsActive}, idx: {comp.CurrentEffectIndex}, count: {group?.Effects.Count}");
            if (group is null || !comp.IsActive || comp.CurrentEffectIndex >= group.Effects.Count)
            {
                _activeDice.Remove(ent);
                comp.IsActive = false;
                comp.CurrentEffectIndex = 0;
                comp.CurrentEffectGroup = null;
                comp.CurrentEffect = null;
                comp.EffectResults.Clear();
                continue;
            }

            if (_timing.CurTime < comp.NextEffectTriggerTime) continue;
            Log.Info("time check pass");

            DestinyDiceEffect? effect = null;
            var earlyFinish = false;
            var foundValidEffect = false;

            // If one effect fails we want to try and start the next one immediately if possible.
            // Basically just going until we find an effect that passes checks or until we exhaust the list.
            while (comp.CurrentEffectIndex < group.Effects.Count)
            {
                effect = group.Effects[comp.CurrentEffectIndex++];
                effect.TimesRolled++;

                if (effect.EntityEffect is null) continue; // Not valid.
                Log.Info("effect not null");

                // Check for probability and conditions etc
                if ((effect.MaxRolls > -1 && effect.TimesRolled >= effect.MaxRolls) ||
                    (effect.MaxTriggers > -1 && effect.TimesTriggered >= effect.MaxTriggers))
                {
                    Log.Info("Max hit");
                    if (effect.ExhaustedMessage is not null)
                        _popup.PopupPredicted(Loc.GetString(effect.ExhaustedMessage), uid, comp.ActiveRoller, effect.ExhaustedPopupType);
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
                                Log.Info("Condition fail");
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
                    Log.Info("Dependency fail");
                    dependencyFail = true;
                    comp.EffectResults.Add(effect, false);
                    if (effect.RequiredTrigger)
                        earlyFinish = true;
                    break;
                }

                if (dependencyFail) break;

                if (!_random.ProbPredicted(_timing, effect.Probability))
                {
                    Log.Info("prob fail");
                    comp.EffectResults.Add(effect, false);
                    if (effect.RequiredTrigger)
                    {
                        earlyFinish = true;
                        break;
                    }
                    continue;
                }

                foundValidEffect = true;
                Log.Info("found effect");
                break;
            }

            Log.Info($"ef: {earlyFinish}, fve: {foundValidEffect}");
            if (earlyFinish || !foundValidEffect || effect?.EntityEffect is null)
            {
                if (effect?.FailureMessage is not null)
                    _popup.PopupPredicted(Loc.GetString(effect.FailureMessage), uid, comp.ActiveRoller, effect.FailurePopupType);
                comp.IsActive = false;
                _activeDice.Remove(ent);
                continue;
            }

            effect.TimesTriggered++;
            comp.EffectResults.Add(effect, true);
            comp.CurrentEffect = effect;
            comp.NextEffectTriggerTime = _timing.CurTime + TimeSpan.FromSeconds(effect.Delay);
            // Effect is applied unconditionally here as effects are checked manually earlier.
            Log.Info("Attempting to do effect.");
            _effects.ApplyEffect(uid, effect.EntityEffect, user: uid);
        }
    }
}
