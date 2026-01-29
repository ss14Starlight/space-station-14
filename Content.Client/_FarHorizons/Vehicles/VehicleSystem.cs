using Content.Shared._FarHorizons.Vehicles;
using Content.Shared._FarHorizons.Vehicles.Components;
using Content.Shared._FarHorizons.Vehicles.EntitySystems;
using Robust.Client.GameObjects;
using Content.Shared.Movement.Events;
using Content.Shared.Buckle.Components;

namespace Content.Client._FarHorizons.Vehicles;

public sealed class VehicleSystems : SharedVehicleSystems
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleComponent, AppearanceChangeEvent>(OnAppearanceChanged);
        SubscribeLocalEvent<VehicleBuckleComponent, MoveInputEvent>(OnMoveInputEvent);
        SubscribeLocalEvent<VehicleBuckleComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<VehicleComponent>();
        while (query.MoveNext(out var uid, out var vehicle))
        {
            if(vehicle.Headlight == null && vehicle.Sirenlight == null)
                continue;
            if(vehicle.Headlight != null && TryComp<SpriteComponent>(vehicle.Headlight, out var headlightSprite) &&
                headlightSprite.Visible)
                _sprite.SetVisible((vehicle.Headlight.Value, headlightSprite), false);
            if(vehicle.Sirenlight != null && TryComp<SpriteComponent>(vehicle.Sirenlight, out var sirelightSprite) &&
                sirelightSprite.Visible)
                _sprite.SetVisible((vehicle.Sirenlight.Value, sirelightSprite), false);
        }
    }

    private void OnAppearanceChanged(EntityUid uid, VehicleComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!args.AppearanceData.TryGetValue(VehicleVisuals.VisualState, out var visualStateObject) ||
            visualStateObject is not VehicleVisualState visualState)
        {
            visualState = VehicleVisualState.Normal;
        }
        UpdateAppearance(uid, visualState, component, args.Sprite);
    }

    private void OnUnstrapAttempt(Entity<VehicleBuckleComponent> ent, ref UnstrapAttemptEvent args)
    {
        if(!TryComp<VehicleComponent>(ent.Owner, out var vehicleComp)) return;
        if(args.User == null || !args.Popup) return;
        if(vehicleComp.Rider == null) return;
        if (vehicleComp.Rider != args.User)
            args.Cancelled = true;
    }

    private void UpdateAppearance(EntityUid uid, VehicleVisualState visualState, VehicleComponent component, SpriteComponent sprite)
    {        
        switch (visualState)
        {
            case VehicleVisualState.Normal:
                SetLayerState(VehicleVisualLayers.Base, component.BaseState, (uid, sprite));
                _sprite.LayerSetAnimationTime((uid, sprite), 0, 0f);
                break;

            case VehicleVisualState.Moving:
                _sprite.LayerSetAutoAnimated((uid, sprite), VehicleVisualLayers.Base, true);
                break;

            case VehicleVisualState.Broken:
                SetLayerState(VehicleVisualLayers.Base, component.BrokenState, (uid, sprite));
                break;
        }
    }

    private void SetLayerState(VehicleVisualLayers layer, string? state, Entity<SpriteComponent> sprite)
    {
        if (string.IsNullOrEmpty(state))
            return;

        _sprite.LayerSetAutoAnimated(sprite.AsNullable(), layer, false);
        _sprite.LayerSetRsiState(sprite.AsNullable(), layer, state);
    }
    
    private void OnMoveInputEvent(Entity<VehicleBuckleComponent> ent, ref MoveInputEvent args)
    {
        if(!TryComp<SpriteComponent>(ent.Owner, out var spriteComp)) return;
        if(!TryComp<VehicleComponent>(ent.Owner, out var vehicleComp)) return;
        if(!vehicleComp.Started && vehicleComp.requireIgnition) return;
        if(args.Dir == Direction.Invalid) return;
        if(args.Dir == vehicleComp.currentDirection) return;
        vehicleComp.currentDirection = args.Dir;
        Dirty(ent.Owner, vehicleComp);

        switch(args.Dir)
        {
            case Direction.North:
                _sprite.SetDrawDepth((ent.Owner, spriteComp), ent.Comp.northDrawDepth);
                _sprite.LayerSetOffset((ent.Owner, spriteComp), (int)VehicleVisuals.VisualState, ent.Comp.NorthOffset);
                break;
            case Direction.South:
                _sprite.SetDrawDepth((ent.Owner, spriteComp), ent.Comp.southDrawDepth);
                _sprite.LayerSetOffset((ent.Owner, spriteComp), (int)VehicleVisuals.VisualState, ent.Comp.SouthOffset);
                break;
            case Direction.West:
                _sprite.SetDrawDepth((ent.Owner, spriteComp), ent.Comp.westDrawDepth);
                _sprite.LayerSetOffset((ent.Owner, spriteComp), (int)VehicleVisuals.VisualState, ent.Comp.WestOffset);
                break;
            case Direction.East:
                _sprite.SetDrawDepth((ent.Owner, spriteComp), ent.Comp.eastDrawDepth);
                _sprite.LayerSetOffset((ent.Owner, spriteComp), (int)VehicleVisuals.VisualState, ent.Comp.EastOffset);
                break;
        }
    }
}
