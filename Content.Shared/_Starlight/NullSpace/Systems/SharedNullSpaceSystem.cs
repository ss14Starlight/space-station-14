using Content.Shared.Interaction.Events;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Mobs;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Content.Shared.Item;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;

namespace Content.Shared._Starlight.NullSpace;

public abstract partial class SharedNullSpaceSystem : EntitySystem
{
    [Dependency] private readonly PullingSystem _pulling = default!;
    public EntProtoId _shadekinShadow = "ShadekinShadow";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NullSpaceComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<NullSpaceComponent, BeforeThrowEvent>(OnBeforeThrow);
        SubscribeLocalEvent<NullSpaceComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<NullSpaceComponent, ShotAttemptedEvent>(OnShootAttempt);
        SubscribeLocalEvent<NullSpaceComponent, UseAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<NullSpaceComponent, PickupAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<NullSpaceComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<NullSpaceComponent, PreventCollideEvent>(PreventCollision);
    }

    private void OnMobStateChanged(EntityUid uid, NullSpaceComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Critical || args.NewMobState == MobState.Dead)
        {
            SpawnAtPosition(_shadekinShadow, Transform(uid).Coordinates);
            RemComp(uid, component);

            if (TryComp<PullableComponent>(uid, out var pullable) && pullable.BeingPulled)
                _pulling.TryStopPull(uid, pullable);
        }
    }

    private void OnShootAttempt(Entity<NullSpaceComponent> ent, ref ShotAttemptedEvent args)
    {
        args.Cancel();
    }

    private void OnAttempt(EntityUid uid, NullSpaceComponent component, CancellableEntityEventArgs args)
    {
        args.Cancel();
    }

    private void OnAttackAttempt(EntityUid uid, NullSpaceComponent component, AttackAttemptEvent args)
    {
        if (HasComp<NullSpaceComponent>(args.Target))
            return;

        args.Cancel();
    }

    private void OnBeforeThrow(Entity<NullSpaceComponent> ent, ref BeforeThrowEvent args)
    {
        args.Cancelled = true;
    }

    private void OnInteractionAttempt(EntityUid uid, NullSpaceComponent component, ref InteractionAttemptEvent args)
    {
        if (args.Target is null)
            return;

        if (HasComp<NullSpaceComponent>(args.Target))
            return;

        args.Cancelled = true;
    }

    private void PreventCollision(EntityUid uid, NullSpaceComponent component, ref PreventCollideEvent args)
    {
        if (HasComp<NullSpaceBlockerComponent>(args.OtherEntity))
            return;
            
        args.Cancelled = true;
    }
}