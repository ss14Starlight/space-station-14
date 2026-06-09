using Content.Shared.Magic.Components;
// Starlight-start: Added using statements for size tracking and events
using Content.Shared._Starlight.Magic.Components;
using Content.Shared.Magic.Events;
using Content.Shared.Item;
// Starlight-end
using Content.Shared.Physics;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using System.Linq;

namespace Content.Shared.Magic.Systems;

public sealed class AnimateSpellSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AnimateComponent, MapInitEvent>(OnAnimate);
        // Starlight-start: Store item size before Item component removal for HP calculation
        SubscribeLocalEvent<ChangeComponentsSpellEvent>(OnChangeComponentsSpell);
        // Starlight-end
    }

    // Starlight-start: Store item size BEFORE Item component gets removed
    // Store item size BEFORE Item component gets removed
    private void OnChangeComponentsSpell(ChangeComponentsSpellEvent ev)
    {
        // Check if this spell will add the Animate component (making it an animated object)
        if (!ev.ToAdd.ContainsKey("Animate"))
            return;

        // Store the item size before removal
        if (TryComp<ItemComponent>(ev.Target, out var item))
        {
            var sizeComp = EnsureComp<AnimatedObjectSizeComponent>(ev.Target);
            sizeComp.OriginalSize = item.Size.Id;
        }
    }
    // Starlight-end

    private void OnAnimate(Entity<AnimateComponent> ent, ref MapInitEvent args)
    {
        // Physics bullshittery necessary for object to behave properly

        if (!TryComp<FixturesComponent>(ent, out var fixtures) || !TryComp<PhysicsComponent>(ent, out var physics))
            return;

        var xform = Transform(ent);
        // Starlight-start: Removed hardcoded fixture selection, now loop through all fixtures
        // var fixture = fixtures.Fixtures.First();
        // Starlight-end

        _transform.Unanchor(ent); // If left anchored they are effectively stuck/immobile and not a threat
        _physics.SetCanCollide(ent, true, true, false, fixtures, physics);

        // Starlight-start: Set collision on ALL fixtures, not just the first one
        // Add MidImpassable so melee AttackMask can detect animated objects for wide swings (AttackMask = MobMask | Opaque)
        foreach (var (key, fixture) in fixtures.Fixtures)
        {
            _physics.SetCollisionMask(ent, key, fixture, (int)CollisionGroup.FlyingMobMask, fixtures, physics);
            _physics.SetCollisionLayer(ent, key, fixture, (int)(CollisionGroup.FlyingMobLayer | CollisionGroup.MidImpassable), fixtures, physics);
            _physics.SetHard(ent, fixture, true, fixtures);
        }
        // Starlight-end
        // Starlight-start: Commented out old single-fixture collision code
        // _physics.SetCollisionMask(ent, fixture.Key, fixture.Value, (int)CollisionGroup.FlyingMobMask, fixtures, physics);
        // _physics.SetCollisionLayer(ent, fixture.Key, fixture.Value, (int)CollisionGroup.FlyingMobLayer, fixtures, physics);
        // Starlight-end

        _physics.SetBodyType(ent, BodyType.KinematicController, fixtures, physics, xform);
        _physics.SetBodyStatus(ent, physics, BodyStatus.InAir, true);
        _physics.SetFixedRotation(ent, false, true, fixtures, physics);
        // Starlight-start: Removed single-fixture SetHard call (now done in loop above)
        // _physics.SetHard(ent, fixture.Value, true, fixtures);
        // Starlight-end
        _container.AttachParentToContainerOrGrid((ent, xform)); // Items animated inside inventory now exit, they can't be picked up and so can't escape otherwise

        var ev = new AnimateSpellEvent();
        RaiseLocalEvent(ent, ref ev);
    }
}

// Starlight-start: Event for server-side HP setting
[ByRefEvent]
public readonly record struct AnimateSpellEvent;
// Starlight-end
