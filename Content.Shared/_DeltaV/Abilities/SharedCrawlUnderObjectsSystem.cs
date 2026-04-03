using Content.Shared.Actions;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Events;
using Content.Shared.Maps;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._DeltaV.Abilities;

public sealed class SharedCrawlUnderObjectsSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movespeed = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrawlUnderObjectsComponent, ComponentStartup>(OnStartup);

        SubscribeLocalEvent<CrawlUnderObjectsComponent, ToggleCrawlingStateEvent>(OnAbilityToggle);
        SubscribeLocalEvent<CrawlUnderObjectsComponent, AttemptClimbEvent>(OnAttemptClimb);
        SubscribeLocalEvent<CrawlUnderObjectsComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
    }

    private void OnAbilityToggle(EntityUid uid, CrawlUnderObjectsComponent component, ToggleCrawlingStateEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<ClimbingComponent>(uid, out var climbing) && climbing.IsClimbing)
            return;

        if (component.Enabled)
            DisableSneakMode(uid, component);
        else
            EnableSneakMode(uid, component);

        _appearance.SetData(uid, SneakMode.Enabled, component.Enabled);

        _movespeed.RefreshMovementSpeedModifiers(uid);

        args.Handled = true;
    }

    private void OnAttemptClimb(EntityUid uid, CrawlUnderObjectsComponent component, AttemptClimbEvent args)
    {
        if (component.Enabled)
            args.Cancelled = true;
    }

    private void OnRefreshMovementSpeed(EntityUid uid, CrawlUnderObjectsComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.Enabled)
            args.ModifySpeed(component.SneakSpeedModifier, component.SneakSpeedModifier);
    }

    private void OnStartup(Entity<CrawlUnderObjectsComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.ToggleHideAction != null)
            return;

        _actionsSystem.AddAction(ent, ref ent.Comp.ToggleHideAction, ent.Comp.ActionProto);
    }

    #region Helper functions

    private void EnableSneakMode(EntityUid uid, CrawlUnderObjectsComponent component)
    {
        component.Enabled = true;
        Dirty(uid, component);

        _popup.PopupClient(Loc.GetString("crawl-under-objects-toggle-on"), uid, uid);

        if (!TryComp<FixturesComponent>(uid, out var fixtureComponent))
            return;

        foreach (var (key, fixture) in fixtureComponent.Fixtures)
        {
            var newMask = (fixture.CollisionMask
                           & (int)~CollisionGroup.HighImpassable
                           & (int)~CollisionGroup.MidImpassable)
                          | (int)CollisionGroup.InteractImpassable;

            if (fixture.CollisionMask == newMask)
                continue;

            component.ChangedFixtures.Add((key, fixture.CollisionMask));
            _physics.SetCollisionMask(uid, key, fixture, newMask, manager: fixtureComponent);
        }

        return;
    }

    private void DisableSneakMode(EntityUid uid, CrawlUnderObjectsComponent component)
    {
        if (IsOnCollidingTile(uid))
        {
            _popup.PopupClient(Loc.GetString("crawl-under-objects-toggle-off-fail"), uid, uid);
            return;
        }

        _popup.PopupClient(Loc.GetString("crawl-under-objects-toggle-off"), uid, uid);

        // Restore normal collision masks
        if (TryComp<FixturesComponent>(uid, out var fixtureComponent))
        {
            foreach (var (key, originalMask) in component.ChangedFixtures)
            {
                if (fixtureComponent.Fixtures.TryGetValue(key, out var fixture))
                    _physics.SetCollisionMask(uid, key, fixture, originalMask, fixtureComponent);
            }
        }

        component.Enabled = false;
        component.ChangedFixtures.Clear();

        Dirty(uid, component);
    }

    private bool IsOnCollidingTile(EntityUid uid)
    {
        if (!_turf.TryGetTileRef(Transform(uid).Coordinates, out var tile))
            return false;

        return _turf.IsTileBlocked(tile.Value, CollisionGroup.MobMask);
    }

    #endregion
}
