using Content.Shared.Interaction.Events;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Mobs;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Content.Shared.Item;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Explosion;
using Content.Shared.Stunnable;
using Content.Shared.Movement.Events;
using Content.Shared.Gravity;

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
        SubscribeLocalEvent<NullSpaceComponent, GetExplosionResistanceEvent>(OnGetExplosionResistance);
        SubscribeLocalEvent<NullSpaceComponent, KnockDownAttemptEvent>(OnKnockdownAttempt);
        SubscribeLocalEvent<NullSpaceComponent, CanWeightlessMoveEvent>((_, _, args) => args.CanMove = true);
        SubscribeLocalEvent<NullSpaceComponent, IsWeightlessEvent>(OnIsWeightless);
    }

    private void OnIsWeightless(Entity<NullSpaceComponent> entity, ref IsWeightlessEvent args)
    {
        args.IsWeightless = false;
        args.Handled = true;
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

    private void OnKnockdownAttempt(EntityUid uid, NullSpaceComponent component, ref KnockDownAttemptEvent args)
    {
        args.Cancelled = true;
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

    private void OnGetExplosionResistance(EntityUid uid, NullSpaceComponent component, ref GetExplosionResistanceEvent args)
        {
            args.DamageCoefficient = 0;
        }
}