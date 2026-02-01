using System.Linq;
using System.Numerics;
using Content.Server.Actions;
using Content.Server.Destructible;
using Content.Shared._Starlight.Antags.Vampires;
using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Antags.Vampires.Components.Classes;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Ensnaring.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Prying.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Antags.Vampires.Systems;

public sealed class GargantuaSystem : EntitySystem
{
    private const string BloodSwellActionId = "ActionVampireBloodSwell";
    private const string BloodRushActionId = "ActionVampireBloodRush";
    private const string OverwhelmingForceActionId = "ActionVampireOverwhelmingForce";
    private const string ChargeActionId = "ActionVampireCharge";

    private static readonly ProtoId<DamageGroupPrototype> _bruteGroupId = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> _burnGroupId = "Burn";

    [Dependency] private readonly VampireSystem _vampire = default!;

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DestructibleSystem _destructible = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _rand = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireComponent, VampireBloodSwellActionEvent>(OnBloodSwell);
        SubscribeLocalEvent<VampireComponent, VampireBloodRushActionEvent>(OnBloodRush);
        SubscribeLocalEvent<VampireComponent, VampireSeismicStompActionEvent>(OnSeismicStomp);
        SubscribeLocalEvent<VampireComponent, VampireOverwhelmingForceActionEvent>(OnOverwhelmingForce);
        SubscribeLocalEvent<VampireComponent, VampireDemonicGraspActionEvent>(OnDemonicGrasp);
        SubscribeLocalEvent<VampireComponent, VampireChargeActionEvent>(OnCharge);

        SubscribeLocalEvent<GargantuaComponent, StartCollideEvent>(OnChargeCollide);
        SubscribeLocalEvent<GargantuaComponent, PullAttemptEvent>(OnPullAttempt);

        SubscribeLocalEvent<ActiveBloodSwellComponent, GetMeleeDamageEvent>(OnBloodSwellMeleeDamage);
        SubscribeLocalEvent<ActiveBloodRushComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<ActiveBloodSwellComponent, BeforeDamageChangedEvent>(OnBloodSwellIncomingDamage);
        SubscribeLocalEvent<ActiveBloodSwellComponent, BeforeStaminaDamageEvent>(OnBloodSwellStaminaDamage);

        SubscribeLocalEvent<GargantuaComponent, VampireBloodDrankEvent>(OnBloodDrank);
        // Status effects are raised on the status effect entity, so hook globally.
        SubscribeLocalEvent<StatusEffectComponent, StatusEffectAppliedEvent>(OnStatusEffectApplied);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var now = _timing.CurTime;

        var swellQuery = EntityQueryEnumerator<ActiveBloodSwellComponent>();
        while (swellQuery.MoveNext(out var uid, out var swell))
        {
            if (now >= swell.EndTime)
                EndBloodSwell(uid);
        }

        var rushQuery = EntityQueryEnumerator<ActiveBloodRushComponent>();
        while (rushQuery.MoveNext(out var uid, out var rush))
        {
            if (now >= rush.EndTime)
                EndBloodRush(uid);
        }

        var query = EntityQueryEnumerator<GargantuaComponent, VampireComponent>();
        while (query.MoveNext(out var uid, out var gargantua, out var vampire))
        {
            if (gargantua.IsCharging)
                ProcessChargeMovement(uid, gargantua, vampire);
        }
    }

    private void OnBloodDrank(EntityUid uid, GargantuaComponent _, ref VampireBloodDrankEvent args)
    {
        if (!TryComp<VampireComponent>(uid, out var vampire))
            return;

        if (vampire.TotalBlood < 300)
            return;

        var spec = new DamageSpecifier();
        spec += new DamageSpecifier(_proto.Index<DamageGroupPrototype>(_bruteGroupId), -FixedPoint2.New(3));
        spec += new DamageSpecifier(_proto.Index<DamageGroupPrototype>(_burnGroupId), -FixedPoint2.New(3));
        _damageableSystem.TryChangeDamage(uid, spec, true);
    }

    private bool TryGetVampireActionEvent<T>(VampireComponent vampire, string actionId, out T ev)
        where T : BaseActionEvent
    {
        ev = default!;

        if (!vampire.ActionEntities.TryGetValue(actionId, out var actionEntity))
            return false;

        if (_actions.GetEvent(actionEntity) is not T typed)
            return false;

        ev = typed;
        return true;
    }

    #region Blood Swell

    private void OnBloodSwell(EntityUid uid, VampireComponent comp, ref VampireBloodSwellActionEvent args)
    {
        if (args.Handled)
            return;

        if (!comp.ActionEntities.TryGetValue(BloodSwellActionId, out var actionEntity)
            || !HasComp<GargantuaComponent>(uid)
            || !_vampire.CheckAndConsumeBloodCost(uid, comp, actionEntity))
            return;

        var now = _timing.CurTime;
        var endTime = now + args.Duration;

        if (TryComp<ActiveBloodSwellComponent>(uid, out var active))
        {
            // Already active, refresh duration
            active.EndTime = endTime;
        }
        else
        {
            active = AddComp<ActiveBloodSwellComponent>(uid);
            active.EndTime = endTime;
            _popup.PopupEntity(Loc.GetString("vampire-blood-swell-start"), uid, uid);
        }

        _alerts.ShowAlert(uid, "VampireBloodSwell");
        args.Handled = true;
    }

    private void EndBloodSwell(EntityUid uid)
    {
        if (!RemComp<ActiveBloodSwellComponent>(uid))
            return;

        _alerts.ClearAlert(uid, "VampireBloodSwell");
        _popup.PopupEntity(Loc.GetString("vampire-blood-swell-end"), uid, uid);
    }

    private void OnBloodSwellMeleeDamage(EntityUid uid, ActiveBloodSwellComponent _, ref GetMeleeDamageEvent args)
    {
        if (!TryComp<VampireComponent>(uid, out var vampire))
            return;

        if (!TryGetVampireActionEvent<VampireBloodSwellActionEvent>(vampire, BloodSwellActionId, out var swellEv))
            return;

        if (args.Weapon != uid)
            return;

        // Bonus only for unarmed after 400 total blood
        if (vampire.TotalBlood < swellEv.EnhancedThreshold)
            return;

        args.Damage.DamageDict.TryGetValue("Blunt", out var blunt);
        args.Damage.DamageDict["Blunt"] = blunt + swellEv.MeleeBonusDamage;
    }

    #endregion

    #region Blood Rush

    private void OnBloodRush(EntityUid uid, VampireComponent comp, ref VampireBloodRushActionEvent args)
    {
        if (args.Handled)
            return;

        if (!comp.ActionEntities.TryGetValue(BloodRushActionId, out var actionEntity)
            || !HasComp<GargantuaComponent>(uid)
            || !_vampire.CheckAndConsumeBloodCost(uid, comp, actionEntity))
            return;

        var now = _timing.CurTime;
        var endTime = now + args.Duration;

        if (TryComp<ActiveBloodRushComponent>(uid, out var active))
        {
            // Already active, refresh duration
            active.EndTime = endTime;
            // Refresh movement speed modifiers to apply the buff
            _movement.RefreshMovementSpeedModifiers(uid);
        }
        else
        {
            active = AddComp<ActiveBloodRushComponent>(uid);
            active.EndTime = endTime;
            _movement.RefreshMovementSpeedModifiers(uid);
            _popup.PopupEntity(Loc.GetString("vampire-blood-rush-start"), uid, uid);
        }

        _alerts.ShowAlert(uid, "VampireBloodRush");
        args.Handled = true;
    }

    private void EndBloodRush(EntityUid uid)
    {
        if (!RemComp<ActiveBloodRushComponent>(uid))
            return;

        _alerts.ClearAlert(uid, "VampireBloodRush");
        _movement.RefreshMovementSpeedModifiers(uid);
        _popup.PopupEntity(Loc.GetString("vampire-blood-rush-end"), uid, uid);
    }

    private void OnRefreshMovementSpeed(EntityUid uid, ActiveBloodRushComponent _, RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<VampireComponent>(uid, out var vampire))
            return;

        if (!TryGetVampireActionEvent<VampireBloodRushActionEvent>(vampire, BloodRushActionId, out var rushEv))
            return;

        args.ModifySpeed(rushEv.SpeedMultiplier, rushEv.SpeedMultiplier);
    }

    #endregion

    #region Blood Swell

    private void OnBloodSwellIncomingDamage(EntityUid uid, ActiveBloodSwellComponent _, ref BeforeDamageChangedEvent args)
    {
        static bool IsBrute(string id)
            => id is "Blunt" or "Slash" or "Piercing";

        static bool IsBurn(string id)
            => id is "Heat" or "Cold" or "Shock" or "Caustic";

        foreach (var entry in args.Damage.DamageDict.ToArray())
        {
            var type = entry.Key;
            var value = entry.Value;
            if (value <= 0)
                continue;

            if (IsBrute(type) || IsBurn(type))
                args.Damage.DamageDict[type] = value * 0.5f;
        }
    }

    private void OnBloodSwellStaminaDamage(EntityUid uid, ActiveBloodSwellComponent _, ref BeforeStaminaDamageEvent args)
        => args.Value *= 0.5f;

    private void OnStatusEffectApplied(EntityUid effectUid, StatusEffectComponent effect, ref StatusEffectAppliedEvent args)
    {
        if (!HasComp<ActiveBloodSwellComponent>(args.Target))
            return;

        if (effect.EndEffectTime is not { } end)
            return;

        // Only affect the same set of status effects as before.
        if (!HasComp<StunnedStatusEffectComponent>(effectUid)
            && !HasComp<KnockdownStatusEffectComponent>(effectUid)
            && !HasComp<MovementModStatusEffectComponent>(effectUid)
            && !HasComp<ForcedSleepingStatusEffectComponent>(effectUid))
            return;

        var now = _timing.CurTime;

        var remaining = end - now;
        if (remaining <= TimeSpan.Zero)
            return;

        if (MetaData(effectUid).EntityPrototype is not { ID: var protoId })
            return;

        _statusEffects.TrySetStatusEffectDuration(args.Target, protoId, remaining / 2);
    }

    #endregion

    #region Seismic Stomp

    private void OnSeismicStomp(EntityUid uid, VampireComponent comp, ref VampireSeismicStompActionEvent args)
    {
        if (args.Handled)
            return;

        if (!comp.ActionEntities.TryGetValue("ActionVampireSeismicStomp", out var actionEntity)
            || !HasComp<GargantuaComponent>(uid)
            || !_vampire.CheckAndConsumeBloodCost(uid, comp, actionEntity))
            return;

        var xform = Transform(uid);
        var worldPos = _transform.GetWorldPosition(xform);

        _popup.PopupEntity(Loc.GetString("vampire-seismic-stomp-activate"), uid, uid);

        // Find all entities in radius
        var entities = _lookup.GetEntitiesInRange(xform.Coordinates, args.Radius);

        foreach (var target in entities)
        {
            if (target == uid)
                continue;

            // Only affect mobs
            if (!HasComp<MobStateComponent>(target))
                continue;

            var targetXform = Transform(target);
            var targetPos = _transform.GetWorldPosition(targetXform);
            var direction = targetPos - worldPos;

            if (direction == Vector2.Zero)
                direction = _rand.NextVector2();

            direction = direction.Normalized();

            // Knockdown the target
            _stun.TryKnockdown(target, TimeSpan.FromSeconds(2), true);

            // Throw them away from the vampire
            _throwing.TryThrow(target, direction * args.ThrowDistance, 5f, uid);
        }

        _audio.PlayPvs(args.Sound, xform.Coordinates, AudioParams.Default.WithVolume(3f));

        // Spawn visual effect at vampire's position
        Spawn("VampireSeismicStompEffect", xform.Coordinates);

        args.Handled = true;
    }

    #endregion

    #region Overwhelming Force

    private void OnOverwhelmingForce(EntityUid uid, VampireComponent comp, ref VampireOverwhelmingForceActionEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<GargantuaComponent>(uid))
            return;

        if (!TryComp(uid, out GargantuaComponent? gargantua))
            return;

        gargantua.OverwhelmingForceActive = !gargantua.OverwhelmingForceActive;

        if (gargantua.OverwhelmingForceActive)
        {
            // Add PryingComponent to enable door prying
            var prying = EnsureComp<PryingComponent>(uid);
            prying.PryPowered = true;
            prying.Force = true;
            prying.SpeedModifier = 10f; // Fast prying

            _popup.PopupEntity(Loc.GetString("vampire-overwhelming-force-start"), uid, uid);
        }
        else
        {
            RemComp<PryingComponent>(uid);

            _popup.PopupEntity(Loc.GetString("vampire-overwhelming-force-stop"), uid, uid);
        }

        // Update action toggle state
        if (comp.ActionEntities.TryGetValue(OverwhelmingForceActionId, out var actionEntity)
            && _actions.GetAction(actionEntity) is { } action)
        {
            _actions.SetToggled(action.AsNullable(), gargantua.OverwhelmingForceActive);
        }

        Dirty(uid, gargantua);
        args.Handled = true;
    }

    private void OnPullAttempt(EntityUid uid, GargantuaComponent component, PullAttemptEvent args)
    {
        if (!component.OverwhelmingForceActive)
            return;

        // Prevent being pulled
        if (args.PulledUid == uid)
        {
            args.Cancelled = true;
            _popup.PopupEntity(Loc.GetString("vampire-overwhelming-force-too-heavy"), uid, args.PullerUid, PopupType.MediumCaution);
        }
    }

    #endregion

    #region Demonic Grasp

    private void OnDemonicGrasp(EntityUid uid, VampireComponent comp, ref VampireDemonicGraspActionEvent args)
    {
        if (args.Handled)
            return;

        if (!comp.ActionEntities.TryGetValue("ActionVampireDemonicGrasp", out var actionEntity))
            return;

        if (!HasComp<GargantuaComponent>(uid))
            return;

        var xform = Transform(uid);
        if (xform.GridUid == null)
            return;

        var direction = (args.Target.Position - xform.Coordinates.Position).Normalized();

        if (direction == Vector2.Zero)
            return;

        if (!_vampire.CheckAndConsumeBloodCost(uid, comp, actionEntity))
            return;

        // Check if combat mode is active for pulling
        var shouldPull = TryComp<CombatModeComponent>(uid, out var combat) && combat.IsInCombatMode;

        // Calculate tiles along the path - capture values for lambda
        var maxTiles = (int) args.Range;
        var immobilizeDuration = args.ImmobilizeDuration;

        var delayPerTile = 50; // 50ms between each tile effect

        // Flag to stop spawning after hitting something
        var stopped = false;

        _audio.PlayPvs(args.Sound, args.Target, AudioParams.Default.WithVolume(3f));

        for (var i = 1; i <= maxTiles; i++)
        {
            var tileIndex = i;
            var tileCoords = xform.Coordinates.Offset(direction * tileIndex);

            Timer.Spawn(delayPerTile * i, () =>
            {
                if (!Exists(uid) || stopped)
                    return;

                var blocked = false;
                var entitiesOnTile = _lookup.GetEntitiesInRange(tileCoords, 0.4f);
                foreach (var ent in entitiesOnTile)
                {
                    if (ent == uid)
                        continue;

                    // Check for walls/obstacles before spawning
                    // Check for static physics bodies (walls, structures)
                    if (TryComp<PhysicsComponent>(ent, out var physics) && physics.BodyType == BodyType.Static && physics.Hard)
                    {
                        blocked = true;
                        stopped = true;
                        break;
                    }
                }

                if (blocked)
                    return;

                // Spawn visual effect on the tile
                Spawn("VampireDemonicGraspEffect", tileCoords);

                foreach (var target in entitiesOnTile)
                {
                    if (target == uid)
                        continue;

                    // Check for mobs on this tile
                    if (!HasComp<MobStateComponent>(target))
                        continue;

                    if (shouldPull)
                    {
                        // apply paralyze in combat mode, otherwise immobilize
                        _stun.TryAddParalyzeDuration(target, immobilizeDuration);
                    }
                    else
                    {
                        _stun.TryAddStunDuration(target, immobilizeDuration);

                        if (!HasComp<KnockedDownComponent>(target))
                        {
                            // Don't spawn the visual on targets that are already lying down
                            var attachCoords = new EntityCoordinates(target, Vector2.Zero);
                            EntityManager.SpawnAttachedTo("VampireImmobilizedEffect", attachCoords);
                        }
                    }

                    // Stop spawning further
                    stopped = true;

                    if (shouldPull && Exists(uid))
                    {
                        var vampireXform = Transform(uid);
                        var vampirePos = _transform.GetWorldPosition(vampireXform);
                        var targetXform = Transform(target);
                        var targetCurrentPos = _transform.GetWorldPosition(targetXform);
                        var pullDirection = (vampirePos - targetCurrentPos).Normalized();
                        var distance = (vampirePos - targetCurrentPos).Length();
                        _throwing.TryThrow(target, pullDirection * (distance - 1f), 8f, uid);
                        _popup.PopupEntity(Loc.GetString("vampire-demonic-grasp-pull"), uid, uid);
                    }

                    _popup.PopupEntity(Loc.GetString("vampire-demonic-grasp-hit"), target, target, PopupType.LargeCaution);
                    break;
                }
            });
        }

        args.Handled = true;
    }

    #endregion

    #region Charge

    private void OnCharge(EntityUid uid, VampireComponent comp, ref VampireChargeActionEvent args)
    {
        if (args.Handled)
            return;

        if (!comp.FullPower)
        {
            _popup.PopupEntity(Loc.GetString("action-vampire-not-enough-power"), uid, uid);
            args.Handled = true;
            return;
        }

        if (!TryComp(uid, out GargantuaComponent? gargantua))
            return;

        if (gargantua.IsCharging)
            return;

        if (!comp.ActionEntities.TryGetValue(ChargeActionId, out var actionEntity))
            return;

        if (TryComp<EnsnareableComponent>(uid, out var ensnareable) && ensnareable.IsEnsnared)
        {
            _popup.PopupEntity(Loc.GetString("vampire-legs-ensnared"), uid, uid, PopupType.Medium);
            return;
        }

        var xform = Transform(uid);
        var startPos = _transform.GetWorldPosition(xform);
        var targetPos = _transform.ToMapCoordinates(args.Target).Position;
        var delta = targetPos - startPos;
        var direction = delta.Normalized();

        if (direction == Vector2.Zero)
            return;

        if (!TryComp<PhysicsComponent>(uid, out var physics))
            return;

        if (!_vampire.CheckAndConsumeBloodCost(uid, comp, actionEntity))
            return;

        gargantua.IsCharging = true;
        gargantua.ChargeDirectionVector = direction;

        // Kick off movement immediately so the charge feels responsive
        _physics.SetLinearVelocity(uid, direction * args.ChargeSpeed, body: physics);

        _popup.PopupEntity(Loc.GetString("vampire-charge-start"), uid, uid);

        Dirty(uid, gargantua);
        args.Handled = true;
    }

    private void ProcessChargeMovement(EntityUid uid, GargantuaComponent gargantua, VampireComponent vampire)
    {
        if (!TryComp<PhysicsComponent>(uid, out var physics))
        {
            EndCharge(uid, gargantua);
            return;
        }

        if (!TryGetVampireActionEvent<VampireChargeActionEvent>(vampire, ChargeActionId, out var chargeEv))
        {
            EndCharge(uid, gargantua);
            return;
        }

        var xform = Transform(uid);

        if (xform.GridUid == null || !TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            EndCharge(uid, gargantua);
            return;
        }

        var tileRef = _map.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);
        if (tileRef.Tile.IsEmpty)
        {
            // Check if were over void/space
            EndCharge(uid, gargantua);
            return;
        }

        // Keep pushing forward at a constant speed
        _physics.SetLinearVelocity(uid, gargantua.ChargeDirectionVector * chargeEv.ChargeSpeed, body: physics);
    }

    private void OnChargeCollide(EntityUid uid, GargantuaComponent gargantua, ref StartCollideEvent args)
    {
        if (!gargantua.IsCharging)
            return;

        var other = args.OtherEntity;
        if (other == uid)
            return;

        // Never interact with contained entities
        if (_container.IsEntityInContainer(other))
            return;

        // Mobs
        if (HasComp<MobStateComponent>(other))
        {
            HandleChargeImpact(uid, other, gargantua);
            EndCharge(uid, gargantua);
            return;
        }

        if (!TryComp<VampireComponent>(uid, out var vampire)
            || !TryGetVampireActionEvent<VampireChargeActionEvent>(vampire, ChargeActionId, out var chargeEv))
        {
            EndCharge(uid, gargantua);
            return;
        }

        if (!TryComp<PhysicsComponent>(uid, out var ourPhysics))
        {
            EndCharge(uid, gargantua);
            return;
        }

        if (TryComp<PhysicsComponent>(other, out var otherPhysics)
            && otherPhysics.BodyType == BodyType.Static
            && otherPhysics.CanCollide
            && otherPhysics.Hard
            && (ourPhysics.CollisionMask & otherPhysics.CollisionLayer) != 0)
        {
            // Static obstacle
            var obstacleCoords = Transform(other).Coordinates;

            _audio.PlayPvs(chargeEv.Sound, obstacleCoords);

            if (HasComp<DestructibleComponent>(other))
                _destructible.DestroyEntity(other);

            EndCharge(uid, gargantua);
        }
    }

    private void HandleChargeImpact(EntityUid uid, EntityUid target, GargantuaComponent gargantua)
    {
        if (!TryComp<VampireComponent>(uid, out var vampire)
            || !TryGetVampireActionEvent<VampireChargeActionEvent>(vampire, ChargeActionId, out var chargeEv))
            return;

        _audio.PlayPvs(chargeEv.Sound, target);

        var damageSpec = new DamageSpecifier();
        damageSpec.DamageDict["Blunt"] = chargeEv.CreatureDamage;
        _damageableSystem.TryChangeDamage(target, damageSpec, true, origin: uid);

        // Throw the target
        _throwing.TryThrow(target, gargantua.ChargeDirectionVector * chargeEv.CreatureThrowDistance, 6f, uid);

        _stun.TryKnockdown(target, TimeSpan.FromSeconds(2), true);

        _popup.PopupEntity(Loc.GetString("vampire-charge-impact", ("target", target)), uid, uid);
    }

    private void EndCharge(EntityUid uid, GargantuaComponent gargantua)
    {
        gargantua.IsCharging = false;
        gargantua.ChargeDirectionVector = default;
        if (TryComp<PhysicsComponent>(uid, out var physics))
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
        Dirty(uid, gargantua);
    }

    #endregion
}
