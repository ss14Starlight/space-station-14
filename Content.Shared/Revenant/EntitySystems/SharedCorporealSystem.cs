using Content.Shared.Physics;
using Robust.Shared.Physics;
using System.Linq;
using Content.Shared.Movement.Systems;
using Content.Shared.Revenant.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Revenant.EntitySystems;

/// <summary>
/// Makes the revenant solid when the status effect is applied.
/// Additionally applies a few visual effects.
/// </summary>
public abstract partial class SharedCorporealSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CorporealComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<CorporealComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<CorporealComponent, StatusEffectRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefresh);
    }

    private void OnRefresh(Entity<CorporealComponent> effect, ref StatusEffectRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        args.Args.ModifySpeed(effect.Comp.MovementSpeedDebuff, effect.Comp.MovementSpeedDebuff);
    }

    public virtual void OnApplied(Entity<CorporealComponent> effect, ref StatusEffectAppliedEvent args)
    {
        var uid = args.Target;
        _appearance.SetData(uid, RevenantVisuals.Corporeal, true);

        if (TryComp<FixturesComponent>(uid, out var fixtures) && fixtures.FixtureCount >= 1)
        {
            var fixture = fixtures.Fixtures.First();

            _physics.SetCollisionMask(uid, fixture.Key, fixture.Value, (int) (CollisionGroup.SmallMobMask | CollisionGroup.GhostImpassable), fixtures);
            _physics.SetCollisionLayer(uid, fixture.Key, fixture.Value, (int) CollisionGroup.SmallMobLayer, fixtures);
        }
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    public virtual void OnRemoved(Entity<CorporealComponent> effect, ref StatusEffectRemovedEvent args)
    {
        var uid = args.Target;
        _appearance.SetData(uid, RevenantVisuals.Corporeal, false);

        if (TryComp<FixturesComponent>(uid, out var fixtures) && fixtures.FixtureCount >= 1)
        {
            var fixture = fixtures.Fixtures.First();

            _physics.SetCollisionMask(uid, fixture.Key, fixture.Value, (int) CollisionGroup.GhostImpassable, fixtures);
            _physics.SetCollisionLayer(uid, fixture.Key, fixture.Value, 0, fixtures);
        }
        _movement.RefreshMovementSpeedModifiers(uid);
    }
}
