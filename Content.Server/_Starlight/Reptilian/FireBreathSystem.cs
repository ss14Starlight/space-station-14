using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Server.Nutrition.EntitySystems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Starlight.Reptilian.Components;
using Content.Shared._Starlight.Reptilian.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Smoking;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Reptilian;

/// <summary>
/// Spits a small flame. Costs stamina and hunger and briefly slows the breather down.
/// Burning fuel from the stomach makes the flame reach further and burn hotter.
/// </summary>
public sealed partial class FireBreathSystem : EntitySystem
{
    [Dependency] private FireBreathFuelSystem _fuel = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private GunSystem _gun = default!;
    [Dependency] private HungerSystem _hunger = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private SmokingSystem _smoking = default!;
    [Dependency] private TransformSystem _transform = default!;

    [SubscribeLocalEvent]
    private void OnFireBreath(Entity<FireBreathComponent> ent, ref FireBreathActionEvent args)
    {
        if (args.Handled)
            return;

        // The action event instance is reused, so clear a cooldown left over from lighting an item.
        args.Cooldown = null;

        var user = ent.Owner;

        if (TryComp<HungerComponent>(user, out var hunger)
            && hunger.CurrentThreshold <= HungerThreshold.Starving)
        {
            _popup.PopupEntity(Loc.GetString("fire-breath-too-hungry"), user, user, PopupType.SmallCaution);
            return;
        }

        // Clicking a lightable thing close by just lights it with a small, cheap puff.
        if (args.Entity is { } lightTarget && TryLightNearby(ent, lightTarget, hunger))
        {
            args.Cooldown = ent.Comp.LightCooldown;
            args.Handled = true;
            return;
        }

        var tier = _fuel.TryBurnFuel(ent);

        // A cigarette in the mouth gets lit either way.
        TryLightSmokable(GetMaskSmokable(user));

        var userCoords = _transform.GetMapCoordinates(user);
        var targetCoords = _transform.ToMapCoordinates(args.Target);
        var direction = targetCoords.Position - userCoords.Position;
        if (targetCoords.MapId != userCoords.MapId || direction.LengthSquared() < 0.0001f)
            direction = _transform.GetWorldRotation(user).ToWorldVec();

        var projectile = Spawn(tier?.Projectile ?? ent.Comp.Projectile, userCoords);
        _gun.ShootProjectile(projectile, direction, _physics.GetMapLinearVelocity(user), user, user, ent.Comp.ProjectileSpeed);

        _audio.PlayPvs(tier?.Sound ?? ent.Comp.BreathSound, user);

        _stamina.TakeStaminaDamage(user, ent.Comp.StaminaCost, visual: false);
        if (hunger != null)
            _hunger.ModifyHunger(user, -ent.Comp.HungerCost, hunger);
        _movementMod.TryUpdateMovementSpeedModDuration(user, ent.Comp.SlowdownEffect, ent.Comp.SlowdownDuration, ent.Comp.SlowdownMultiplier);

        _popup.PopupEntity(Loc.GetString(tier != null ? $"{tier.Popup}-others" : "fire-breath-others", ("user", Identity.Entity(user, EntityManager))),
            user,
            Filter.PvsExcept(user),
            true);
        _popup.PopupEntity(Loc.GetString(tier != null ? $"{tier.Popup}-self" : "fire-breath-self"), user, user);

        args.Handled = true;
    }

    private bool TryLightNearby(Entity<FireBreathComponent> ent, EntityUid target, HungerComponent? hunger)
    {
        var user = ent.Owner;

        if (target == user)
        {
            if (GetMaskSmokable(user) is not { } mask)
                return false;
            target = mask;
        }
        else if (HasComp<MobStateComponent>(target) || !_interaction.InRangeUnobstructed(user, target, ent.Comp.LightRange))
        {
            return false;
        }

        if (!TryLightSmokable(target) && !TryIgnite(target, user))
            return false;

        _audio.PlayPvs(ent.Comp.LightSound, user);
        _stamina.TakeStaminaDamage(user, ent.Comp.LightStaminaCost, visual: false);
        if (hunger != null)
            _hunger.ModifyHunger(user, -ent.Comp.LightHungerCost, hunger);

        var targetName = Identity.Entity(target, EntityManager);
        _popup.PopupEntity(Loc.GetString("fire-breath-light-others", ("user", Identity.Entity(user, EntityManager)), ("target", targetName)),
            user,
            Filter.PvsExcept(user),
            true);
        _popup.PopupEntity(Loc.GetString("fire-breath-light-self", ("target", targetName)), user, user);
        return true;
    }

    private EntityUid? GetMaskSmokable(EntityUid user)
    {
        if (_inventory.TryGetSlotEntity(user, "mask", out var mask) && HasComp<SmokableComponent>(mask))
            return mask;

        return null;
    }

    private bool TryLightSmokable(EntityUid? uid)
    {
        if (!TryComp<SmokableComponent>(uid, out var smokable) || smokable.State != SmokableState.Unlit)
            return false;

        _smoking.SetSmokableState(uid.Value, SmokableState.Lit, smokable);
        return true;
    }

    private bool TryIgnite(EntityUid target, EntityUid user)
    {
        if (!TryComp<FlammableComponent>(target, out var flammable) || flammable.OnFire)
            return false;

        _flammable.Ignite(target, user, flammable, user);
        return flammable.OnFire;
    }
}
