using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Content.Shared.Stunnable; //Starlight
using Content.Shared.Movement.Components; //Starlight

namespace Content.Shared.Traits.Assorted;

public sealed class LegsParalyzedSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifierSystem = default!;
    [Dependency] private readonly StandingStateSystem _standingSystem = default!;
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LegsParalyzedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LegsParalyzedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LegsParalyzedComponent, BuckledEvent>(OnBuckled);
        SubscribeLocalEvent<LegsParalyzedComponent, UnbuckledEvent>(OnUnbuckled);
        //SubscribeLocalEvent<LegsParalyzedComponent, ThrowPushbackAttemptEvent>(OnThrowPushbackAttempt); //Starlight
        //SubscribeLocalEvent<LegsParalyzedComponent, UpdateCanMoveEvent>(OnUpdateCanMoveEvent); //Starlight
    }

    private void OnStartup(EntityUid uid, LegsParalyzedComponent component, ComponentStartup args)
    {
        // TODO: In future probably must be surgery related wound
        _movementSpeedModifierSystem.ChangeBaseSpeed(uid, 1, 1, 15); //Starlight
		EnsureComp<WormComponent>(uid); //Starlight
        EnsureComp<KnockedDownComponent>(uid); //Starlight
    }

    private void OnShutdown(EntityUid uid, LegsParalyzedComponent component, ComponentShutdown args)
    {
        //_standingSystem.Stand(uid); //Starlight
        //_bodySystem.UpdateMovementSpeed(uid); //Starlight
		RemComp<WormComponent>(uid); //Starlight
        RemComp<KnockedDownComponent>(uid); //Starlight
    }

    private void OnBuckled(EntityUid uid, LegsParalyzedComponent component, ref BuckledEvent args)
    {
        //_standingSystem.Stand(uid); //Starlight
		RemComp<WormComponent>(uid); //Starlight
        RemComp<KnockedDownComponent>(uid); //Starlight
    }

    private void OnUnbuckled(EntityUid uid, LegsParalyzedComponent component, ref UnbuckledEvent args)
    {
        //_standingSystem.Down(uid); //Starlight
		EnsureComp<WormComponent>(uid); //Starlight
        EnsureComp<KnockedDownComponent>(uid); //Starlight
    }
//starlight commented out
    /*private void OnUpdateCanMoveEvent(EntityUid uid, LegsParalyzedComponent component, UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnThrowPushbackAttempt(EntityUid uid, LegsParalyzedComponent component, ThrowPushbackAttemptEvent args)
    {
        args.Cancel();
    }*/
}
