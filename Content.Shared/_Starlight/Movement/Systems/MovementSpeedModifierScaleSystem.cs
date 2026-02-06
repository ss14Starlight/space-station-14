using Content.Shared._Starlight.Movement.Components;
using Content.Shared._Starlight.Movement.Events;

namespace Content.Shared._Starlight.Movement.Systems;

public sealed class MovementSpeedModifierScaleSystem : EntitySystem
{

    public override void Initialize()
        => SubscribeLocalEvent<MovementSpeedModifierScaleComponent, ApplyMovementScaleModifierEvent>(OnRefreshMovement);

    private void OnRefreshMovement(EntityUid uid, MovementSpeedModifierScaleComponent comp, ref ApplyMovementScaleModifierEvent args)
    {
        args.ChangedWalkSpeedModifier = ((args.OriginalWalkSpeedModifier - 1) * comp.MovementSpeedScale) + 1;
        args.ChangedSprintSpeedModifier = ((args.OriginalSprintSpeedModifier - 1) * comp.MovementSpeedScale) + 1;
    }
}