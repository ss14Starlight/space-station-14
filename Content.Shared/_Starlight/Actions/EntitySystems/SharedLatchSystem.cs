using Content.Shared._Starlight.Actions.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Actions.EntitySystems;

public abstract partial class SharedLatchSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LatchComponent, RefreshMovementSpeedModifiersEvent>(OnLatcherRefreshMovementSpeed);
        SubscribeLocalEvent<LatchedComponent, RefreshMovementSpeedModifiersEvent>(OnTargetRefreshMovementSpeed);
    }

    private void OnLatcherRefreshMovementSpeed(EntityUid uid, LatchComponent comp, RefreshMovementSpeedModifiersEvent ev)
    {
        if (comp.Active)
            ev.ModifySpeed(0f);
    }

    private void OnTargetRefreshMovementSpeed(EntityUid uid, LatchedComponent comp, RefreshMovementSpeedModifiersEvent ev)
    {
        ev.ModifySpeed(0f);
    }
}
