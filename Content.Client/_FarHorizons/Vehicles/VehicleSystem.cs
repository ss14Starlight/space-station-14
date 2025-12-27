using Content.Shared._FarHorizons.Vehicles;
using Content.Shared._FarHorizons.Vehicles.Components;
using Content.Shared._FarHorizons.VehicleBuckle.Components;
using Content.Shared._FarHorizons.Vehicles.EntitySystems;
using Robust.Client.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;
using Content.Shared.Movement.Events;
using System.Numerics;

namespace Content.Client._FarHorizons.Vehicles;

public sealed class VehicleSystems : SharedVehicleSystems
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleComponent, AppearanceChangeEvent>(OnAppearanceChanged);
        SubscribeLocalEvent<VehicleBuckleComponent, MoveInputEvent>(OnMoveInputEvent);
    }

    private void OnAppearanceChanged(EntityUid uid, VehicleComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;
   
        if (!_sprite.TryGetLayer((uid, args.Sprite), VehicleVisualLayers.AutoAnimate, out var _, false))
            return;

        var state = component.BaseState;

        _sprite.LayerSetRsiState((uid, args.Sprite), VehicleVisualLayers.AutoAnimate, state);

        if(!TryComp<SpriteComponent>(uid, out var spriteComp)) return;

        if (_appearance.TryGetData<bool>(uid, VehicleVisuals.AutoAnimate, out var autoAnimate, args.Component))
            _sprite.LayerSetAutoAnimated((uid, spriteComp), VehicleVisualLayers.AutoAnimate, autoAnimate);
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
                _sprite.LayerSetOffset((ent.Owner, spriteComp), (int)VehicleVisualLayers.AutoAnimate, ent.Comp.NorthOffset);
                break;
            case Direction.South:
                _sprite.SetDrawDepth((ent.Owner, spriteComp), ent.Comp.southDrawDepth);
                _sprite.LayerSetOffset((ent.Owner, spriteComp), (int)VehicleVisualLayers.AutoAnimate, ent.Comp.SouthOffset);
                break;
            case Direction.West:
                _sprite.SetDrawDepth((ent.Owner, spriteComp), ent.Comp.westDrawDepth);
                _sprite.LayerSetOffset((ent.Owner, spriteComp), (int)VehicleVisualLayers.AutoAnimate, ent.Comp.WestOffset);
                break;
            case Direction.East:
                _sprite.SetDrawDepth((ent.Owner, spriteComp), ent.Comp.eastDrawDepth);
                _sprite.LayerSetOffset((ent.Owner, spriteComp), (int)VehicleVisualLayers.AutoAnimate, ent.Comp.EastOffset);
                break;
        }
    }
}
