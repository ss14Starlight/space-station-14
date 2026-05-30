using System.Linq;
using System.Numerics;
using Content.Server.Actions;
using Content.Server.Polymorph.Systems;
using Content.Shared._Starlight.Antags.Vampires;
using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Antags.Vampires.Components.Classes;
using Content.Shared._Starlight.Antags.Vampires.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Components;
using Content.Shared.Maps;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Physics;
using Content.Shared.Polymorph;
using Content.Shared.Warps;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Antags.Vampires.Systems;

public sealed class HemomancerSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> _poisonTypeId = "Poison";
    private static readonly ProtoId<DamageTypePrototype> _bluntTypeId = "Blunt";
    private static readonly ProtoId<DamageGroupPrototype> _bruteGroupId = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> _burnGroupId = "Burn";

    private static readonly Vector2[] _tendrilOffsets =
    [
        new(-1, -1), new(0, -1), new(1, -1),
        new(-1, 0), new(0, 0), new(1, 0),
        new(-1, 1), new(0, 1), new(1, 1),
    ];

    private readonly Dictionary<EntityUid, EntityUid> _predatorSenseUiActionEntities = new();

    [Dependency] private readonly VampireSystem _vampire = default!;

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedWieldableSystem _wieldable = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly MovementModStatusSystem _movementMod = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private ISawmill? _sawmill;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _log.GetSawmill("Vampire.Hemomancer");

        SubscribeLocalEvent<VampireHemomancerTendrilsActionEvent>(OnHemomancerTendrils);
        SubscribeLocalEvent<VampireBloodBarrierActionEvent>(OnBloodBarrier);
        SubscribeLocalEvent<VampireComponent, VampireSanguinePoolActionEvent>(OnSanguinePool);
        SubscribeLocalEvent<VampireComponent, VampireBloodEruptionActionEvent>(OnBloodEruption);
        SubscribeLocalEvent<VampireComponent, VampireBloodBringersRiteActionEvent>(OnBloodBringersRite);
        SubscribeLocalEvent<VampireHemomancerClawsActivatedEvent>(OnHemomancerClawsActivated);
        SubscribeLocalEvent<HemomancerComponent, PolymorphedEvent>(OnHemomancerPolymorphed);
        SubscribeLocalEvent<SanguinePoolComponent, PolymorphedEvent>(OnSanguinePoolReverted);

        SubscribeLocalEvent<HemomancerComponent, VampireBloodDrankEvent>(OnBloodDrank);

        SubscribeLocalEvent<VampireComponent, VampireLocateMindActionEvent>(OnPredatorSense);
        Subs.BuiEvents<VampireComponent>(VampireLocateUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnPredatorSenseUiOpened);
            subs.Event<BoundUIClosedEvent>(OnPredatorSenseUiClosed);
            subs.Event<VampireLocateSelectedBuiMsg>(OnPredatorSenseSelected);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var tendrils = EntityQueryEnumerator<PendingVampireTendrilsComponent>();
        while (tendrils.MoveNext(out var uid, out var pending))
        {
            if (now < pending.TriggerTime)
                continue;

            ProcessPendingTendrils((uid, pending));
            RemComp<PendingVampireTendrilsComponent>(uid);
        }

        ProcessActiveBloodBringersRites(now);
    }

    private void OnBloodDrank(EntityUid uid, HemomancerComponent hemomancer, ref VampireBloodDrankEvent args)
    {
        if (!TryComp<VampireComponent>(uid, out var vampire))
            return;

        if (vampire.TotalBlood < 300)
            return;

        var wasStarving = vampire.BloodFullness <= 0f;
        vampire.BloodFullness = MathF.Min(vampire.MaxBloodFullness, vampire.BloodFullness + 5f);
        if (wasStarving && vampire.BloodFullness > 0f)
            _movementSpeed.RefreshMovementSpeedModifiers(uid);

        Dirty(uid, vampire);
    }

    private void OnHemomancerClawsActivated(ref VampireHemomancerClawsActivatedEvent args)
    {
        var uid = args.Performer;
        var active = EnsureComp<ActiveVampireHemomancerClawsComponent>(uid);

        if (active.SpawnedClaws != null && EntityManager.EntityExists(active.SpawnedClaws.Value))
        {
            var oldClaws = active.SpawnedClaws.Value;
            active.SpawnedClaws = null;
            RemComp<UnremoveableComponent>(oldClaws);
            EntityManager.DeleteEntity(oldClaws);
        }

        if (TryComp<HandsComponent>(uid, out var handsComp))
        {
            _wieldable.UnwieldAll((uid, handsComp), force: true);
            foreach (var handName in handsComp.Hands.Keys.ToArray())
                _hands.TryDrop((uid, handsComp), handName, checkActionBlocker: false);
        }

        var coords = Transform(uid).Coordinates;
        var claws = EntityManager.SpawnEntity("VampiricClawsItem", coords);
        active.SpawnedClaws = claws;

        if (TryComp<VampireComponent>(uid, out var vampire))
        {
            vampire.SpawnedClaws = claws;
            Dirty(uid, vampire);
        }

        if (!_hands.TryPickupAnyHand(uid, claws, checkActionBlocker: false, animate: false))
        {
            active.SpawnedClaws = null;

            if (vampire != null && vampire.SpawnedClaws == claws)
            {
                vampire.SpawnedClaws = null;
                Dirty(uid, vampire);
            }

            RemComp<UnremoveableComponent>(claws);
            EntityManager.DeleteEntity(claws);
            return;
        }

        if (TryComp<WieldableComponent>(claws, out var wieldable) && _hands.IsHolding(uid, claws, out _))
            _wieldable.TryWield(claws, wieldable, uid);
    }

    private void OnHemomancerTendrils(VampireHemomancerTendrilsActionEvent args)
    {
        var action = args.Action.Owner;
        if (args.Handled
            || !TryComp<VampireComponent>(args.Performer, out var comp)
            || !HasComp<HemomancerComponent>(args.Performer))
            return;

        var targetCoords = args.Target;
        var tileCoords = targetCoords.WithPosition(targetCoords.Position.Floored() + new Vector2(args.PositionOffset, args.PositionOffset));

        if (_transform.GetGrid(tileCoords) is not { } gridUid
            || !TryComp<MapGridComponent>(gridUid, out var gridComp)
            || !_map.TryGetTileRef(gridUid, gridComp, tileCoords, out var tileRef)
            || _turf.IsSpace(tileRef)
            || _vampire.IsTileBlockedByEntities(tileCoords))
        {
            _popup.PopupEntity(Loc.GetString("action-vampire-hemomancer-tendrils-wrong-place"), args.Performer, args.Performer);
            return;
        }

        if (!_vampire.CheckAndConsumeActionCost(args.Performer, comp, action))
            return;

        args.Handled = true;

        if (args.SpawnVisuals)
            SpawnTendrilVisuals(tileCoords, args.TendrilsVisualPrototype);

        var delay = args.Delay < args.MinDelay ? args.MinDelay : args.Delay;
        var slowDuration = args.SlowDuration < args.MinSlowDuration ? args.MinSlowDuration : args.SlowDuration;
        var slowMultiplier = MathF.Max(args.MinSlowMultiplier, args.SlowMultiplier);
        var toxinDamage = args.ToxinDamage;
        var performerUid = args.Performer;
        var targetRange = args.TargetRange;
        var puddleId = args.TendrilsPuddlePrototype;

        var pending = EnsureComp<PendingVampireTendrilsComponent>(performerUid);
        pending.TileCoordinates = tileCoords;
        pending.PuddlePrototype = puddleId;
        pending.TargetRange = targetRange;
        pending.SlowDuration = slowDuration;
        pending.SlowMultiplier = slowMultiplier;
        pending.ToxinDamage = toxinDamage;
        pending.TriggerTime = _timing.CurTime + delay;
    }

    private void ProcessPendingTendrils(Entity<PendingVampireTendrilsComponent> ent)
    {
        var (performerUid, pending) = ent;
        if (!Exists(performerUid))
            return;

        var tileCoords = pending.TileCoordinates;
        var gridUid = _transform.GetGrid(tileCoords);
        if (gridUid == null || !TryComp<MapGridComponent>(gridUid.Value, out var gridComp))
            return;

        if (_map.TryGetTileRef(gridUid.Value, gridComp, tileCoords, out var centerTileRef)
            && !_turf.IsSpace(centerTileRef)
            && !_vampire.IsTileBlockedByEntities(tileCoords))
        {
            Spawn(pending.PuddlePrototype, tileCoords);
        }

        var hitEnemies = new HashSet<EntityUid>();

        foreach (var offset in _tendrilOffsets)
        {
            var center = tileCoords.Offset(offset);
            if (!_map.TryGetTileRef(gridUid.Value, gridComp, center, out var tileRef)
                || _turf.IsSpace(tileRef)
                || _vampire.IsTileBlockedByEntities(center))
                continue;

            foreach (var target in _lookup.GetEntitiesInRange(center, pending.TargetRange, LookupFlags.Dynamic | LookupFlags.Sundries))
            {
                if (target == performerUid
                    || hitEnemies.Contains(target)
                    || !HasComp<HumanoidAppearanceComponent>(target)
                    || !HasComp<DamageableComponent>(target))
                {
                    continue;
                }

                var poisonSpec = new DamageSpecifier(_proto.Index<DamageTypePrototype>(_poisonTypeId), pending.ToxinDamage);
                _damageableSystem.TryChangeDamage(target, poisonSpec, true, origin: performerUid);
                _movementMod.TryAddMovementSpeedModDuration(target, MovementModStatusSystem.FlashSlowdown, pending.SlowDuration, pending.SlowMultiplier);
                hitEnemies.Add(target);
            }
        }
    }

    private void SpawnTendrilVisuals(EntityCoordinates tileCoords, EntProtoId tendrilVisualId)
    {
        var gridUid = _transform.GetGrid(tileCoords);
        if (gridUid == null || !TryComp<MapGridComponent>(gridUid.Value, out var gridComp))
            return;

        foreach (var offset in _tendrilOffsets)
        {
            var coords = tileCoords.Offset(offset);
            if (!_map.TryGetTileRef(gridUid.Value, gridComp, coords, out var tileRef)
                || _turf.IsSpace(tileRef)
                || _vampire.IsTileBlockedByEntities(coords))
                continue;

            EntityManager.SpawnEntity(tendrilVisualId, coords);
        }
    }

    private void OnBloodBarrier(VampireBloodBarrierActionEvent args)
    {
        if (args.Handled
            || !TryComp<VampireComponent>(args.Performer, out var comp)
            || !HasComp<HemomancerComponent>(args.Performer))
            return;

        var targetCoords = args.Target;
        var tileCoords = targetCoords.WithPosition(targetCoords.Position.Floored() + new Vector2(0.5f, 0.5f));

        if (_transform.GetGrid(tileCoords) is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var gridComp))
        {
            _popup.PopupEntity(Loc.GetString("action-vampire-blood-barrier-wrong-place"), args.Performer, args.Performer);
            return;
        }

        var performerTransform = Transform(args.Performer);
        var direction = performerTransform.LocalRotation.ToWorldVec();

        var perpendicular = new Vector2(-direction.Y, direction.X);

        var barrierCount = Math.Clamp(args.BarrierCount, 1, 9);
        var half = barrierCount / 2;
        var successfulPositions = new List<Vector2>(barrierCount);

        for (var i = -half; i <= half && successfulPositions.Count < barrierCount; i++)
        {
            var pos = tileCoords.Position + (perpendicular * i);
            var barrierCoords = tileCoords.WithPosition(pos.Floored() + new Vector2(0.5f, 0.5f));

            if (!_vampire.IsValidTile(barrierCoords, gridUid, gridComp))
                continue;

            successfulPositions.Add(barrierCoords.Position);
        }

        if (successfulPositions.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("action-vampire-blood-barrier-wrong-place"), args.Performer, args.Performer);
            return;
        }

        if (!_vampire.CheckAndConsumeBloodCost(args.Performer, comp, args.Action.Owner))
            return;

        args.Handled = true;

        foreach (var pos in successfulPositions)
        {
            var barrierCoords = tileCoords.WithPosition(pos);
            var barrier = EntityManager.SpawnEntity(args.BarrierPrototype, barrierCoords);
            var preventComp = EnsureComp<PreventCollideComponent>(barrier);
            preventComp.Uid = args.Performer;
            Dirty(barrier, preventComp);
        }
    }

    private void OnSanguinePool(EntityUid uid, VampireComponent comp, ref VampireSanguinePoolActionEvent args)
    {
        if (args.Handled || !TryComp<HemomancerComponent>(uid, out var hemomancer))
            return;

        if (hemomancer.InSanguinePool)
        {
            _popup.PopupEntity(Loc.GetString("action-vampire-sanguine-pool-already-in"), uid, uid);
            return;
        }

        var curCoords = Transform(uid).Coordinates;
        if (_transform.GetGrid(curCoords) is not { } gridUid
            || !TryComp<MapGridComponent>(gridUid, out var gridComp)
            || !_map.TryGetTileRef(gridUid, gridComp, curCoords, out var tileRef)
            || _turf.IsSpace(tileRef))
        {
            _popup.PopupEntity(Loc.GetString("action-vampire-sanguine-pool-invalid-tile"), uid, uid);
            return;
        }

        if (!_vampire.CheckAndConsumeBloodCost(uid, comp, args.Action.Owner))
            return;

        if (TryActivateSanguinePool(uid, args))
            args.Handled = true;
    }

    private bool TryActivateSanguinePool(EntityUid uid, VampireSanguinePoolActionEvent args)
    {
        if (!_proto.TryIndex(args.PolymorphPrototype, out var polymorphProto))
        {
            _sawmill?.Error($"Missing polymorph prototype '{args.PolymorphPrototype}'.");
            return false;
        }

        var duration = Math.Max(1, (int)MathF.Ceiling((float)args.Duration.TotalSeconds));
        var configuration = polymorphProto.Configuration with
        {
            Duration = duration
        };

        var poolEntity = _polymorph.PolymorphEntity(uid, configuration);
        if (poolEntity == null)
            return false;

        if (TryComp<SanguinePoolComponent>(poolEntity.Value, out var poolComp))
        {
            poolComp.ExitEffectPrototype = args.ExitEffectPrototype;
            poolComp.ExitSound = args.ExitSound;
            Dirty(poolEntity.Value, poolComp);
        }

        Spawn(args.EnterEffectPrototype, Transform(poolEntity.Value).Coordinates);
        _audio.PlayPvs(args.EnterSound, uid, AudioParams.Default.WithVolume(-2f));
        _popup.PopupEntity(Loc.GetString("action-vampire-sanguine-pool-enter"), poolEntity.Value, poolEntity.Value);
        return true;
    }

    private void OnHemomancerPolymorphed(Entity<HemomancerComponent> ent, ref PolymorphedEvent args)
    {
        if (args.IsRevert || !HasComp<SanguinePoolComponent>(args.NewEntity))
            return;

        var (uid, comp) = ent;
        if (comp.InSanguinePool)
            return;

        comp.InSanguinePool = true;
        Dirty(uid, comp);
    }

    private void OnSanguinePoolReverted(Entity<SanguinePoolComponent> ent, ref PolymorphedEvent args)
    {
        if (!args.IsRevert || !Exists(args.NewEntity) || !TryComp<HemomancerComponent>(args.NewEntity, out var hemomancer))
            return;

        if (!hemomancer.InSanguinePool)
            return;

        hemomancer.InSanguinePool = false;
        Dirty(args.NewEntity, hemomancer);

        Spawn(ent.Comp.ExitEffectPrototype, Transform(args.NewEntity).Coordinates);
        _audio.PlayPvs(ent.Comp.ExitSound, args.NewEntity, AudioParams.Default.WithVolume(-2f));
        _popup.PopupEntity(Loc.GetString("action-vampire-sanguine-pool-exit"), args.NewEntity, args.NewEntity);
    }

    private void OnBloodEruption(EntityUid uid, VampireComponent comp, ref VampireBloodEruptionActionEvent args)
    {
        if (args.Handled || !_vampire.CanUseVampireAbility(uid, comp, args.Action.Owner))
            return;

        var coords = Transform(uid).Coordinates;
        var nearbyEntities = _lookup.GetEntitiesInRange(coords, args.Range);

        var targetsToDamage = new HashSet<EntityUid>();
        var targetsToVisualize = new HashSet<EntityUid>();

        foreach (var entity in nearbyEntities)
        {
            if (entity == uid)
                continue;

            if (!IsBloodPuddle(entity, args.PuddleReagent))
                continue;

            if (!TryComp(entity, out TransformComponent? xform) || !xform.Anchored)
                continue;

            if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var gridComp))
                continue;

            if (_container.IsEntityOrParentInContainer(entity))
                continue;

            var puddleCoords = xform.Coordinates;
            var puddleTile = _map.CoordinatesToTile(gridUid, gridComp, puddleCoords);
            var targetsNearPuddle = _lookup.GetEntitiesInRange(puddleCoords, args.TargetRange)
                .Where(target => target != uid
                                 && target != entity
                                 && HasComp<DamageableComponent>(target)
                                 && HasComp<BloodstreamComponent>(target)
                                 && !_container.IsEntityOrParentInContainer(target))
                .ToList();

            foreach (var target in targetsNearPuddle)
            {
                targetsToDamage.Add(target);

                if (!TryComp(target, out TransformComponent? targetXform))
                    continue;

                if (targetXform.GridUid != gridUid)
                    continue;

                var targetTile = _map.CoordinatesToTile(gridUid, gridComp, targetXform.Coordinates);
                if (targetTile == puddleTile)
                    targetsToVisualize.Add(target);
            }
        }

        if (targetsToDamage.Count == 0)
            return;

        if (!_vampire.CheckAndConsumeBloodCost(uid, comp, args.Action.Owner))
            return;

        var blunt = _proto.Index<DamageTypePrototype>(_bluntTypeId);
        foreach (var targetUid in targetsToDamage)
        {
            var spec = new DamageSpecifier(blunt, args.Damage);
            _damageableSystem.TryChangeDamage(targetUid, spec, true, origin: uid);
        }

        foreach (var targetUid in targetsToVisualize)
        {
            if (!TryComp(targetUid, out TransformComponent? targetXform) || _container.IsEntityOrParentInContainer(targetUid))
                continue;

            if (targetXform == null)
                continue;

            var visual = Spawn("VampireBloodEruptionVisual", targetXform.Coordinates);
            _audio.PlayPvs(args.Sound, visual, AudioParams.Default.WithVolume(-2f));
        }

        _popup.PopupEntity(Loc.GetString("action-vampire-blood-eruption-activated"), uid, uid);
        args.Handled = true;
    }

    private bool IsBloodPuddle(EntityUid uid, string reagent)
    {
        if (!TryComp<PuddleComponent>(uid, out var puddle))
            return false;

        if (!_solution.TryGetSolution(uid, puddle.SolutionName, out _, out var solution))
            return false;

        return solution.ContainsReagent(reagent, null);
    }

    private void OnBloodBringersRite(EntityUid uid, VampireComponent comp, ref VampireBloodBringersRiteActionEvent args)
    {
        if (args.Handled
            || !comp.ActionEntities.TryGetValue("ActionVampireBloodBringersRite", out var actionEntity)
            || !TryComp<HemomancerComponent>(uid, out var hemomancer))
            return;

        if (!comp.FullPower)
        {
            _popup.PopupEntity(Loc.GetString("action-vampire-not-enough-power"), uid, uid);
            args.Handled = true;
            return;
        }

        if (hemomancer.BloodBringersRiteActive)
        {
            DeactivateBloodBringersRite(uid, hemomancer);
            _popup.PopupEntity(Loc.GetString("action-vampire-blood-bringers-rite-stop"), uid, uid);
        }
        else
        {
            if (comp.DrunkBlood < args.Cost)
            {
                _popup.PopupEntity(Loc.GetString("action-vampire-blood-brighters-rite-not-enough-blood"), uid, uid);
                return;
            }

            ActivateBloodBringersRite(uid, hemomancer, args.ToggleInterval, args.Cost, args.Range, args.Damage,
                args.HealBrute, args.HealBurn, args.HealStamina, args.MaxTicks, args.BeamPrototype);
            _popup.PopupEntity(Loc.GetString("action-vampire-blood-bringers-rite-start"), uid, uid);
        }

        if (_actions.GetAction(actionEntity) is { } action)
            _actions.SetToggled(action.AsNullable(), hemomancer.BloodBringersRiteActive);

        args.Handled = true;
    }

    private void ActivateBloodBringersRite(EntityUid uid,
        HemomancerComponent comp,
        TimeSpan interval,
        int cost,
        float range,
        FixedPoint2 damage,
        FixedPoint2 healBrute,
        FixedPoint2 healBurn,
        float healStamina,
        int maxTicks,
        string beamPrototype)
    {
        comp.BloodBringersRiteActive = true;
        comp.BloodBringersRiteLoopId++;

        var drainBeamComp = EnsureComp<VampireDrainBeamComponent>(uid);
        drainBeamComp.VisualPrototype = beamPrototype;
        drainBeamComp.ActiveBeams.Clear();

        Dirty(uid, comp);

        var active = EnsureComp<ActiveVampireBloodBringersRiteComponent>(uid);
        active.TicksRemaining = Math.Max(1, maxTicks);
        active.TickInterval = interval;
        active.BloodCost = cost;
        active.Range = range;
        active.Damage = damage;
        active.HealBrute = healBrute;
        active.HealBurn = healBurn;
        active.HealStamina = healStamina;
        active.BeamPrototype = beamPrototype;
        active.NextTick = _timing.CurTime;
    }

    private void DeactivateBloodBringersRite(EntityUid uid, HemomancerComponent comp)
    {
        comp.BloodBringersRiteActive = false;

        if (TryComp<VampireDrainBeamComponent>(uid, out var drainBeamComp))
        {
            foreach (var connection in drainBeamComp.ActiveBeams.Values)
            {
                var removeEvent = new VampireDrainBeamEvent(GetNetEntity(connection.Source), GetNetEntity(connection.Target), false, drainBeamComp.VisualPrototype);
                RaiseNetworkEvent(removeEvent);
            }

            drainBeamComp.ActiveBeams.Clear();
        }

        RemComp<ActiveVampireBloodBringersRiteComponent>(uid);
        Dirty(uid, comp);
    }

    private void ProcessActiveBloodBringersRites(TimeSpan now)
    {
        var query = EntityQueryEnumerator<ActiveVampireBloodBringersRiteComponent, VampireComponent, HemomancerComponent>();
        while (query.MoveNext(out var uid, out var active, out var comp, out var hemomancer))
        {
            if (active.TickInterval <= TimeSpan.Zero)
            {
                DeactivateBloodBringersRite(uid, hemomancer);
                continue;
            }

            while (now >= active.NextTick)
            {
                if (!ProcessBloodBringersRiteTick(uid, active, comp, hemomancer))
                    break;

                active.TicksRemaining--;
                if (active.TicksRemaining <= 0)
                {
                    DeactivateBloodBringersRite(uid, hemomancer);
                    break;
                }

                active.NextTick += active.TickInterval;
            }
        }
    }

    private bool ProcessBloodBringersRiteTick(
        EntityUid uid,
        ActiveVampireBloodBringersRiteComponent active,
        VampireComponent comp,
        HemomancerComponent hemomancer)
    {
        if (!comp.ActionEntities.TryGetValue("ActionVampireBloodBringersRite", out var actionEntity)
            || !hemomancer.BloodBringersRiteActive)
            return false;

        if (TryComp<MobStateComponent>(uid, out var mobState) &&
            mobState.CurrentState == Shared.Mobs.MobState.Dead)
        {
            DeactivateBloodBringersRite(uid, hemomancer);
            return false;
        }

        if (comp.DrunkBlood < active.BloodCost)
        {
            DeactivateBloodBringersRite(uid, hemomancer);
            _popup.PopupEntity(Loc.GetString("action-vampire-blood-bringers-rite-stop-blood"), uid, uid);

            if (_actions.GetAction(actionEntity) is { } action)
                _actions.SetToggled(action.AsNullable(), false);

            return false;
        }

        if (!_vampire.TrySpendBlood(uid, comp, active.BloodCost))
            return false;

        var coords = Transform(uid).Coordinates;
        var currentTargets = new List<EntityUid>();
        var nearbyEntities = _lookup.GetEntitiesInRange(coords, active.Range);

        foreach (var entity in nearbyEntities)
        {
            if (entity == uid)
                continue;

            if (_container.IsEntityOrParentInContainer(entity))
                continue;

            if (TryComp<MobStateComponent>(entity, out var state) && state.CurrentState == Shared.Mobs.MobState.Dead)
                continue;

            if (!HasComp<HumanoidAppearanceComponent>(entity) || !HasComp<BloodstreamComponent>(entity))
                continue;

            if (!_examine.InRangeUnOccluded(uid, entity, active.Range))
                continue;

            currentTargets.Add(entity);
        }

        UpdateDrainBeamNetwork(uid, currentTargets, active.Range);

        var count = currentTargets.Count;
        if (count > 0)
        {
            var bluntType = _proto.Index<DamageTypePrototype>(_bluntTypeId);
            foreach (var target in currentTargets)
            {
                var dmgSpec = new DamageSpecifier(bluntType, active.Damage);
                _damageableSystem.TryChangeDamage(target, dmgSpec, true, origin: uid);
            }

            var selfHealSpec = new DamageSpecifier();
            selfHealSpec += new DamageSpecifier(_proto.Index<DamageGroupPrototype>(_bruteGroupId), -(active.HealBrute * count));
            selfHealSpec += new DamageSpecifier(_proto.Index<DamageGroupPrototype>(_burnGroupId), -(active.HealBurn * count));
            _damageableSystem.TryChangeDamage(uid, selfHealSpec, true);

            if (TryComp<StaminaComponent>(uid, out var stam))
                _stamina.TakeStaminaDamage(uid, -active.HealStamina * count, stam);
        }

        return true;
    }

    private void UpdateDrainBeamNetwork(EntityUid vampire, List<EntityUid> targets, float range)
    {
        if (!TryComp<VampireDrainBeamComponent>(vampire, out var drainBeamComp))
            return;

        var requiredTargets = new HashSet<EntityUid>(targets);

        var toRemove = new List<EntityUid>();
        foreach (var (targetKey, connection) in drainBeamComp.ActiveBeams)
        {
            if (connection.Source != vampire)
            {
                var removeLegacy = new VampireDrainBeamEvent(GetNetEntity(connection.Source), GetNetEntity(connection.Target), false, drainBeamComp.VisualPrototype);
                RaiseNetworkEvent(removeLegacy);
                toRemove.Add(targetKey);
                continue;
            }

            if (!requiredTargets.Contains(connection.Target))
            {
                var removeEvent = new VampireDrainBeamEvent(GetNetEntity(connection.Source), GetNetEntity(connection.Target), false, drainBeamComp.VisualPrototype);
                RaiseNetworkEvent(removeEvent);

                toRemove.Add(targetKey);
            }
        }

        foreach (var key in toRemove)
            drainBeamComp.ActiveBeams.Remove(key);

        foreach (var target in requiredTargets)
        {
            if (!drainBeamComp.ActiveBeams.ContainsKey(target))
            {
                var connection = new DrainBeamConnection(vampire, target, range);
                drainBeamComp.ActiveBeams[target] = connection;

                var createEvent = new VampireDrainBeamEvent(GetNetEntity(vampire), GetNetEntity(target), true, drainBeamComp.VisualPrototype);
                RaiseNetworkEvent(createEvent);
            }
        }
    }

    private void OnPredatorSense(EntityUid uid, VampireComponent comp, ref VampireLocateMindActionEvent args)
    {
        var actionEntity = args.Action.Owner;
        if (args.Handled || !_vampire.CanUseVampireAbility(uid, comp, actionEntity))
            return;

        _predatorSenseUiActionEntities[uid] = actionEntity;

        _ui.CloseUi(uid, VampireLocateUiKey.Key);
        _ui.OpenUi(uid, VampireLocateUiKey.Key, uid);
        UpdatePredatorSenseUi(uid);

        args.Handled = true;
    }

    private void OnPredatorSenseUiOpened(EntityUid uid, VampireComponent comp, BoundUIOpenedEvent args)
    {
        if (!Equals(args.UiKey, VampireLocateUiKey.Key))
            return;

        UpdatePredatorSenseUi(uid);
    }

    private void OnPredatorSenseUiClosed(EntityUid uid, VampireComponent comp, BoundUIClosedEvent args)
    {
        if (!Equals(args.UiKey, VampireLocateUiKey.Key))
            return;

        _predatorSenseUiActionEntities.Remove(uid);
    }

    private void UpdatePredatorSenseUi(EntityUid uid)
    {
        var casterMap = Transform(uid).MapID;
        var targets = new List<VampireLocateTarget>();

        var query = AllEntityQuery<MindContainerComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var mindContainer, out var xform))
        {
            if (ent == uid)
                continue;

            if (!TryGetPredatorSenseTargetName(casterMap, ent, mindContainer, xform, out var display))
                continue;

            targets.Add(new VampireLocateTarget(GetNetEntity(ent), display));
        }

        targets.Sort(static (a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));
        _ui.SetUiState(uid, VampireLocateUiKey.Key, new VampireLocateBuiState { Targets = targets });
    }

    private void OnPredatorSenseSelected(EntityUid uid, VampireComponent comp, VampireLocateSelectedBuiMsg args)
    {
        if (args.Actor != uid)
            return;

        if (!_predatorSenseUiActionEntities.TryGetValue(uid, out var actionEntity) || !Exists(actionEntity))
            return;

        var target = GetEntity(args.Target);
        if (!Exists(target) || !TryComp<MindContainerComponent>(target, out var mindContainer))
            return;

        var xform = Transform(target);

        var casterMap = Transform(uid).MapID;
        if (!TryGetPredatorSenseTargetName(casterMap, target, mindContainer, xform, out var targetName))
        {
            _popup.PopupEntity(Loc.GetString("vampire-locate-unknown"), uid, uid, PopupType.MediumCaution);
            _ui.CloseUi(uid, VampireLocateUiKey.Key);
            return;
        }

        if (xform.MapID != casterMap)
        {
            _popup.PopupEntity(Loc.GetString("vampire-locate-not-same-sector"), uid, uid, PopupType.MediumCaution);
            return;
        }

        if (!_vampire.CheckAndConsumeBloodCost(uid, comp, actionEntity))
            return;

        var location = TryGetNearestWarpLocationName(target, out var loc)
            ? loc
            : Loc.GetString("vampire-locate-unknown");

        _popup.PopupEntity(Loc.GetString("vampire-locate-result", ("target", targetName), ("location", location)), uid, uid,
            PopupType.LargeCaution);

        _ui.CloseUi(uid, VampireLocateUiKey.Key);
    }

    private bool TryGetPredatorSenseTargetName(
        MapId casterMap,
        EntityUid target,
        MindContainerComponent mindContainer,
        TransformComponent xform,
        out string displayName)
    {
        displayName = string.Empty;

        if (!mindContainer.HasMind)
            return false;

        if (xform.MapID != casterMap)
            return false;

        if (HasComp<GhostComponent>(target) || !HasComp<HumanoidAppearanceComponent>(target))
            return false;

        var name = MetaData(target).EntityName;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        displayName = name;
        return true;
    }

    private bool TryGetNearestWarpLocationName(EntityUid target, out string location)
    {
        location = string.Empty;

        var targetXform = Transform(target);
        var targetMap = targetXform.MapID;
        var targetGrid = targetXform.GridUid;
        var targetPos = _transform.GetWorldPosition(targetXform);

        float bestDistSq = float.MaxValue;
        string? best = null;

        var warps = AllEntityQuery<WarpPointComponent, TransformComponent>();
        while (warps.MoveNext(out var warpUid, out var warp, out var warpXform))
        {
            if (_whitelist.IsWhitelistPass(warp.Blacklist, warpUid))
                continue;

            if (string.IsNullOrWhiteSpace(warp.Location))
                continue;

            if (warpXform.MapID != targetMap)
                continue;

            if (targetGrid != null && warpXform.GridUid != targetGrid)
                continue;

            var warpPos = _transform.GetWorldPosition(warpXform);
            var distSq = (warpPos - targetPos).LengthSquared();
            if (distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            best = warp.Location;
        }

        if (best == null)
            return false;

        location = best;
        return true;
    }
}
