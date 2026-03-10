using System.Numerics;
using Content.Server.Cargo.Systems;
using Content.Server.Weapons.Ranged.Components;
using Content.Shared.Cargo;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

#region Starlight
using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Decals;
using Content.Server.Emp;
using Content.Server.IgnitionSource;
using Content.Server.Interaction;
using Content.Server.Mech.Equipment.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Stunnable.Components;
using Content.Server.Stunnable;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Components;
using Content.Shared.Decals;
using Content.Shared.Interaction.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Reflect;
using Content.Shared._Starlight.Weapon.Components;
using Content.Shared._Starlight.Weapon;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Random;
using Robust.Shared.Timing;
#endregion Starlight

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem : SharedGunSystem
{
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

#region Starlight
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _rand = default!;
#endregion Starlight

    private const float DamagePitchVariation = 0.05f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BallisticAmmoProviderComponent, PriceCalculationEvent>(OnBallisticPrice);
    }

    private void OnBallisticPrice(Entity<BallisticAmmoProviderComponent> ent, ref PriceCalculationEvent args)
    {
        if (string.IsNullOrEmpty(ent.Comp.Proto) || ent.Comp.UnspawnedCount == 0)
            return;

        if (!ProtoManager.TryIndex<EntityPrototype>(ent.Comp.Proto, out var proto))
        {
            Log.Error($"Unable to find fill prototype for price on {ent.Comp.Proto} on {ToPrettyString(ent)}");
            return;
        }

        // Probably good enough for most.
        var price = _pricing.GetEstimatedPrice(proto);
        args.Price += price * ent.Comp.UnspawnedCount;
    }

    public override void Shoot(Entity<GunComponent> gun, List<(EntityUid? Entity, IShootable Shootable)> ammo,
        EntityCoordinates fromCoordinates, EntityCoordinates toCoordinates, out bool userImpulse, out bool fired, EntityUid? user = null, bool throwItems = false) // Starlight-edit
    {
        fired = false; // Starlight
        userImpulse = true;

        if (user != null)
        {
            var selfEvent = new SelfBeforeGunShotEvent(user.Value, gun, ammo);
            RaiseLocalEvent(user.Value, selfEvent);
            if (selfEvent.Cancelled)
            {
                userImpulse = false;
                return;
            }
        }

        var fromMap = TransformSystem.ToMapCoordinates(fromCoordinates);
        var toMap = TransformSystem.ToMapCoordinates(toCoordinates).Position;
        var mapDirection = toMap - fromMap.Position;
        var mapAngle = mapDirection.ToAngle();
        var angle = GetRecoilAngle(Timing.CurTime, gun, mapDirection.ToAngle());

        // If applicable, this ensures the projectile is parented to grid on spawn, instead of the map.
        var fromEnt = MapManager.TryFindGridAt(fromMap, out var gridUid, out _)
            ? TransformSystem.WithEntityId(fromCoordinates, gridUid)
            : new EntityCoordinates(_map.GetMapOrInvalid(fromMap.MapId), fromMap.Position);

        // Update shot based on the recoil
        toMap = fromMap.Position + angle.ToVec() * mapDirection.Length();
        mapDirection = toMap - fromMap.Position;
        var gunVelocity = Physics.GetMapLinearVelocity(fromEnt);

        // I must be high because this was getting tripped even when true.
        // DebugTools.Assert(direction != Vector2.Zero);
        var shotProjectiles = new List<EntityUid>(ammo.Count);

        foreach (var (ent, shootable) in ammo)
        {
            // pneumatic cannon doesn't shoot bullets it just throws them, ignore ammo handling
            if (throwItems && ent != null)
            {
                ShootOrThrow(ent.Value, mapDirection, gunVelocity, gun, user);
                fired = true; // Starlight
                continue;
            }

            // TODO: Clean this up in a gun refactor at some point - too much copy pasting
            switch (shootable)
            {
                // Cartridge shoots something else
                case CartridgeAmmoComponent cartridge:
                    if (!cartridge.Spent)
                    {
                        var uid = Spawn(cartridge.Prototype, fromEnt);
                        CreateAndFireProjectiles(uid, cartridge);

                        RaiseLocalEvent(ent!.Value, new AmmoShotEvent()
                        {
                            FiredProjectiles = shotProjectiles,
                        });

                        SetCartridgeSpent(ent.Value, cartridge, true);

                        if (cartridge.DeleteOnSpawn)
                            Del(ent.Value);
                        fired = true; // Starlight
                    }
                    else
                    {
                        userImpulse = false;
                        Audio.PlayPredicted(gun.Comp.SoundEmpty, gun, user);
                    }

                    // Something like ballistic might want to leave it in the container still
                    if (!cartridge.DeleteOnSpawn && !Containers.IsEntityInContainer(ent!.Value) && !gun.Comp.Pump)
                        EjectCartridge(ent!.Value, angle);

                    Dirty(ent!.Value, cartridge);
                    break;
                // Ammo shoots itself
                case AmmoComponent newAmmo:
                    if (ent == null)
                        break;
                    CreateAndFireProjectiles(ent.Value, newAmmo);

                    fired = true; // Starlight
                    break;
                case HitscanAmmoComponent:
                    if (ent == null)
                        break;

                    var hitscanEv = new HitscanTraceEvent
                    {
                        FromCoordinates = fromCoordinates,
                        ToCoordinates = toCoordinates, // Starlight-edit
                        ShotDirection = mapDirection.Normalized(),
                        Gun = gun,
                        Shooter = user,
                        Target = gun.Comp.Target,
                    };
                    RaiseLocalEvent(ent.Value, ref hitscanEv);

                    Del(ent);

                    Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);
                    fired = true; // Starlight
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        RaiseLocalEvent(gun, new AmmoShotEvent()
        {
            FiredProjectiles = shotProjectiles,
            Shooter = user, //starlight
        });

        void CreateAndFireProjectiles(EntityUid ammoEnt, AmmoComponent ammoComp)
        {
            // Startlight-edit: start
            var isMechShooter = user != null && TryComp<MechPilotComponent>(user.Value, out _);
            const float MechMuzzleOffset = 0.8f;

            EntityCoordinates SpawnFrom(Angle angle)
            {
                if (!isMechShooter)
                    return fromEnt;

                var localAngle = angle;
                if (TryComp(fromEnt.EntityId, out TransformComponent? anchorXform))
                {
                    var anchorRot = _transform.GetWorldRotation(anchorXform);
                    localAngle -= anchorRot;
                }

                var dir = localAngle.ToVec().Normalized();
                return fromEnt.Offset(dir * MechMuzzleOffset);
            }
            // Startlight-edit: end
            if (TryComp<ProjectileSpreadComponent>(ammoEnt, out var ammoSpreadComp))
            {
                var spreadEvent = new GunGetAmmoSpreadEvent(ammoSpreadComp.Spread);
                RaiseLocalEvent(gun, ref spreadEvent);

                var angles = LinearSpread(mapAngle - spreadEvent.Spread / 2,
                    mapAngle + spreadEvent.Spread / 2, ammoSpreadComp.Count);
                // Startlight-edit: start
                if (isMechShooter)
                {
                    var spawn = SpawnFrom(angles[0]);
                    _transform.SetCoordinates(ammoEnt, Transform(ammoEnt), spawn);
                }
                // Startlight-edit: end
                ShootOrThrow(ammoEnt, angles[0].ToVec(), gunVelocity, gun, user);
                shotProjectiles.Add(ammoEnt);

                for (var i = 1; i < ammoSpreadComp.Count; i++)
                {
                    // Startlight-edit: start
                    var spawn = isMechShooter ? SpawnFrom(angles[i]) : fromEnt;
                    var newuid = Spawn(ammoSpreadComp.Proto, spawn);
                    // Startlight-edit: end
                    ShootOrThrow(newuid, angles[i].ToVec(), gunVelocity, gun, user);
                    shotProjectiles.Add(newuid);
                }
            }
            else
            {
                // Startlight-edit: start
                if (isMechShooter)
                {
                    var spawn = SpawnFrom(mapDirection.ToAngle());
                    _transform.SetCoordinates(ammoEnt, Transform(ammoEnt), spawn);
                }
                // Startlight-edit: end
                ShootOrThrow(ammoEnt, mapDirection, gunVelocity, gun, user);
                shotProjectiles.Add(ammoEnt);
            }

            MuzzleFlash(gun, ammoComp, mapDirection.ToAngle(), user);
            Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);
        }
    }

    private void ShootOrThrow(EntityUid uid, Vector2 mapDirection, Vector2 gunVelocity, Entity<GunComponent> gun, EntityUid? user)
    {
        if (gun.Comp.Target is { } target && !TerminatingOrDeleted(target))
        {
            var targeted = EnsureComp<TargetedProjectileComponent>(uid);
            targeted.Target = target;
            Dirty(uid, targeted);
        }

        // Starlight start - cartridges can hold hitscans
        if (HasComp<HitscanAmmoComponent>(uid))
        {
            var coordinates = EntityManager.GetComponent<TransformComponent>(uid).Coordinates; // Starlight-edit
            var hitscanEv = new HitscanTraceEvent
            {
                FromCoordinates = coordinates,
                ToCoordinates = coordinates.Offset(mapDirection), // Starlight-edit
                ShotDirection = mapDirection.Normalized(),
                Gun = gun,
                Shooter = user,
                Target = gun.Comp.Target,
            };
            RaiseLocalEvent(uid, ref hitscanEv);

            Del(uid);
            return;
        }
        // Starlight end - cartridges can hold hitscans

        // Do a throw
        if (!HasComp<ProjectileComponent>(uid))
        {
            RemoveShootable(uid);
            // TODO: Someone can probably yeet this a billion miles so need to pre-validate input somewhere up the call stack.
            ThrowingSystem.TryThrow(uid, mapDirection, gun.Comp.ProjectileSpeedModified, user);
            return;
        }

        ShootProjectile(uid, mapDirection, gunVelocity, gun, user, gun.Comp.ProjectileSpeedModified);
    }

    /// <summary>
    /// Gets a linear spread of angles between start and end.
    /// </summary>
    /// <param name="start">Start angle in degrees</param>
    /// <param name="end">End angle in degrees</param>
    /// <param name="intervals">How many shots there are</param>
    private Angle[] LinearSpread(Angle start, Angle end, int intervals)
    {
        var angles = new Angle[intervals];
        DebugTools.Assert(intervals > 1);

        for (var i = 0; i <= intervals - 1; i++)
        {
            angles[i] = new Angle(start + (end - start) * i / (intervals - 1));
        }

        return angles;
    }

    // 🌟Starlight🌟
    private Angle[] LinearSpreadWithRandom(Angle start, Angle end, int intervals, float randomSpread)
    {
        var angles = new Angle[intervals];
        DebugTools.Assert(intervals > 1);

        for (var i = 0; i < intervals; i++)
        {
            var t = (float)i / (intervals - 1);
            var baseAngle = start + (end - start) * t;

            var randomFactor = _rand.NextFloat() - 0.5f;

            var randomOffset = Angle.FromDegrees(randomFactor * randomSpread);

            angles[i] = baseAngle + randomOffset;
        }

        return angles;
    }

    private Angle GetRecoilAngle(TimeSpan curTime, GunComponent component, Angle direction)
    {
        var timeSinceLastFire = (curTime - component.LastFire).TotalSeconds;
        var newTheta = MathHelper.Clamp(component.CurrentAngle.Theta + component.AngleIncreaseModified.Theta - component.AngleDecayModified.Theta * timeSinceLastFire, component.MinAngleModified.Theta, component.MaxAngleModified.Theta);
        component.CurrentAngle = new Angle(newTheta);
        component.LastFire = component.NextFire;

        // Convert it so angle can go either side.
        var random = Random.NextFloat(-0.5f, 0.5f);
        var spread = component.CurrentAngle.Theta * random;
        var angle = new Angle(direction.Theta + component.CurrentAngle.Theta * random);
        DebugTools.Assert(spread <= component.MaxAngleModified.Theta);
        return angle;
    }

    protected override void Popup(string message, EntityUid? uid, EntityUid? user) { }

    protected override void CreateEffect(EntityUid gunUid, MuzzleFlashEvent message, EntityUid? user = null)
    {
        var filter = Filter.Pvs(gunUid, entityManager: EntityManager);

        if (TryComp<ActorComponent>(user, out var actor))
            filter.RemovePlayer(actor.PlayerSession);

        RaiseNetworkEvent(message, filter);
    }

    public override void PlayImpactSound(EntityUid otherEntity, DamageSpecifier? modifiedDamage, SoundSpecifier? weaponSound, bool forceWeaponSound)
    {
        DebugTools.Assert(!Deleted(otherEntity), "Impact sound entity was deleted");

        // Like projectiles and melee,
        // 1. Entity specific sound
        // 2. Ammo's sound
        // 3. Nothing
        var playedSound = false;

        if (!forceWeaponSound && modifiedDamage != null && modifiedDamage.GetTotal() > 0 && TryComp<RangedDamageSoundComponent>(otherEntity, out var rangedSound))
        {
            var type = SharedMeleeWeaponSystem.GetHighestDamageSound(modifiedDamage, ProtoManager);

            if (type != null && rangedSound.SoundTypes?.TryGetValue(type, out var damageSoundType) == true)
            {
                Audio.PlayPvs(damageSoundType, otherEntity, AudioParams.Default.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
            else if (type != null && rangedSound.SoundGroups?.TryGetValue(type, out var damageSoundGroup) == true)
            {
                Audio.PlayPvs(damageSoundGroup, otherEntity, AudioParams.Default.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
        }

        if (!playedSound && weaponSound != null)
        {
            Audio.PlayPvs(weaponSound, otherEntity);
        }
    }
}
