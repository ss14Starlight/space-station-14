using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Timing;
#region Starlight
using Content.Shared.Body.Systems;
#endregion

namespace Content.Shared.Traits.Assorted;

public sealed partial class LegsParalyzedSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movementSpeedModifierSystem = default!;
    [Dependency] private StandingStateSystem _standingSystem = default!;
    [Dependency] private SharedBodySystem _bodySystem = default!; // Starlight: Reverted NuBody
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LegsParalyzedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LegsParalyzedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LegsParalyzedComponent, BuckledEvent>(OnBuckled);
        SubscribeLocalEvent<LegsParalyzedComponent, UnbuckledEvent>(OnUnbuckled);
        SubscribeLocalEvent<LegsParalyzedComponent, ThrowPushbackAttemptEvent>(OnThrowPushbackAttempt);
        SubscribeLocalEvent<LegsParalyzedComponent, UpdateCanMoveEvent>(OnUpdateCanMoveEvent);
    }

    private void OnStartup(EntityUid uid, LegsParalyzedComponent component, ComponentStartup args)
    {
        if (_timing.ApplyingState)
            return;

        if (component.AllowCrawling)
        {
            if (!_buckle.IsBuckled(uid))
                StartCrawling(uid);
            return;
        }

        // TODO: In future probably must be surgery related wound
        _movementSpeedModifierSystem.ChangeBaseSpeed(uid, 0, 0, 20);
    }

    private void OnShutdown(EntityUid uid, LegsParalyzedComponent component, ComponentShutdown args)
    {
        if (_timing.ApplyingState || TerminatingOrDeleted(uid))
            return;

        if (component.AllowCrawling)
            StopCrawling(uid);

        _standingSystem.Stand(uid);
        _bodySystem.UpdateMovementSpeed(uid); // Starlight: Reverted NuBody
    }

    private void OnBuckled(EntityUid uid, LegsParalyzedComponent component, ref BuckledEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (component.AllowCrawling)
            StopCrawling(uid);

        _standingSystem.Stand(uid);
    }

    private void OnUnbuckled(EntityUid uid, LegsParalyzedComponent component, ref UnbuckledEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (component.AllowCrawling)
            StartCrawling(uid);
        else
            _standingSystem.Down(uid);
    }

    private void OnUpdateCanMoveEvent(EntityUid uid, LegsParalyzedComponent component, UpdateCanMoveEvent args)
    {
        if (!component.AllowCrawling)
            args.Cancel();
    }

    private void OnThrowPushbackAttempt(EntityUid uid, LegsParalyzedComponent component, ThrowPushbackAttemptEvent args)
    {
        if (!component.AllowCrawling)
            args.Cancel();
    }

    private void StartCrawling(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return;

        EnsureComp<WormComponent>(uid);
        EnsureComp<KnockedDownComponent>(uid);
    }

    private void StopCrawling(EntityUid uid)
    {
        if (!RemComp<WormComponent>(uid))
            return;

        RemComp<KnockedDownComponent>(uid);
    }
}
