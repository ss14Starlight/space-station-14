using System.Linq;
using System.Numerics;
using Content.Shared._Funkystation.Footprints;
using Content.Shared._Starlight.ReagentDraw.Components;
using Content.Shared._Starlight.ReagentDraw.EntitySystems;
using Content.Shared._Starlight.Vehicle;
using Content.Shared._FarHorizons.Tools.FloorBuffer.Components;
using Content.Shared.Actions;
using Content.Shared.Audio;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Decals;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems;
using Content.Shared.Toggleable;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Tools.FloorBuffer.Systems;

public sealed partial class FloorBufferSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedDecalSystem _decals = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedReagentDrawSystem _reagents = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private VehicleSystem _vehicles = default!;
    [Dependency] private INetManager _net = default!;
    private static readonly ProtoId<ReagentPrototype> _water = "Water";
    private static readonly FixedPoint2 _bufferWaterDraw = 1;

    [SubscribeLocalEvent]
    private void OnGetActions(EntityUid owner, FloorBufferComponent component, GetItemActionsEvent args)
    {
        var ent = new Entity<FloorBufferComponent>(owner, component);
        args.AddAction(ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnDriverChanged(Entity<FloorBufferComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (args.OldOperator != null)
        {
            SetEnabled(ent, false, ent.Owner);
            if (!_net.IsClient)
                _actions.RemoveAction(ent.Comp.ToggleActionEntity);
        }

        if (!_net.IsClient && args.NewOperator is { } driver)
        {
            _actions.AddAction(driver, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction, ent.Owner);
            Dirty(ent);
        }
    }

    [SubscribeLocalEvent]
    private void OnToggle(EntityUid owner, FloorBufferComponent component, ToggleActionEvent args)
    {
        var ent = new Entity<FloorBufferComponent>(owner, component);
        if (args.Handled)
            return;

        var driving = _vehicles.GetOccupiedVehicle(args.Performer) == ent.Owner;
        if (!driving && !_hands.IsHolding(args.Performer, ent.Owner))
            return;

        if (ent.Comp.Enabled || CanStart(ent))
            SetEnabled(ent, !ent.Comp.Enabled, driving ? ent.Owner : args.Performer);

        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnWaterEmpty(Entity<FloorBufferComponent> ent, ref ReagentContainerSlotEmptyEvent args)
        => SetEnabled(ent, false, GetMovingEntity(ent));

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<FloorBufferComponent> ent, ref ComponentShutdown args)
        => ent.Comp.SoundStream = _audio.Stop(ent.Comp.SoundStream);

    [SubscribeLocalEvent]
    private void OnIgnitionChanged(Entity<FloorBufferComponent> ent, ref VehicleIgnitionChangedEvent args)
    {
        if (!args.Started)
            SetEnabled(ent, false, GetMovingEntity(ent));
    }

    [SubscribeLocalEvent]
    private void OnReagentDraw(Entity<FloorBufferComponent> ent, ref BeforeReagentDrawEvent args)
    {
        if (!ent.Comp.Enabled || !HasComp<VehicleIgnitionComponent>(ent))
            return;

        var combinedDrain = args.DrainAmount + _bufferWaterDraw;
        if (args.AvailableVolume < combinedDrain)
        {
            SetEnabled(ent, false, GetMovingEntity(ent));
            return;
        }

        args.DrainAmount = combinedDrain;
    }

    private bool CanStart(Entity<FloorBufferComponent> ent)
    {
        if (!TryComp<VehicleIgnitionComponent>(ent, out var ignition))
            return _reagents.HasDrawReagent(ent.Owner);

        return ignition.Started && _reagents.HasDrawReagent(ent.Owner, _bufferWaterDraw);
    }

    private EntityUid GetMovingEntity(EntityUid buffer)
    {
        var parent = Transform(buffer).ParentUid;
        return _hands.IsHolding(parent, buffer) ? parent : buffer;
    }

    private void SetEnabled(Entity<FloorBufferComponent> ent, bool enabled, EntityUid movingEntity)
    {
        if (ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        Dirty(ent);
        if (!HasComp<VehicleIgnitionComponent>(ent) && TryComp<ReagentDrawComponent>(ent, out var draw))
            _reagents.SetDrawEnabled((ent.Owner, draw), enabled);
        _actions.SetToggled(ent.Comp.ToggleActionEntity, enabled);
        if (ent.Comp.ActiveSound is { } sound)
        {
            if (_net.IsServer)
            {
                ent.Comp.SoundStream = enabled
                    ? _audio.PlayPvs(sound, ent.Owner, sound.Params.WithLoop(true))?.Entity
                    : _audio.Stop(ent.Comp.SoundStream);
            }
        }
        else
            _ambient.SetAmbience(ent, enabled);

        _speed.RefreshMovementSpeedModifiers(movingEntity);
    }

    [SubscribeLocalEvent]
    private void OnHeldRefresh(Entity<FloorBufferComponent> ent, ref HeldRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        if (ent.Comp.Enabled)
            args.Args.ModifySpeed(ent.Comp.SpeedReduction);
    }

    [SubscribeLocalEvent]
    private void OnRefresh(EntityUid owner, FloorBufferComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        var ent = new Entity<FloorBufferComponent>(owner, component);
        if (ent.Comp.Enabled)
            args.ModifySpeed(ent.Comp.SpeedReduction);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<FloorBufferComponent>();
        while (query.MoveNext(out var uid, out var buffer))
        {
            if (!buffer.Enabled)
                continue;

            var moving = GetMovingEntity(uid);
            if (!TryComp<PhysicsComponent>(moving, out var body) || body.BodyStatus == BodyStatus.InAir ||
                (body.LinearVelocity == Vector2.Zero && body.AngularVelocity == 0f))
                continue;

            var xform = Transform(moving);
            if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            var tile = _map.GetTileRef(gridUid, grid, xform.Coordinates);
            if (TryComp<DecalGridComponent>(gridUid, out var decalGrid))
            {
                foreach (var decal in _decals.GetDecalsInRange(gridUid, xform.Coordinates.Position).ToArray())
                {
                    if (decal.Decal.Cleanable)
                        _decals.RemoveDecal(gridUid, decal.Index, decalGrid);
                }
            }

            foreach (var target in _lookup.GetLocalEntitiesIntersecting(tile, 0f).ToArray())
            {
                if (HasComp<FootprintComponent>(target))
                {
                    QueueDel(target);
                    continue;
                }

                if (!TryComp<PuddleComponent>(target, out var comp) ||
                    !_solutions.TryGetSolution(target, comp.SolutionName, out var entity, out var solution))
                    continue;

                var removed = _solutions.SplitSolutionWithout(entity.Value, solution.Volume * 0.05, _water);
                if (removed.Volume > 0)
                    _solutions.TryAddSolution(entity.Value, new Solution(_water, removed.Volume));
            }
        }
    }
}
