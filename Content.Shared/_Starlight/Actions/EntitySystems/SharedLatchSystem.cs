using Content.Shared._Starlight.Actions.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.Containers;
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

        SubscribeLocalEvent<LatchComponent, AttackAttemptEvent>(OnLatcherAttackAttempt);
        SubscribeLocalEvent<LatchBlockedHandComponent, ContainerGettingRemovedAttemptEvent>(OnBlockedHandRemoveAttempt);
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

    /// <summary>
    /// Blocks manual attacks while latched, so Bite Harder is the only option.
    /// </summary>
    private void OnLatcherAttackAttempt(EntityUid uid, LatchComponent comp, AttackAttemptEvent ev)
    {
        if (comp.Active)
            ev.Cancel();
    }

    /// <summary>
    /// The latch's blocked hand can't be dropped via the drop key.
    /// </summary>
    private void OnBlockedHandRemoveAttempt(EntityUid uid, LatchBlockedHandComponent comp, ref ContainerGettingRemovedAttemptEvent args)
    {
        args.Cancel();
    }
}
