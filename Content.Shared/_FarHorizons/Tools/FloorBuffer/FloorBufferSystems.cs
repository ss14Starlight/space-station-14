using Content.Shared._FarHorizons.Tools.FloorBuffer.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Decals;
using Content.Shared.Fluids.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Components;
using System.Linq;
using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Toggleable;
using Content.Shared.Movement.Systems;
using Content.Shared.Hands;
using Content.Shared.Audio;
using Content.Shared._FarHorizons.ReagentDraw.EntitySystems;
using Content.Shared._FarHorizons.ReagentDraw.Components;

namespace Content.Shared._FarHorizons.Tools.FloorBuffer.Systems;

public sealed partial class FloorBufferSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedDecalSystem _decals = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedReagentDrawSystem _ReagentDraw = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    static readonly public ProtoId<ReagentPrototype> ReplacementReagent = "Water";
    public override void Initialize()
    {
        SubscribeLocalEvent<FloorBufferComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FloorBufferComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<FloorBufferComponent, HeldRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnMovementRefreshHeld);
        SubscribeLocalEvent<FloorBufferComponent, RefreshMovementSpeedModifiersEvent>(OnMovementRefresh);
        SubscribeLocalEvent<FloorBufferComponent, ToggleActionEvent>(OnToggleAction);
        base.Initialize();
    }
    private void OnMapInit(Entity<FloorBufferComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction, ent.Owner);
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnGetActions(EntityUid ent, FloorBufferComponent component, GetItemActionsEvent args)
    {
        args.AddAction(ref component.ToggleActionEntity, component.ToggleAction);
        Dirty(ent, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        
        var query = EntityQueryEnumerator<FloorBufferComponent, TransformComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var floorComp, out var xForm, out var Phys))
        {
            if (!floorComp.Enabled)
                continue;

            var moveTarget = uid;
            if(xForm.ParentUid != xForm.GridUid && TryComp<PhysicsComponent>(xForm.ParentUid, out var Phys2))
            {
                moveTarget = xForm.ParentUid;
                xForm = Transform(xForm.ParentUid);
                Phys = Phys2;
            }

            if(!TryComp<MapGridComponent>(xForm.GridUid, out var grid))
                continue;
                        
            if(TryComp<ReagentDrawComponent>(uid, out var rdComp) && !_ReagentDraw.HasDrawReagant(uid))
            {
                floorComp.Enabled = false;
                rdComp.Enabled = false;
                _ambient.SetAmbience(uid, false);
                Dirty(uid, floorComp);
                Dirty(uid, rdComp);
                _movementSpeed.RefreshMovementSpeedModifiers(moveTarget);
            }

            if ((Phys.LinearVelocity.Equals(Vector2.Zero) && Phys.AngularVelocity.Equals(0f)) || Phys.BodyStatus == BodyStatus.InAir)
                continue;

            var tile = _map.GetTileRef(xForm.GridUid.Value, grid, xForm.Coordinates);
            CleanDecalssandPuddles(moveTarget,tile, grid);
        }
    }

    private void OnToggleAction(EntityUid uid, FloorBufferComponent component, ToggleActionEvent args)
    {
        if (args.Handled)
            return;
        
        if(TryComp<ReagentDrawComponent>(uid, out var rdComp))
        {
            rdComp.Enabled = !rdComp.Enabled;
            Dirty(uid, rdComp);
        }

        component.Enabled = !component.Enabled;
        if(args.Performer == Transform(uid).ParentUid)
            _movementSpeed.RefreshMovementSpeedModifiers(args.Performer);
        else
            _movementSpeed.RefreshMovementSpeedModifiers(uid);
        _ambient.SetAmbience(uid, component.Enabled);
        Dirty(uid, component);
        args.Handled = true;
    }

    private void OnMovementRefreshHeld(Entity<FloorBufferComponent> ent, ref HeldRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        if(ent.Comp.Enabled)
            args.Args.ModifySpeed(ent.Comp.SpeedReduction);
    }

    private void OnMovementRefresh(Entity<FloorBufferComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if(ent.Comp.Enabled)
            args.ModifySpeed(ent.Comp.SpeedReduction);
    }

    private void CleanDecalssandPuddles(EntityUid uid, TileRef tile, MapGridComponent grid)
    {
        if(TryComp<DecalGridComponent>(tile.GridUid, out var decalGrid))
        {
            var decals = _decals.GetDecalsInRange(tile.GridUid, Transform(uid).Coordinates.Position);
            foreach(var decal in decals)
            {
                if(!decal.Decal.Cleanable)
                    continue;

                _decals.RemoveDecal(tile.GridUid, decal.Index, decalGrid);
            }
        }
        var entities = _lookup.GetLocalEntitiesIntersecting(tile, 0f).ToArray();
        foreach(var entity in entities)
        {
            if(!TryComp<PuddleComponent>(entity, out var puddleComp) 
                || !_solutionContainer.TryGetSolution(entity, puddleComp.SolutionName, out var solutionComp, out var solution))
                continue;
            
            var replaceTotal = _solutionContainer.SplitSolutionWithout(solutionComp.Value, solution.Volume*0.05, ReplacementReagent);
            _solutionContainer.TryAddSolution(solutionComp.Value, new Solution(ReplacementReagent, replaceTotal.Volume));
        }
    }
}