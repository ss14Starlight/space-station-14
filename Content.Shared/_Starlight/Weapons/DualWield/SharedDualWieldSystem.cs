using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._Starlight.Weapons.DualWield;

/// <summary>
/// Handles the dual-wield toggle verb and cleanup when a gun leaves a hand.
/// The actual alternating-gun logic lives in SharedGunSystem (TryGetGun + OnShootRequest).
/// </summary>
public sealed class SharedDualWieldSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CanDualWieldComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
        SubscribeLocalEvent<GunComponent, GotUnequippedHandEvent>(OnGunUnequipped);
    }

    /// <summary>
    /// Accuracy penalty
    /// </summary>
    private void OnGunRefreshModifiers(Entity<CanDualWieldComponent> gun, ref GunRefreshModifiersEvent args)
    {
        if (gun.Comp.DualWieldInaccuracyPenalty <= 0f)
            return;

        // The gun lives in a ContainerSlot whose parent is the holder entity
        var holder = Transform(gun).ParentUid;
        if (!TryComp<DualWieldComponent>(holder, out var dw) || !dw.Active)
            return;

        if (dw.LeftGun != gun.Owner && dw.RightGun != gun.Owner)
            return;

        var penalty = Angle.FromDegrees(gun.Comp.DualWieldInaccuracyPenalty);
        args.MinAngle += penalty;
        args.MaxAngle += penalty;
    }
    public void ToggleDualWield(EntityUid user, EntityUid leftGun, EntityUid rightGun, bool isCurrentlyActive)
    {
        if (isCurrentlyActive)
        {
            if (TryComp<DualWieldComponent>(user, out var dw))
            {
                var left = dw.LeftGun;
                var right = dw.RightGun;
                dw.Active = false;
                Dirty(user, dw);
                // Remove accuracy penalties
                _gun.RefreshModifiers(left);
                _gun.RefreshModifiers(right);
            }
            _popup.PopupClient(Loc.GetString("dual-wield-disabled"), user, user);
        }
        else
        {
            // Safety check — both guns must have CanDualWieldComponent
            if (!HasComp<CanDualWieldComponent>(leftGun) || !HasComp<CanDualWieldComponent>(rightGun))
            {
                _popup.PopupClient(Loc.GetString("dual-wield-too-heavy"), user, user);
                return;
            }

            var dw = EnsureComp<DualWieldComponent>(user);
            dw.Active   = true;
            dw.LeftGun  = leftGun;
            dw.RightGun = rightGun;

            // Start firing from whichever hand is currently active
            dw.NextIsLeft = _hands.GetActiveItem(user) == leftGun;

            Dirty(user, dw);
            // Refresh both guns so accuracy penalties kick in
            _gun.RefreshModifiers(leftGun);
            _gun.RefreshModifiers(rightGun);
            _popup.PopupClient(Loc.GetString("dual-wield-enabled"), user, user);
        }
    }

    private void OnGunUnequipped(Entity<GunComponent> gun, ref GotUnequippedHandEvent args)
    {
        // If the user that was holding this gun had dual-wield active, disable it
        if (!TryComp<DualWieldComponent>(args.User, out var dw) || !dw.Active)
            return;

        if (dw.LeftGun != gun.Owner && dw.RightGun != gun.Owner)
            return;

        dw.Active = false;
        Dirty(args.User, dw);
        // Remove accuracy penalties from the gun that stayed
        var other = dw.LeftGun == gun.Owner ? dw.RightGun : dw.LeftGun;
        _gun.RefreshModifiers(gun.Owner);
        _gun.RefreshModifiers(other);
        _popup.PopupClient(Loc.GetString("dual-wield-interrupted"), args.User, args.User);
    }

    /// <summary>
    /// Returns true if the user has a gun in each hand.
    /// gun1 is always the gun in the ACTIVE hand (fires first); gun2 is the other.
    /// Works for any two guns with CanDualWieldComponent, regardless of type.
    /// </summary>
    public bool TryGetBothGuns(
        EntityUid user,
        out EntityUid gun1,
        out EntityUid gun2)
    {
        gun1 = EntityUid.Invalid;
        gun2 = EntityUid.Invalid;

        // EnumerateHeld starts with the active hand — so gun1 = active gun naturally.
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (!HasComp<GunComponent>(held))
                continue;

            if (gun1 == EntityUid.Invalid)
                gun1 = held;
            else if (gun2 == EntityUid.Invalid)
            {
                gun2 = held;
                break;
            }
        }

        return gun1 != EntityUid.Invalid && gun2 != EntityUid.Invalid;
    }
}
