using Content.Shared.Actions;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Events;
using Content.Shared.Hands;
using Content.Shared.Item;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Starlight.CrawlUnder;

public abstract class SharedCrawlUnderObjectsSystem : EntitySystem
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

        SubscribeLocalEvent<CrawlUnderObjectsComponent, DropAttemptEvent>(OnDropAttempt);
        SubscribeLocalEvent<CrawlUnderObjectsComponent, PickupAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<CrawlUnderObjectsComponent, UseAttemptEvent>(OnUseAttempt);
        SubscribeLocalEvent<CrawlUnderObjectsComponent, ShotAttemptedEvent>(OnShootAttempt);
        SubscribeLocalEvent<CrawlUnderObjectsComponent, AttemptMeleeEvent>(OnMeleeAttempt);
    }

    private void OnAbilityToggle(EntityUid uid, CrawlUnderObjectsComponent component, ToggleCrawlingStateEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<ClimbingComponent>(uid, out var climbing) && climbing.IsClimbing)
            return;

        // Block ability when standing up from knockdown
        if (TryComp<KnockedDownComponent>(uid, out var knockedDown) && knockedDown.DoAfterId.HasValue)
            return;

        // Don't allow entering sneak while currently on a blocking tile (like a table)
        if (IsOnCollidingTile(uid))
        {
            _popup.PopupClient(Loc.GetString("knockdown-component-stand-no-room"), uid, uid, PopupType.SmallCaution);
            return;
        }

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

    private void OnRefreshMovementSpeed(EntityUid uid, CrawlUnderObjectsComponent component,
        RefreshMovementSpeedModifiersEvent args)
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

        _popup.PopupClient(Loc.GetString("crawl-under-objects-toggle-on"), uid, uid, PopupType.MediumCaution);

        //var ev = new DropHandItemsEvent();
        //RaiseLocalEvent(uid, ref ev);

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
            _popup.PopupClient(Loc.GetString("crawl-under-objects-toggle-off-fail"), uid, uid, PopupType.SmallCaution);
            return;
        }

        _popup.PopupClient(Loc.GetString("crawl-under-objects-toggle-off"), uid, uid, PopupType.MediumCaution);

        // Restore normal collision masks
        if (TryComp<FixturesComponent>(uid, out var fixtureComponent))
        {
            var down = TryComp<StandingStateComponent>(uid, out var standing) && !standing.Standing;

            foreach (var (key, originalMask) in component.ChangedFixtures)
            {
                if (fixtureComponent.Fixtures.TryGetValue(key, out var fixture))
                {
                    var targetMask = down
                        ? originalMask & ~StandingStateSystem.StandingCollisionLayer
                        : originalMask | StandingStateSystem.StandingCollisionLayer;

                    if (fixture.CollisionMask == targetMask)
                        continue;

                    _physics.SetCollisionMask(uid, key, fixture, targetMask, fixtureComponent);
                }
            }
        }

        component.Enabled = false;
        component.ChangedFixtures.Clear();

        Dirty(uid, component);
    }

    /// <summary>
    /// Disallows dropping items when underneath a table while sneaking.
    /// </summary>
    private void OnDropAttempt(EntityUid uid, CrawlUnderObjectsComponent component, ref DropAttemptEvent args)
    {
        if (args.Cancelled) return;
        if (!component.Enabled || !component.BlockHands) return;
        if (!IsOnCollidingTile(uid)) return;

        args.Cancel();
    }

    /// <summary>
    /// Disallows gun use entirely while sneaking.
    /// </summary>
    private void OnShootAttempt(EntityUid uid, CrawlUnderObjectsComponent component, ref ShotAttemptedEvent args)
    {
        if (args.Cancelled) return;
        if (!component.Enabled || !component.BlockHands) return;

        // Use popup cooldown here since the event is fired every tick.
        if (TryPopupCooldown(component))
            _popup.PopupClient(Loc.GetString("crawl-under-objects-attack-fail"), uid, uid, PopupType.MediumCaution);
        args.Cancel();
    }

    /// <summary>
    /// Disallows melee use entirely while sneaking.
    /// </summary>
    private void OnMeleeAttempt(EntityUid uid, CrawlUnderObjectsComponent component, ref AttemptMeleeEvent args)
    {
        if (args.Cancelled) return;
        if (!component.Enabled || !component.BlockHands) return;

        _popup.PopupClient(Loc.GetString("crawl-under-objects-attack-fail"), uid, uid, PopupType.MediumCaution);
        args.Cancelled = true;
    }

    private void OnPickupAttempt(EntityUid uid, CrawlUnderObjectsComponent component, ref PickupAttemptEvent args)
    {
        if (args.Cancelled) return;
        if (!component.Enabled || !component.BlockHands) return;
        if (!IsOnCollidingTile(uid)) return;

        _popup.PopupClient(Loc.GetString("crawl-under-objects-pickup-fail"), uid, uid, PopupType.SmallCaution);
        args.Cancel();
    }

    private void OnUseAttempt(EntityUid uid, CrawlUnderObjectsComponent component, ref UseAttemptEvent args)
    {
        if (args.Cancelled) return;
        if (!component.Enabled || !component.BlockHands) return;
        if (!IsOnCollidingTile(uid)) return;

        _popup.PopupClient(Loc.GetString("crawl-under-objects-use-fail"), uid, uid, PopupType.SmallCaution);
        args.Cancel();
    }

    private bool IsOnCollidingTile(EntityUid uid)
    {
        if (!_turf.TryGetTileRef(Transform(uid).Coordinates, out var tile))
            return false;

        return _turf.IsTileBlocked(tile.Value, CollisionGroup.MobMask);
    }

    /// <summary>
    /// Used to prevent spamming failure popups since some events are fired every tick (guh). Uses
    /// <see cref="CrawlUnderObjectsComponent.LastFailedPopup"/> and
    /// <see cref="CrawlUnderObjectsComponent.FailedPopupCooldown"/> to determine if a popup should be shown.
    /// </summary>
    /// <returns>Whether to show the popup this time</returns>
    protected virtual bool TryPopupCooldown(CrawlUnderObjectsComponent comp) => false;

    #endregion
}
