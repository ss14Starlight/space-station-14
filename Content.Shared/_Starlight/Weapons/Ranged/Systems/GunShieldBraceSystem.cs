using Content.Shared.Blocking;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;

namespace Content.Shared._Starlight.Weapons.Ranged.Systems;

public sealed partial class GunShieldBraceSystem : EntitySystem
{
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunWieldBonusComponent, GotEquippedHandEvent>(OnGunEquippedHand);
        SubscribeLocalEvent<GunWieldBonusComponent, GotUnequippedHandEvent>(OnGunUnequippedHand);
    }

    private void OnGunEquippedHand(Entity<GunWieldBonusComponent> gun, ref GotEquippedHandEvent args)
        => _gun.RefreshModifiers(gun.Owner);

    private void OnGunUnequippedHand(Entity<GunWieldBonusComponent> gun, ref GotUnequippedHandEvent args)
        => _gun.RefreshModifiers(gun.Owner);

    public void RefreshHeldGuns(EntityUid user)
    {
        if (!TryComp<HandsComponent>(user, out var hands))
            return;

        foreach (var held in _hands.EnumerateHeld((user, hands)))
        {
            if (HasComp<GunWieldBonusComponent>(held))
                _gun.RefreshModifiers(held);
        }
    }

    public void ApplyBraceBonus(Entity<GunWieldBonusComponent> gun, ref GunRefreshModifiersEvent args)
    {
        var fraction = gun.Comp.ShieldBraceMultiplier;

        if (fraction <= 0f || !IsBracedWithShield(gun.Owner))
            return;

        args.MinAngle += gun.Comp.MinAngle * fraction;
        args.MinAngle /= ScaleDivider(gun.Comp.MinAngleDivider, fraction);
        args.MaxAngle += gun.Comp.MaxAngle * fraction;
        args.MaxAngle /= ScaleDivider(gun.Comp.MaxAngleDivider, fraction);
        args.AngleDecay += gun.Comp.AngleDecay * fraction;
        args.AngleDecay /= ScaleDivider(gun.Comp.AngleDecayDivider, fraction);
        args.AngleIncrease += gun.Comp.AngleIncrease * fraction;
        args.AngleIncrease /= ScaleDivider(gun.Comp.AngleIncreaseDivider, fraction);
    }

    private static float ScaleDivider(float divider, float fraction)
    {
        var scaled = 1f + ((divider - 1f) * fraction);

        return scaled <= 0f ? 1f : scaled;
    }

    public string? TryGetBraceExamineMessage(Entity<GunWieldBonusComponent> gun)
    {
        if (gun.Comp.ShieldBraceMultiplier <= 0f || gun.Comp.ShieldBraceExamineMessage is not { } message)
            return null;

        if (HasComp<GunRequiresWieldComponent>(gun))
            return null;

        return Loc.GetString(message);
    }

    public bool IsBracedWithShield(EntityUid gun)
    {
        if (TryComp<WieldableComponent>(gun, out var wieldable) && wieldable.Wielded)
            return false;

        var holder = Transform(gun).ParentUid;

        if (!TryComp<HandsComponent>(holder, out var hands) || !_hands.IsHolding((holder, hands), gun, out _))
            return false;

        foreach (var held in _hands.EnumerateHeld((holder, hands)))
        {
            if (held == gun || !HasComp<BlockingComponent>(held))
                continue;

            if (HasComp<ItemToggleComponent>(held) && !_toggle.IsActivated(held))
                continue;

            return true;
        }

        return false;
    }
}
