using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Hands;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Content.Shared.Examine;
using Content.Shared.Localizations;

#region Starlight
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared._Starlight.Abstract.Extensions;
using Robust.Shared.Timing;
#endregion

namespace Content.Shared.Weapons.Reflect;

/// <summary>
/// This handles reflecting projectiles and hitscan shots.
/// </summary>
public sealed partial class ReflectSystem : EntitySystem
{
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    #region Starlight
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    #endregion

    public override void Initialize()
    {
        base.Initialize();

        Subs.SubscribeWithRelay<ReflectComponent, ProjectileReflectAttemptEvent>(OnReflectUserCollide, baseEvent: false);
        Subs.SubscribeWithRelay<ReflectComponent, HitScanReflectAttemptEvent>(OnReflectUserHitscan, baseEvent: false);
        SubscribeLocalEvent<ReflectComponent, ProjectileReflectAttemptEvent>(OnReflectCollide);
        SubscribeLocalEvent<ReflectComponent, HitScanReflectAttemptEvent>(OnReflectHitscan);

        SubscribeLocalEvent<ReflectComponent, GotEquippedEvent>(OnReflectEquipped);
        SubscribeLocalEvent<ReflectComponent, GotUnequippedEvent>(OnReflectUnequipped);
        SubscribeLocalEvent<ReflectComponent, GotEquippedHandEvent>(OnReflectHandEquipped);
        SubscribeLocalEvent<ReflectComponent, GotUnequippedHandEvent>(OnReflectHandUnequipped);
        SubscribeLocalEvent<ReflectComponent, ExaminedEvent>(OnExamine);
    }

    private void OnReflectUserCollide(Entity<ReflectComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!ent.Comp.InRightPlace)
            return; // only reflect when equipped correctly

        if (TryReflectProjectile(ent, ent.Owner, args.ProjUid))
            args.Cancelled = true;
    }

    private void OnReflectUserHitscan(Entity<ReflectComponent> ent, ref HitScanReflectAttemptEvent args)
    {
        if (args.Reflected)
            return;

        if (!ent.Comp.InRightPlace)
            return; // only reflect when equipped correctly

        if (TryReflectHitscan(ent, ent.Owner, args.Shooter, args.SourceItem, args.Direction, args.Reflective, args.HitscanId, out var dir)) //STARLIGHT
        {
            args.Direction = dir.Value;
            args.Reflected = true;
        }
    }

    private void OnReflectCollide(Entity<ReflectComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (TryReflectProjectile(ent, ent.Owner, args.ProjUid))
            args.Cancelled = true;
    }

    private void OnReflectHitscan(Entity<ReflectComponent> ent, ref HitScanReflectAttemptEvent args)
    {
        if (args.Reflected)
            return;

        if (TryReflectHitscan(ent, ent.Owner, args.Shooter, args.SourceItem, args.Direction, args.Reflective, args.HitscanId, out var dir)) //STARLIGHT
        {
            args.Direction = dir.Value;
            args.Reflected = true;
        }
    }

    private bool TryReflectProjectile(Entity<ReflectComponent> reflector, EntityUid user, Entity<ProjectileComponent?> projectile)
    {
        if (!TryComp<ReflectiveComponent>(projectile, out var reflective) ||
            (reflector.Comp.Reflects & reflective.Reflective) == 0x0 ||
            !_toggle.IsActivated(reflector.Owner) ||
            !TryComp<PhysicsComponent>(projectile, out var physics))
        {
            return false;
        }

        #region 🌟Starlight🌟
        var availableEnergy = 0;
        if (HasComp<PowerCellSlotComponent>(reflector.Owner)) //if the shield has a battery slot, then we consume charge to perform the reflection
        {
            availableEnergy = _powerCell.GetRemainingUses(reflector.Owner, reflector.Comp.ReflectEnergyDraw);
            if (availableEnergy <= 0)
                return false;
        }

        var reflectionChance = reflector.Comp.ReflectProb;

        // Check for enhanced reflection against specific projectile types
        var metaData = MetaData(projectile);
        if (metaData.EntityPrototype != null)
        {
            var projectileId = metaData.EntityPrototype.ID;
            if (reflector.Comp.EnhancedReflection.TryGetValue(projectileId, out var enhancedChance))
            {
                reflectionChance = enhancedChance;
            }
        }

        if (!_random.Prob(reflectionChance))
        {
            return false;
        }

        if (availableEnergy > 0 && !_powerCell.TryUseCharge(reflector.Owner, reflector.Comp.ReflectEnergyDraw, user: user))
            return false; // if no battery or no charge, doesn't work and reflect fails

        if (reflector.Comp.OverrideAngle is not null)
        {
            var overrideAngle = _transform.GetWorldRotation(reflector) + reflector.Comp.OverrideAngle.Value;

            var existingVelocity = _physics.GetMapLinearVelocity(projectile, component: physics);
            var relativeVelocity = existingVelocity - _physics.GetMapLinearVelocity(user);
            var speed = relativeVelocity.Length();

            var newVelocity = new Vector2((float)Math.Cos(overrideAngle), (float)Math.Sin(overrideAngle)) * speed;

            var difference = newVelocity - existingVelocity;
            _physics.SetLinearVelocity(projectile, physics.LinearVelocity + difference, body: physics);
            var velocityAngle = (float)Math.Atan2(newVelocity.Y, newVelocity.X);
            _transform.SetWorldRotation(projectile, velocityAngle - reflector.Comp.OverrideAngle.Value);
        }
        else
        {
            var rotation = _random.NextAngle(-reflector.Comp.Spread / 2, reflector.Comp.Spread / 2).Opposite();
            var existingVelocity = _physics.GetMapLinearVelocity(projectile, component: physics);
            var relativeVelocity = existingVelocity - _physics.GetMapLinearVelocity(user);
            var newVelocity = rotation.RotateVec(relativeVelocity);

            var difference = newVelocity - existingVelocity;

            _physics.SetLinearVelocity(projectile, physics.LinearVelocity + difference, body: physics);

            var locRot = Transform(projectile).LocalRotation;
            var newRot = rotation.RotateVec(locRot.ToVec());
            _transform.SetLocalRotation(projectile, newRot.ToAngle());
        }
        #endregion

        PlayAudioAndPopup(reflector.Comp, user);

        if (Resolve(projectile, ref projectile.Comp, false))
        {
            _adminLogger.Add(LogType.BulletHit, LogImpact.Medium, $"{ToPrettyString(user)} reflected {ToPrettyString(projectile)} from {ToPrettyString(projectile.Comp.Weapon)} shot by {projectile.Comp.Shooter}");

            projectile.Comp.Shooter = user;
            projectile.Comp.Weapon = user;
            Dirty(projectile, projectile.Comp);
        }
        else
        {
            _adminLogger.Add(LogType.BulletHit, LogImpact.Medium, $"{ToPrettyString(user)} reflected {ToPrettyString(projectile)}");
        }

        return true;
    }
    private bool TryReflectHitscan(
        Entity<ReflectComponent> reflector,
        EntityUid user,
        EntityUid? shooter,
        EntityUid shotSource,
        Vector2 direction,
        ReflectType hitscanReflectType,
        // 🌟Starlight🌟 start
        string? hitscanId,
        [NotNullWhen(true)] out Vector2? newDirection)
    {
        newDirection = null; //Starlight
        if ((reflector.Comp.Reflects & hitscanReflectType) == 0x0 ||
            !_toggle.IsActivated(reflector.Owner))
            return false;

        // Get reflection probability - check for enhanced reflection against specific bullet types
        var reflectionChance = reflector.Comp.ReflectProb;

        // Check for enhanced reflection against specific bullet types
        if (hitscanId != null && reflector.Comp.EnhancedReflection.TryGetValue(hitscanId, out var enhancedChance))
            reflectionChance = enhancedChance;

        var availableEnergy = 0;
        if (HasComp<PowerCellSlotComponent>(reflector.Owner)) //if the shield has a battery slot, then we consume charge to perform the reflection
        {
            availableEnergy = _powerCell.GetRemainingUses(reflector.Owner, reflector.Comp.ReflectEnergyDraw);
            if (availableEnergy <= 0)
                return false;
        }

        if (!_random.ProbPredicted(_timing, reflectionChance))
            return false;

        if (availableEnergy > 0 && !_powerCell.TryUseCharge(reflector.Owner, reflector.Comp.ReflectEnergyDraw, user: user))
            return false; // if no battery or no charge, doesn't work and reflect fails

        PlayAudioAndPopup(reflector.Comp, user);

        if (reflector.Comp.OverrideAngle is { } newAngle)
        {
            var overrideAngle = _transform.GetWorldRotation(reflector) + newAngle;
            newDirection = new Vector2((float)Math.Cos(overrideAngle), (float)Math.Sin(overrideAngle));
            newDirection = newDirection.Value.Normalized();
        }
        else
        {
            var spread = _random.NextAngle(-reflector.Comp.Spread / 2, reflector.Comp.Spread / 2);
            newDirection = -spread.RotateVec(direction);
        }
        // 🌟Starlight🌟 end

        if (shooter != null)
            _adminLogger.Add(LogType.HitScanHit, LogImpact.Medium, $"{ToPrettyString(user)} reflected hitscan from {ToPrettyString(shotSource)} shot by {ToPrettyString(shooter.Value)}");
        else
            _adminLogger.Add(LogType.HitScanHit, LogImpact.Medium, $"{ToPrettyString(user)} reflected hitscan from {ToPrettyString(shotSource)}");

        return true;
    }

    private void PlayAudioAndPopup(ReflectComponent reflect, EntityUid user)
    {
        // Can probably be changed for prediction
        if (_netManager.IsServer)
        {
            _popup.PopupEntity(Loc.GetString("reflect-shot"), user);
            _audio.PlayPvs(reflect.SoundOnReflect, user);
        }
    }

    private void OnReflectEquipped(Entity<ReflectComponent> ent, ref GotEquippedEvent args)
    {
        ent.Comp.InRightPlace = (ent.Comp.SlotFlags & args.SlotFlags) == args.SlotFlags;
        Dirty(ent);
    }

    private void OnReflectUnequipped(Entity<ReflectComponent> ent, ref GotUnequippedEvent args)
    {
        ent.Comp.InRightPlace = false;
        Dirty(ent);
    }

    private void OnReflectHandEquipped(Entity<ReflectComponent> ent, ref GotEquippedHandEvent args)
    {
        ent.Comp.InRightPlace = ent.Comp.ReflectingInHands;
        Dirty(ent);
    }

    private void OnReflectHandUnequipped(Entity<ReflectComponent> ent, ref GotUnequippedHandEvent args)
    {
        ent.Comp.InRightPlace = false;
        Dirty(ent);
    }

    #region Examine
    private void OnExamine(Entity<ReflectComponent> ent, ref ExaminedEvent args)
    {
        // This isn't examine verb or something just because it looks too much bad.
        // Trust me, universal verb for the potential weapons, armor and walls looks awful.
        var value = MathF.Round(ent.Comp.ReflectProb * 100, 1);

        if (!_toggle.IsActivated(ent.Owner) || value == 0 || ent.Comp.Reflects == ReflectType.None)
            return;

        var compTypes = ent.Comp.Reflects.ToString().Split(", ");

        List<string> typeList = new(compTypes.Length);

        for (var i = 0; i < compTypes.Length; i++)
        {
            var type = Loc.GetString(("reflect-component-" + compTypes[i]).ToLower());
            typeList.Add(type);
        }

        var msg = ContentLocalizationManager.FormatList(typeList);

        args.PushMarkup(Loc.GetString("reflect-component-examine", ("value", value), ("type", msg)));
    }
    #endregion
}
