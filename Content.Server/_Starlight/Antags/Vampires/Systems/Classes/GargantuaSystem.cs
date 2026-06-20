using System.Linq;
using System.Numerics;
using Content.Server.Actions;
using Content.Server.Destructible;
using Content.Shared._Starlight.Antags.Vampires;
using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Antags.Vampires.Components.Classes;
using Content.Shared._Starlight.Antags.Vampires.Systems;
using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Doors.Components;
using Content.Shared.Ensnaring.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Prying.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
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
using Content.Shared.Physics;

namespace Content.Server._Starlight.Antags.Vampires.Systems.Classes;

public sealed partial class GargantuaSystem : EntitySystem
{
    private const string ChargeActionId = "ActionVampireCharge";

    private static readonly ProtoId<DamageGroupPrototype> _bruteGroupId = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> _burnGroupId = "Burn";

    [Dependency] private VampireSystem _vampire = default!;

    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedVampireActionUseSystem _vampireActions = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _rand = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireSeismicStompActionEvent>(OnSeismicStomp);
        SubscribeLocalEvent<VampireDemonicGraspActionEvent>(OnDemonicGrasp);
        SubscribeLocalEvent<VampireChargeActionEvent>(OnCharge);

        SubscribeLocalEvent<GargantuaComponent, StartCollideEvent>(OnChargeCollide);

        SubscribeLocalEvent<ActiveBloodSwellComponent, StatusEffectRelayedEvent<BeforeDamageChangedEvent>>(OnBloodSwellIncomingDamage);
        SubscribeLocalEvent<ActiveBloodSwellComponent, StatusEffectRelayedEvent<BeforeStaminaDamageEvent>>(OnBloodSwellStaminaDamage);

        SubscribeLocalEvent<GargantuaComponent, VampireBloodDrankEvent>(OnBloodDrank);
        SubscribeLocalEvent<GargantuaComponent, UserPriedDoorEvent>(OnDoorPried);
        // Status effects are raised on the status effect entity, so hook globally.
        SubscribeLocalEvent<StatusEffectComponent, StatusEffectAppliedEvent>(OnStatusEffectApplied);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<GargantuaComponent>();
        while (query.MoveNext(out var uid, out var gargantua))
        {
            if (gargantua.IsCharging)
                ProcessChargeMovement(uid, gargantua);
        }

        ProcessActiveDemonicGrasps(now);
    }

    private void OnBloodDrank(EntityUid uid, GargantuaComponent gargantua, ref VampireBloodDrankEvent args)
    {
        if (!TryComp<VampireComponent>(uid, out var vampire))
            return;

        if (vampire.TotalBlood < gargantua.PassiveHealBloodThreshold)
            return;

        var spec = new DamageSpecifier();
        foreach (var (groupId, amount) in gargantua.PassiveHealGroups)
        {
            if (amount <= FixedPoint2.Zero || !_proto.TryIndex<DamageGroupPrototype>(groupId, out var group))
                continue;

            spec += new DamageSpecifier(group, -amount);
        }

        if (spec.Empty)
            return;

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

    private void OnBloodSwellIncomingDamage(EntityUid uid, ActiveBloodSwellComponent active, ref StatusEffectRelayedEvent<BeforeDamageChangedEvent> args)
    {
        foreach (var entry in args.Args.Damage.DamageDict.ToArray())
        {
            var type = entry.Key;
            var value = entry.Value;
            if (value <= 0)
                continue;

            if (active.ReducedDamageTypes.Contains(type))
                args.Args.Damage.DamageDict[type] = value * active.IncomingDamageMultiplier;
        }
    }

    private void OnBloodSwellStaminaDamage(EntityUid uid, ActiveBloodSwellComponent active, ref StatusEffectRelayedEvent<BeforeStaminaDamageEvent> args)
    {
        var ev = args.Args;
        ev.Value *= active.StaminaDamageMultiplier;
        args.Args = ev;
    }

    private void OnStatusEffectApplied(EntityUid effectUid, StatusEffectComponent effect, ref StatusEffectAppliedEvent args)
    {
        if (!_statusEffects.TryEffectsWithComp<ActiveBloodSwellComponent>(args.Target, out var effects))
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

        var multiplier = 1f;
        foreach (var activeEffect in effects)
        {
            multiplier = MathF.Min(multiplier, activeEffect.Comp1.StatusEffectDurationMultiplier);
        }

        _statusEffects.TrySetStatusEffectDuration(args.Target, protoId, remaining * multiplier);
    }

    #endregion

    #region Seismic Stomp

    private void OnSeismicStomp(VampireSeismicStompActionEvent args)
    {
        if (args.Handled)
            return;

        var uid = args.Performer;
        var actionEntity = args.Action.Owner;
        if (!Exists(actionEntity)
            || !HasComp<GargantuaComponent>(uid)
            || !_vampireActions.TryUse(uid, actionEntity))
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

    private void OnDoorPried(EntityUid uid, GargantuaComponent component, ref UserPriedDoorEvent args)
    {

        if (!component.OverwhelmingForceActive)
            return;

        if (!TryComp<VampireComponent>(uid, out var vampire))
            return;

        _vampire.TrySpendBlood(uid, vampire, component.OverwhelmingForceDoorPryBloodCost, showPopup: false);

        _audio.PlayPvs(component.OverwhelmingForcePrySound, uid, AudioParams.Default.WithVolume(2f));
    }

    #endregion

    #region Demonic Grasp

    private void OnDemonicGrasp(VampireDemonicGraspActionEvent args)
    {
        if (args.Handled)
            return;

        var uid = args.Performer;
        var actionEntity = args.Action.Owner;
        if (!HasComp<GargantuaComponent>(uid))
            return;

        if (HasComp<ActiveVampireDemonicGraspComponent>(uid))
        {
            args.Handled = true;
            return;
        }

        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid)
            return;

        if (_transform.GetGrid(args.Target) != gridUid)
            return;

        var direction = (args.Target.Position - xform.Coordinates.Position).Normalized();

        if (direction == Vector2.Zero)
            return;

        if (!Exists(actionEntity)
            || !_vampireActions.TryUse(uid, actionEntity))
            return;

        args.Handled = true;

        // Check if combat mode is active for pulling
        var shouldPull = TryComp<CombatModeComponent>(uid, out var combat) && combat.IsInCombatMode;

        _audio.PlayPvs(args.Sound, args.Target, AudioParams.Default.WithVolume(3f));

        var distance = MathF.Min(args.Range, (args.Target.Position - xform.Coordinates.Position).Length());
        var maxTiles = Math.Max(1, (int) MathF.Ceiling(distance));
        var tileInterval = args.ProjectileSpeed > 0f
            ? TimeSpan.FromSeconds(1f / args.ProjectileSpeed)
            : args.TileInterval;

        var active = EnsureComp<ActiveVampireDemonicGraspComponent>(uid);
        active.StartCoordinates = xform.Coordinates;
        active.GridUid = gridUid;
        active.Direction = direction;
        active.CurrentTile = 0;
        active.MaxTiles = maxTiles;
        active.TileInterval = tileInterval;
        active.ImmobilizeDuration = args.ImmobilizeDuration;
        active.PullTarget = shouldPull;
        active.EffectPrototype = args.EffectPrototype;
        active.ImmobilizedEffectPrototype = args.ImmobilizedEffectPrototype;
        active.NextTileTime = _timing.CurTime + tileInterval;
    }

    private void ProcessActiveDemonicGrasps(TimeSpan now)
    {
        var query = EntityQueryEnumerator<ActiveVampireDemonicGraspComponent>();
        while (query.MoveNext(out var uid, out var active))
        {
            if (active.TileInterval <= TimeSpan.Zero)
            {
                RemComp<ActiveVampireDemonicGraspComponent>(uid);
                continue;
            }

            while (now >= active.NextTileTime)
            {
                active.CurrentTile++;
                if (active.CurrentTile > active.MaxTiles || !Exists(active.GridUid))
                {
                    RemComp<ActiveVampireDemonicGraspComponent>(uid);
                    break;
                }

                var tileCoords = active.StartCoordinates.Offset(active.Direction * active.CurrentTile);
                if (ProcessDemonicGraspTile(uid, active, tileCoords))
                {
                    RemComp<ActiveVampireDemonicGraspComponent>(uid);
                    break;
                }

                active.NextTileTime += active.TileInterval;
            }
        }
    }

    private bool ProcessDemonicGraspTile(EntityUid uid, ActiveVampireDemonicGraspComponent active, EntityCoordinates tileCoords)
    {
        if (!_vampire.IsValidTile(tileCoords, active.GridUid))
            return true;

        var entitiesOnTile = _lookup.GetEntitiesInRange(tileCoords, 0.4f);
        foreach (var ent in entitiesOnTile)
        {
            if (ent == uid)
                continue;

            if (TryComp<PhysicsComponent>(ent, out var physics)
                && physics.BodyType == BodyType.Static
                && physics.Hard
                && (physics.CollisionLayer & (int) CollisionGroup.Impassable) != 0)
            {
                EntityManager.SpawnAttachedTo(active.EffectPrototype, tileCoords);
                return true;
            }
        }

        foreach (var target in entitiesOnTile)
        {
            if (target == uid || !HasComp<MobStateComponent>(target))
                continue;

            if (active.PullTarget)
            {
                _stun.TryAddParalyzeDuration(target, active.ImmobilizeDuration);
            }
            else
            {
                _stun.TryAddStunDuration(target, active.ImmobilizeDuration);

                if (!HasComp<KnockedDownComponent>(target))
                {
                    var attachCoords = new EntityCoordinates(target, Vector2.Zero);
                    EntityManager.SpawnAttachedTo(active.ImmobilizedEffectPrototype, attachCoords);
                }
            }

            if (active.PullTarget && Exists(uid))
            {
                var vampirePos = _transform.GetWorldPosition(Transform(uid));
                var targetCurrentPos = _transform.GetWorldPosition(Transform(target));
                var pullDirection = (vampirePos - targetCurrentPos).Normalized();
                var distance = (vampirePos - targetCurrentPos).Length();
                if (distance > 1f)
                    _throwing.TryThrow(target, pullDirection * (distance - 1f), 8f, uid);
                _popup.PopupEntity(Loc.GetString("vampire-demonic-grasp-pull"), uid, uid);
            }

            _popup.PopupEntity(Loc.GetString("vampire-demonic-grasp-hit"), target, target, PopupType.LargeCaution);
            return true;
        }

        EntityManager.SpawnAttachedTo(active.EffectPrototype, tileCoords);
        return false;
    }

    #endregion

    #region Charge

    private void OnCharge(VampireChargeActionEvent args)
    {
        if (args.Handled)
            return;

        var uid = args.Performer;
        var actionEntity = args.Action.Owner;

        if (!TryComp<GargantuaComponent>(uid, out var gargantua))
            return;

        if (gargantua.IsCharging)
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

        if (!Exists(actionEntity)
            || !_vampireActions.TryUse(uid, actionEntity))
            return;

        gargantua.IsCharging = true;
        gargantua.ChargeDirectionVector = direction;
        gargantua.ChargeSpeed = args.ChargeSpeed;
        gargantua.ChargeCreatureDamage = args.CreatureDamage;
        gargantua.ChargeCreatureThrowDistance = args.CreatureThrowDistance;
        gargantua.ChargeStructuralDamage = args.StructuralDamage;
        gargantua.ChargeSound = args.Sound;

        // Kick off movement immediately so the charge feels responsive
        _physics.SetLinearVelocity(uid, direction * gargantua.ChargeSpeed, body: physics);

        _popup.PopupEntity(Loc.GetString("vampire-charge-start"), uid, uid);

        Dirty(uid, gargantua);
        args.Handled = true;
    }

    private void ProcessChargeMovement(EntityUid uid, GargantuaComponent gargantua)
    {
        if (!TryComp<PhysicsComponent>(uid, out var physics))
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
        _physics.SetLinearVelocity(uid, gargantua.ChargeDirectionVector * gargantua.ChargeSpeed, body: physics);
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

            _audio.PlayPvs(gargantua.ChargeSound, obstacleCoords, AudioParams.Default.WithVolume(3f));

            if (HasComp<DestructibleComponent>(other))
                _destructible.DestroyEntity(other);

            EndCharge(uid, gargantua);
        }
    }

    private void HandleChargeImpact(EntityUid uid, EntityUid target, GargantuaComponent gargantua)
    {
        _audio.PlayPvs(gargantua.ChargeSound, target, AudioParams.Default.WithVolume(3f));

        var damageSpec = new DamageSpecifier();
        damageSpec.DamageDict["Blunt"] = gargantua.ChargeCreatureDamage;
        _damageableSystem.TryChangeDamage(target, damageSpec, true, origin: uid);

        // Throw the target
        _throwing.TryThrow(target, gargantua.ChargeDirectionVector * gargantua.ChargeCreatureThrowDistance, 6f, uid);

        _stun.TryKnockdown(target, TimeSpan.FromSeconds(2), true);

        _popup.PopupEntity(Loc.GetString("vampire-charge-impact", ("target", target)), uid, uid);
    }

    private void EndCharge(EntityUid uid, GargantuaComponent gargantua)
    {
        gargantua.IsCharging = false;
        gargantua.ChargeDirectionVector = default;
        gargantua.ChargeSpeed = 0f;
        gargantua.ChargeCreatureDamage = 0f;
        gargantua.ChargeCreatureThrowDistance = 0f;
        gargantua.ChargeStructuralDamage = 0f;
        gargantua.ChargeSound = null;
        if (TryComp<PhysicsComponent>(uid, out var physics))
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
        Dirty(uid, gargantua);
    }

    #endregion
}
