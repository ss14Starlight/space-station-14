using Content.Shared._FarHorizons.Vehicles;
using Content.Shared._FarHorizons.Vehicles.Components;
using Robust.Client.GameObjects;
using Content.Shared.Buckle.Components;
using Robust.Client.Graphics;

namespace Content.Client._FarHorizons.Vehicles;

public sealed class VehicleSystems : SharedVehicleSystems
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleComponent, AppearanceChangeEvent>(OnAppearanceChanged);
        SubscribeLocalEvent<VehicleBuckleComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
        _transform.OnGlobalMoveEvent += OnMoveEvent;
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

    private void OnMoveEvent(ref MoveEvent ev)
    {
        var target = ev.Entity;
        if(!TryComp<VehicleBuckleComponent>(target, out var vbComp)
            || !TryComp<SpriteComponent>(target, out var spriteComp)
            || !TryComp<VehicleComponent>(target, out var vehicleComp)
            || (!vehicleComp.Started && vehicleComp.RequireIgnition)) return;

        var rotation = Transform(target.Owner).LocalRotation + (_eye.CurrentEye.Rotation - (Transform(target.Owner).LocalRotation - _transform.GetWorldRotation(target.Owner)));
        var direction = rotation.GetDir();
        switch(direction)
        {
            case Direction.North:
                _sprite.SetDrawDepth((target, spriteComp), vbComp.northDrawDepth);
                _sprite.SetOffset((target, spriteComp), vbComp.NorthOffset);
                break;
            case Direction.South:
                _sprite.SetDrawDepth((target, spriteComp), vbComp.southDrawDepth);
                _sprite.SetOffset((target, spriteComp), vbComp.SouthOffset);
                break;
            case Direction.West:
                _sprite.SetDrawDepth((target, spriteComp), vbComp.westDrawDepth);
                _sprite.SetOffset((target, spriteComp), vbComp.WestOffset);
                break;
            case Direction.East:
                _sprite.SetDrawDepth((target, spriteComp), vbComp.eastDrawDepth);
                _sprite.SetOffset((target, spriteComp), vbComp.EastOffset);
                break;
            case Direction.NorthWest:
                _sprite.SetDrawDepth((target, spriteComp), vbComp.westDrawDepth);
                _sprite.SetOffset((target, spriteComp), vbComp.WestOffset);
                break;
            case Direction.NorthEast:
                _sprite.SetDrawDepth((target, spriteComp), vbComp.eastDrawDepth);
                _sprite.SetOffset((target, spriteComp), vbComp.EastOffset);
                break;
            case Direction.SouthWest:
                _sprite.SetDrawDepth((target, spriteComp), vbComp.westDrawDepth);
                _sprite.SetOffset((target, spriteComp), vbComp.WestOffset);
                break;
            case Direction.SouthEast:
                _sprite.SetDrawDepth((target, spriteComp), vbComp.eastDrawDepth);
                _sprite.SetOffset((target, spriteComp), vbComp.EastOffset);
                break;
        }
    }
}
