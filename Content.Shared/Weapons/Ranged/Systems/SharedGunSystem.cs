using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._Starlight.Weapon.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Audio;
using Content.Shared.CombatMode;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Timing;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared.VentCrawl;

#region Starlight
using Content.Shared._Starlight.Weapons.DualWield;
using Content.Shared.Mech.Components;
using Content.Shared.Starlight.Utility;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared._Starlight.Camera;
#endregion Starlight

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly RechargeBasicEntityAmmoSystem _recharge = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] protected readonly DamageableSystem Damageable = default!;
    [Dependency] protected readonly ExamineSystemShared Examine = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IMapManager MapManager = default!;
    [Dependency] protected readonly IPrototypeManager ProtoManager = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] protected readonly ISharedAdminLogManager Logs = default!;
    [Dependency] protected readonly SharedActionsSystem Actions = default!;
    [Dependency] protected readonly SharedAppearanceSystem Appearance = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
    [Dependency] protected readonly SharedContainerSystem Containers = default!;
    [Dependency] protected readonly SharedPhysicsSystem Physics = default!;
    [Dependency] protected readonly SharedPointLightSystem Lights = default!;
    [Dependency] protected readonly SharedPopupSystem PopupSystem = default!;
    [Dependency] protected readonly SharedProjectileSystem Projectiles = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
    [Dependency] protected readonly TagSystem TagSystem = default!;
    [Dependency] protected readonly ThrowingSystem ThrowingSystem = default!;
    [Dependency] private readonly ScreenshakeSystem _shake = default!; // Starlight | ES Screenshake

    /// <summary>
    /// Default projectile speed
    /// </summary>
    public const float ProjectileSpeed = 40f;

    /// <summary>
    ///     Name of the container slot used as the gun's chamber
    /// </summary>
    public const string ChamberSlot = "gun_chamber";

    /// <summary>
    ///     Name of the container slot used as the gun's magazine
    /// </summary>
    public const string MagazineSlot = "gun_magazine";

    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    private const float InteractNextFire = 0.3f;
    private const double SafetyNextFire = 0.5;
    private const float EjectOffset = 0.4f;
    protected const string AmmoExamineColor = "yellow";
    protected const string FireRateExamineColor = "yellow";
    public const string ModeExamineColor = "cyan";

    public override void Initialize()
    {
        SubscribeAllEvent<RequestShootEvent>(OnShootRequest);
        SubscribeAllEvent<RequestStopShootEvent>(OnStopShootRequest);
        SubscribeLocalEvent<GunComponent, MeleeHitEvent>(OnGunMelee);

        // Ammo providers
        InitializeBallistic();
        InitializeBattery();
        InitializeCartridge();
        InitializeChamberMagazine();
        InitializeMagazine();
        InitializeRevolver();
        InitializeBasicEntity();
        InitializeClothing();
        InitializeContainer();
        InitializeSolution();
        InitializeMech(); //Starlight

        // Interactions
        SubscribeLocalEvent<GunComponent, GetVerbsEvent<AlternativeVerb>>(OnAltVerb);
        SubscribeLocalEvent<GunComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<GunComponent, CycleModeEvent>(OnCycleMode);
        SubscribeLocalEvent<GunComponent, HandSelectedEvent>(OnGunSelected);
        SubscribeLocalEvent<GunComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<GunComponent> gun, ref MapInitEvent args)
    {
#if DEBUG
        if (gun.Comp.NextFire > Timing.CurTime)
            Log.Warning($"Initializing a map that contains an entity that is on cooldown. Entity: {ToPrettyString(gun)}");

        DebugTools.Assert((gun.Comp.AvailableModes & gun.Comp.SelectedMode) != 0x0);
#endif

        RefreshModifiers((gun, gun));
    }

    private void OnGunMelee(Entity<GunComponent> ent, ref MeleeHitEvent args)
    {
        if (!TryComp<MeleeWeaponComponent>(ent, out var melee))
            return;

        if (melee.NextAttack > ent.Comp.NextFire)
        {
            ent.Comp.NextFire = melee.NextAttack;
            DirtyField(ent.AsNullable(), nameof(GunComponent.NextFire));
        }
    }

    private void OnShootRequest(RequestShootEvent msg, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.AttachedEntity;

        if (user == null ||
            !_combatMode.IsInCombatMode(user) ||
            !TryGetGun(user.Value, out var gun))
        {
            return;
        }

        // 🌟Starlight🌟 — in dual-wield mode, TryGetGun already picks the correct alternating gun;
        // skip the gun-ID match check so the server fires the right gun even when the client's
        // NextIsLeft state hasn't synced back yet.
        var isDualWield = TryComp<DualWieldComponent>(user.Value, out var dualWield) && dualWield.Active;
        if (!isDualWield && gun.Owner != GetEntity(msg.Gun))
            return;

        if (TryComp(user, out VentCrawlerComponent? crawlerComp) //🌟Starlight🌟
            && crawlerComp.InTube == true)
            return;

        gun.Comp.ShootCoordinates = GetCoordinates(msg.Coordinates);
        gun.Comp.Target = GetEntity(msg.Target);
        var fired = AttemptShoot(user.Value, gun);

        // 🌟Starlight🌟 — dual-wield: only alternate after an actual shot so both guns stay in sync
        if (isDualWield && fired)
        {
            dualWield!.NextIsLeft = !dualWield.NextIsLeft;
            Dirty(user.Value, dualWield);
        }
    }

    private void OnStopShootRequest(RequestStopShootEvent ev, EntitySessionEventArgs args)
    {
        var gunUid = GetEntity(ev.Gun);

        if (args.SenderSession.AttachedEntity == null ||
            !TryComp<GunComponent>(gunUid, out var gun) ||
            !TryGetGun(args.SenderSession.AttachedEntity.Value, out var userGun))
        {
            return;
        }

        if (userGun != (gunUid, gun))
            return;

        StopShooting(userGun);
    }

    public bool CanShoot(GunComponent component)
    {
        if (component.NextFire > Timing.CurTime)
            return false;

        return true;
    }

    /// <summary>
    ///     Tries to get an entity with <see cref="GunComponent"/> from the specified entity's hands, or from the entity itself.
    /// </summary>
    /// <param name="entity">Entity that is holding the gun, or is the gun</param>
    /// <param name="gun">Gun entity to return</param>
    /// <returns>True if gun was found</returns>
    public bool TryGetGun(EntityUid entity, out Entity<GunComponent> gun)
    {
        gun = default;

        // 🌟Starlight🌟 — dual-wield: return the alternating gun instead of the active-hand gun
        if (TryComp<DualWieldComponent>(entity, out var dualWieldComp) && dualWieldComp.Active)
        {
            var dwGunUid = dualWieldComp.NextIsLeft ? dualWieldComp.LeftGun : dualWieldComp.RightGun;
            if (TryComp<GunComponent>(dwGunUid, out var dwGunComp))
            {
                gun = (dwGunUid, dwGunComp);
                return true;
            }
            // Gun no longer valid — disable dual-wield and fall through
            dualWieldComp.Active = false;
            Dirty(entity, dualWieldComp);
        }

        if (_hands.GetActiveItem(entity) is { } held &&
            TryComp(held, out GunComponent? gunComp))
        {
            gun = (held, gunComp);
            return true;
        }

        // Starlight-edit: start
        if (TryComp<MechPilotComponent>(entity, out var pilot) &&
            TryComp(pilot.Mech, out MechComponent? mechFromPilot) &&
            mechFromPilot.CurrentSelectedEquipment is { } equipFromPilot &&
            TryComp(equipFromPilot, out gunComp))
        {
            gun = (equipFromPilot, gunComp);
            return true;
        }

        if (TryComp<MechComponent>(entity, out var mech) &&
            mech.CurrentSelectedEquipment is { } equip &&
            TryComp(equip, out gunComp))
        {
            gun = (equip, gunComp);
            return true;
        }
        // Starlight-edit: end

        // Last resort is check if the entity itself is a gun.
        if (TryComp(entity, out gunComp))
        {
            gun = (entity, gunComp);
            return true;
        }

        return false;
    }

    private void StopShooting(Entity<GunComponent> ent)
    {
        if (ent.Comp.ShotCounter == 0)
            return;

        ent.Comp.ShotCounter = 0;
        ent.Comp.ShootCoordinates = null;
        ent.Comp.Target = null;
        DirtyField(ent.AsNullable(), nameof(GunComponent.ShotCounter));
    }

    /// <summary>
    /// Attempts to shoot at the target coordinates. Resets the shot counter after every shot.
    /// </summary>
    public bool AttemptShoot(EntityUid user, Entity<GunComponent> gun, EntityCoordinates toCoordinates, EntityUid? target = null)
    {
        gun.Comp.ShootCoordinates = toCoordinates;
        gun.Comp.Target = target;
        var result = AttemptShoot(user, gun);
        gun.Comp.ShotCounter = 0;
        DirtyField(gun.AsNullable(), nameof(GunComponent.ShotCounter));
        return result;
    }

    /// <summary>
    /// Shoots by assuming the gun is the user at default coordinates.
    /// </summary>
    public bool AttemptShoot(Entity<GunComponent> gun)
    {
        var coordinates = new EntityCoordinates(gun, gun.Comp.DefaultDirection);
        gun.Comp.ShootCoordinates = coordinates;
        var result = AttemptShoot(gun, gun);
        gun.Comp.ShotCounter = 0;
        return result;
    }

    private bool AttemptShoot(EntityUid user, Entity<GunComponent> gun)
    {
        if (gun.Comp.FireRateModified <= 0f ||
            !_actionBlockerSystem.CanAttack(user))
        {
            return false;
        }

        var toCoordinates = gun.Comp.ShootCoordinates;

        if (toCoordinates == null)
            return false;

        var curTime = Timing.CurTime;

        // check if anything wants to prevent shooting
        var prevention = new ShotAttemptedEvent
        {
            User = user,
            Used = gun
        };
        RaiseLocalEvent(gun, ref prevention);
        if (prevention.Cancelled)
            return false;

        RaiseLocalEvent(user, ref prevention);
        if (prevention.Cancelled)
            return false;

        // Need to do this to play the clicking sound for empty automatic weapons
        // but not play anything for burst fire.
        if (gun.Comp.NextFire > curTime)
            return false;

        var fireRate = TimeSpan.FromSeconds(1f / gun.Comp.FireRateModified);

        if (gun.Comp.SelectedMode == SelectiveFire.Burst || gun.Comp.BurstActivated)
            fireRate = TimeSpan.FromSeconds(1f / gun.Comp.BurstFireRate);

        // First shot
        // Previously we checked shotcounter but in some cases all the bullets got dumped at once
        // curTime - fireRate is insufficient because if you time it just right you can get a 3rd shot out slightly quicker.
        if (gun.Comp.NextFire < curTime - fireRate || gun.Comp.ShotCounter == 0 && gun.Comp.NextFire < curTime)
            gun.Comp.NextFire = curTime;

        var shots = 0;
        var lastFire = gun.Comp.NextFire;

        while (gun.Comp.NextFire <= curTime)
        {
            gun.Comp.NextFire += fireRate;
            shots++;
        }

        // NextFire has been touched regardless so need to dirty the gun.
        DirtyField(gun.AsNullable(), nameof(GunComponent.NextFire));

        // Get how many shots we're actually allowed to make, due to clip size or otherwise.
        // Don't do this in the loop so we still reset NextFire.
        if (!gun.Comp.BurstActivated)
        {
            switch (gun.Comp.SelectedMode)
            {
                case SelectiveFire.SemiAuto:
                    shots = Math.Min(shots, 1 - gun.Comp.ShotCounter);
                    break;
                case SelectiveFire.Burst:
                    shots = Math.Min(shots, gun.Comp.ShotsPerBurstModified - gun.Comp.ShotCounter);
                    break;
                case SelectiveFire.FullAuto:
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"No implemented shooting behavior for {gun.Comp.SelectedMode}!");
            }
        }
        else
        {
            shots = Math.Min(shots, gun.Comp.ShotsPerBurstModified - gun.Comp.ShotCounter);
        }

        var attemptEv = new AttemptShootEvent(user, null);
        RaiseLocalEvent(gun, ref attemptEv);

        if (attemptEv.Cancelled)
        {
            if (attemptEv.Message != null)
            {
                PopupSystem.PopupClient(attemptEv.Message, gun, user);
            }
            gun.Comp.BurstActivated = false;
            gun.Comp.BurstShotsCount = 0;
            gun.Comp.NextFire = TimeSpan.FromSeconds(Math.Max(lastFire.TotalSeconds + SafetyNextFire, gun.Comp.NextFire.TotalSeconds));
            return false;
        }

        var fromCoordinates = Transform(user).Coordinates;
        // Remove ammo
        var ev = new TakeAmmoEvent(shots, [], fromCoordinates, user);

        // Listen it just makes the other code around it easier if shots == 0 to do this.
        if (shots > 0)
            RaiseLocalEvent(gun, ev);

        DebugTools.Assert(ev.Ammo.Count <= shots);
        DebugTools.Assert(shots >= 0);
        UpdateAmmoCount(gun);

        // Even if we don't actually shoot update the ShotCounter. This is to avoid spamming empty sounds
        // where the gun may be SemiAuto or Burst.
        gun.Comp.ShotCounter += shots;
        DirtyField(gun.AsNullable(), nameof(GunComponent.ShotCounter));

        if (ev.Ammo.Count <= 0)
        {
            // triggers effects on the gun if it's empty
            var emptyGunShotEvent = new OnEmptyGunShotEvent(user);
            RaiseLocalEvent(gun, ref emptyGunShotEvent);

            gun.Comp.BurstActivated = false;
            gun.Comp.BurstShotsCount = 0;
            gun.Comp.NextFire += TimeSpan.FromSeconds(gun.Comp.BurstCooldown);

            // Play empty gun sounds if relevant
            // If they're firing an existing clip then don't play anything.
            if (shots > 0)
            {
                PopupSystem.PopupCursor(ev.Reason ?? Loc.GetString("gun-magazine-fired-empty"));

                // Don't spam safety sounds at gun fire rate, play it at a reduced rate.
                // May cause prediction issues? Needs more tweaking
                gun.Comp.NextFire = TimeSpan.FromSeconds(Math.Max(lastFire.TotalSeconds + SafetyNextFire, gun.Comp.NextFire.TotalSeconds));
                Audio.PlayPredicted(gun.Comp.SoundEmpty, gun, user);
                return false;
            }

            return false;
        }

        //Starlight start
        var NonEmptyGunShotEvent = new OnNonEmptyGunShotEvent(user, ev.Ammo);
        RaiseLocalEvent(gun, ref NonEmptyGunShotEvent);
        //starlight end

        // Handle burstfire
        if (gun.Comp.SelectedMode == SelectiveFire.Burst)
        {
            gun.Comp.BurstActivated = true;
        }
        if (gun.Comp.BurstActivated)
        {
            gun.Comp.BurstShotsCount += shots;
            if (gun.Comp.BurstShotsCount >= gun.Comp.ShotsPerBurstModified)
            {
                gun.Comp.NextFire += TimeSpan.FromSeconds(gun.Comp.BurstCooldown);
                gun.Comp.BurstActivated = false;
                gun.Comp.BurstShotsCount = 0;
            }
        }

        // Shoot confirmed - sounds also played here in case it's invalid (e.g. cartridge already spent).
        Shoot(gun, ev.Ammo, fromCoordinates, toCoordinates.Value, out var userImpulse, out var fired, user, throwItems: attemptEv.ThrowItems);
        var shotEv = new GunShotEvent(user, ev.Ammo);
        RaiseLocalEvent(gun, ref shotEv);

        //Starlight begin | ES Screenshake
        if (fired)
        {
            var gunShakeRotation = new ScreenshakeParameters()
            {
                Trauma = 0.035f * gun.Comp.CameraRecoilScalarModified,
                DecayRate = 1.2f,
                Frequency = 0.008f
            };
            _shake.Screenshake(user, null, gunShakeRotation);
        }
        //Starlight end
        
        if (!userImpulse || !TryComp<PhysicsComponent>(user, out var userPhysics))
            return true;

        var shooterEv = new ShooterImpulseEvent();
        RaiseLocalEvent(user, ref shooterEv);

        if (shooterEv.Push)
            CauseImpulse(fromCoordinates, toCoordinates.Value, (user, userPhysics));
        return true;
    }

    public void Shoot(
        Entity<GunComponent> gun,
        EntityUid ammo,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        out bool userImpulse,
        out bool fired, // Starlight-edit
        EntityUid? user = null,
        bool throwItems = false)
    {
        var shootable = EnsureShootable(ammo);
        Shoot(gun, new List<(EntityUid? Entity, IShootable Shootable)>(1) { (ammo, shootable) }, fromCoordinates, toCoordinates, out userImpulse, out fired, user, throwItems); // Starlight-edit
    }

    public abstract void Shoot(
        Entity<GunComponent> gun,
        List<(EntityUid? Entity, IShootable Shootable)> ammo,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        out bool userImpulse,
        out bool fired, // Starlight-edit
        EntityUid? user = null,
        bool throwItems = false);

    public void ShootProjectile(EntityUid uid, Vector2 direction, Vector2 gunVelocity, EntityUid? gunUid, EntityUid? user = null, float speed = ProjectileSpeed)
    {
        var physics = EnsureComp<PhysicsComponent>(uid);
        Physics.SetBodyStatus(uid, physics, BodyStatus.InAir);

        var targetMapVelocity = gunVelocity + direction.Normalized() * speed;
        var currentMapVelocity = Physics.GetMapLinearVelocity(uid, physics);
        var finalLinear = physics.LinearVelocity + targetMapVelocity - currentMapVelocity;
        Physics.SetLinearVelocity(uid, finalLinear, body: physics);

        var projectile = EnsureComp<ProjectileComponent>(uid);
        projectile.Weapon = gunUid;

        // Starlight-edit: start
        EntityUid? shooter = null;
        if (user != null && TryComp<MechPilotComponent>(user.Value, out var pilotComp))
        {
            shooter = pilotComp.Mech;
        }
        else
        {
            shooter = user ?? gunUid;
        }
        // Starlight-edit: end

        if (shooter != null)
            Projectiles.SetShooter(uid, projectile, shooter.Value);

        TransformSystem.SetWorldRotation(uid, direction.ToWorldAngle() + projectile.Angle);
    }

    protected abstract void Popup(string message, EntityUid? uid, EntityUid? user);

    /// <summary>
    /// Call this whenever the ammo count for a gun changes.
    /// </summary>
    protected virtual void UpdateAmmoCount(EntityUid uid, bool prediction = true) { }

    protected void SetCartridgeSpent(EntityUid uid, CartridgeAmmoComponent cartridge, bool spent)
    {
        if (cartridge.Spent != spent)
            DirtyField(uid, cartridge, nameof(CartridgeAmmoComponent.Spent));

        cartridge.Spent = spent;
        Appearance.SetData(uid, AmmoVisuals.Spent, spent);

        if (!cartridge.MarkSpentAsTrash)
            return;

        if (spent)
            TagSystem.AddTag(uid, TrashTag);
        else
            TagSystem.RemoveTag(uid, TrashTag);
    }

    #region Starlight
    public bool IsChamberClosed(EntityUid gunEntity)
        => Appearance.TryGetData(gunEntity, AmmoVisuals.BoltClosed, out bool boltClosed) && boltClosed;
    #endregion

    /// <summary>
    /// Drops a single cartridge / shell
    /// </summary>
    protected void EjectCartridge(
        EntityUid entity,
        Angle? angle = null,
        bool playSound = true)
    {
        // TODO: Sound limit version.
        var offsetPos = Random.NextVector2(EjectOffset);
        var xform = Transform(entity);
        // Starlight-edit: start
        TransformSystem.AttachToGridOrMap(entity, xform);
        var coordinates = xform.Coordinates.Offset(offsetPos);
        // Starlight-edit: end

        TransformSystem.SetLocalRotation(entity, Random.NextAngle(), xform);
        TransformSystem.SetCoordinates(entity, xform, coordinates);

        // decides direction the casing ejects and only when not cycling
        if (angle != null)
        {
            var ejectAngle = angle.Value;
            ejectAngle += 3.7f; // 212 degrees; casings should eject slightly to the right and behind of a gun
            ThrowingSystem.TryThrow(entity, ejectAngle.ToVec().Normalized() / 100, 5f);
        }
        if (playSound && TryComp<CartridgeAmmoComponent>(entity, out var cartridge))
        {
            Audio.PlayPvs(cartridge.EjectSound, entity, AudioParams.Default.WithVariation(SharedContentAudioSystem.DefaultVariation).WithVolume(-1f));
        }
    }

    protected IShootable EnsureShootable(EntityUid uid)
    {
        if (TryComp<CartridgeAmmoComponent>(uid, out var cartridge))
            return cartridge;

        if (TryComp<HitscanAmmoComponent>(uid, out var hitscanAmmo))
            return hitscanAmmo;

        return EnsureComp<AmmoComponent>(uid);
    }

    protected void RemoveShootable(EntityUid uid)
    {
        RemCompDeferred<CartridgeAmmoComponent>(uid);
        RemCompDeferred<AmmoComponent>(uid);
    }

    protected void MuzzleFlash(EntityUid gun, AmmoComponent component, Angle worldAngle, EntityUid? user = null)
    {
        var attemptEv = new GunMuzzleFlashAttemptEvent();
        RaiseLocalEvent(gun, ref attemptEv);
        if (attemptEv.Cancelled)
            return;

        var sprite = component.MuzzleFlash;

        if (sprite == null)
            return;

        var ev = new MuzzleFlashEvent(GetNetEntity(gun), sprite, worldAngle);
        CreateEffect(gun, ev, user);
    }

    public void CauseImpulse(EntityCoordinates fromCoordinates, EntityCoordinates toCoordinates, Entity<PhysicsComponent> user)
    {
        var fromMap = TransformSystem.ToMapCoordinates(fromCoordinates).Position;
        var toMap = TransformSystem.ToMapCoordinates(toCoordinates).Position;
        var shotDirection = (toMap - fromMap).Normalized();

        const float impulseStrength = 25.0f;
        var impulseVector = shotDirection * impulseStrength;
        Physics.ApplyLinearImpulse(user, -impulseVector, body: user.Comp);
    }

    public void RefreshModifiers(Entity<GunComponent?> gun)
    {
        if (!Resolve(gun, ref gun.Comp))
            return;

        var comp = gun.Comp;
        var ev = new GunRefreshModifiersEvent(
            (gun, comp),
            comp.SoundGunshot,
            comp.CameraRecoilScalar,
            comp.AngleIncrease,
            comp.AngleDecay,
            comp.MaxAngle,
            comp.MinAngle,
            comp.ShotsPerBurst,
            comp.FireRate,
            comp.ProjectileSpeed
        );

        RaiseLocalEvent(gun, ref ev);

        if (comp.SoundGunshotModified != ev.SoundGunshot)
        {
            comp.SoundGunshotModified = ev.SoundGunshot;
            DirtyField(gun, nameof(GunComponent.SoundGunshotModified));
        }

        if (!MathHelper.CloseTo(comp.CameraRecoilScalarModified, ev.CameraRecoilScalar))
        {
            comp.CameraRecoilScalarModified = ev.CameraRecoilScalar;
            DirtyField(gun, nameof(GunComponent.CameraRecoilScalarModified));
        }

        if (!comp.AngleIncreaseModified.EqualsApprox(ev.AngleIncrease))
        {
            comp.AngleIncreaseModified = ev.AngleIncrease;
            DirtyField(gun, nameof(GunComponent.AngleIncreaseModified));
        }

        if (!comp.AngleDecayModified.EqualsApprox(ev.AngleDecay))
        {
            comp.AngleDecayModified = ev.AngleDecay;
            DirtyField(gun, nameof(GunComponent.AngleDecayModified));
        }

        if (!comp.MaxAngleModified.EqualsApprox(ev.MaxAngle))
        {
            comp.MaxAngleModified = ev.MaxAngle;
            DirtyField(gun, nameof(GunComponent.MaxAngleModified));
        }

        if (!comp.MinAngleModified.EqualsApprox(ev.MinAngle))
        {
            comp.MinAngleModified = ev.MinAngle;
            DirtyField(gun, nameof(GunComponent.MinAngleModified));
        }

        if (comp.ShotsPerBurstModified != ev.ShotsPerBurst)
        {
            comp.ShotsPerBurstModified = ev.ShotsPerBurst;
            DirtyField(gun, nameof(GunComponent.ShotsPerBurstModified));
        }

        if (!MathHelper.CloseTo(comp.FireRateModified, ev.FireRate))
        {
            comp.FireRateModified = ev.FireRate;
            DirtyField(gun, nameof(GunComponent.FireRateModified));
        }

        if (!MathHelper.CloseTo(comp.ProjectileSpeedModified, ev.ProjectileSpeed))
        {
            comp.ProjectileSpeedModified = ev.ProjectileSpeed;
            DirtyField(gun, nameof(GunComponent.ProjectileSpeedModified));
        }
    }

    protected abstract void CreateEffect(EntityUid gunUid, MuzzleFlashEvent message, EntityUid? user = null);

    public abstract void PlayImpactSound(EntityUid otherEntity, DamageSpecifier? modifiedDamage, SoundSpecifier? weaponSound, bool forceWeaponSound);

    /// <summary>
    /// Used for animated effects on the client.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class HitscanEvent : EntityEventArgs
    {
        // Starlight - comment out the upstream Sprites list in favor of tracking the hitscan and its traces
        //public List<(NetCoordinates coordinates, Angle angle, SpriteSpecifier Sprite, float Distance)> Sprites = [];
        // Starilght start - we add the traces block below, and use that instead of the sprite list above.
        public required List<HitscanTrace> Traces;

        // The following set of properties are copied from the HitscanBasicVisualsComponent
        public SpriteSpecifier? MuzzleFlash;
        public SpriteSpecifier? TravelFlash;
        public SpriteSpecifier? ImpactFlash;
        public ExtendedSpriteSpecifier? Bullet;
        public required float Speed;
    }

    /// <summary>
    /// Get the ammo count for a given EntityUid. Can be a firearm or magazine.
    /// </summary>
    public int GetAmmoCount(EntityUid uid)
    {
        var ammoEv = new GetAmmoCountEvent();
        RaiseLocalEvent(uid, ref ammoEv);
        return ammoEv.Count;
    }

    /// <summary>
    /// Get the ammo capacity for a given EntityUid. Can be a firearm or magazine.
    /// </summary>
    public int GetAmmoCapacity(EntityUid uid)
    {
        var ammoEv = new GetAmmoCountEvent();
        RaiseLocalEvent(uid, ref ammoEv);
        return ammoEv.Capacity;
    }

    public override void Update(float frameTime)
    {
        UpdateBattery(frameTime);
        UpdateBallistic(frameTime);
    }
}

/// <summary>
///     Raised directed on the gun before firing to see if the shot should go through.
/// </summary>
/// <remarks>
///     Handling this in server exclusively will lead to mispredicts.
/// </remarks>
/// <param name="User">The user that attempted to fire this gun.</param>
/// <param name="Cancelled">Set this to true if the shot should be cancelled.</param>
/// <param name="ThrowItems">Set this to true if the ammo shouldn't actually be fired, just thrown.</param>
[ByRefEvent]
public record struct AttemptShootEvent(EntityUid User, string? Message, bool Cancelled = false, bool ThrowItems = false);

/// <summary>
///     Raised directed on the gun after firing.
/// </summary>
/// <param name="User">The user that fired this gun.</param>
[ByRefEvent]
public record struct GunShotEvent(EntityUid User, List<(EntityUid? Uid, IShootable Shootable)> Ammo);

//starlight start
[ByRefEvent]
public record struct OnNonEmptyGunShotEvent(EntityUid User, List<(EntityUid? Uid, IShootable Shootable)> Ammo);
//starlight end

/// <summary>
/// Raised on an entity after firing a gun to see if any components or systems would allow this entity to be pushed
/// by the gun they're firing. If true, GunSystem will create an impulse on our entity.
/// </summary>
[ByRefEvent]
public record struct ShooterImpulseEvent()
{
    public bool Push;
};

public enum EffectLayers : byte
{
    Unshaded,
}

[Serializable, NetSerializable]
public enum AmmoVisuals : byte
{
    Spent,
    AmmoCount,
    AmmoMax,
    HasAmmo, // used for generic visualizers. c# stuff can just check ammocount != 0
    MagLoaded,
    BoltClosed,
}
