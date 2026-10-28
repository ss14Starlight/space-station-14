using System.Numerics;
using Content.Server.NPC.Components;
using Content.Server.Speech.EntitySystems;
using Content.Server.Speech.Muting;
using Content.Shared._Starlight.FireDragon;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Chat;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC;
using Content.Shared.NPC.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.FireDragon;

public sealed partial class FireDragonSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedGunSystem _guns = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private OccluderSystem _occluder = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private MovementModStatusSystem _movementStatus = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private NpcFactionSystem _factions = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FireDragonComponent, NPCMeleeCombatComponent, ActiveNPCComponent>();
        while (query.MoveNext(out var uid, out var dragon, out var combat, out _))
        {
            var ent = new Entity<FireDragonComponent>(uid, dragon);
            if (!TryStartNpcDecision(ent, combat.Target, out var direction))
                continue;

            var distance = direction.Length();

            // HTN supplies the combat target; choose one ability for its current
            // distance instead of falling through every ready offensive ability.
            var attack = SelectNpcAttack(dragon, distance);
            if (TryPerformAbility(uid, attack, combat.Target) || TryNpcRoar(ent, combat.Target, distance))
                continue;

            TryNpcDash(ent, direction, distance);
        }
    }

    [SubscribeLocalEvent]
    private void OnInit(Entity<FireDragonComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.AncientFlameActionEntity, ent.Comp.AncientFlameAction);
        _actions.AddAction(ent, ref ent.Comp.TailSlamActionEntity, ent.Comp.TailSlamAction);
        _actions.AddAction(ent, ref ent.Comp.RoarActionEntity, ent.Comp.RoarAction);

        if (ent.Comp.WingDashEnabled)
            _actions.AddAction(ent, ref ent.Comp.WingDashActionEntity, ent.Comp.WingDashAction);

        _actions.AddAction(ent, ref ent.Comp.FirebreathActionEntity, ent.Comp.FirebreathAction);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<FireDragonComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Comp.AncientFlameActionEntity);
        _actions.RemoveAction(ent.Comp.TailSlamActionEntity);
        _actions.RemoveAction(ent.Comp.RoarActionEntity);
        _actions.RemoveAction(ent.Comp.WingDashActionEntity);
        _actions.RemoveAction(ent.Comp.FirebreathActionEntity);
    }

    [SubscribeLocalEvent]
    private void OnAncientFlame(Entity<FireDragonComponent> ent, ref FireDragonAncientFlameEvent args)
    {
        if (!CanUse(ent, args))
            return;

        var origin = _transform.GetMapCoordinates(ent);
        var target = _transform.ToMapCoordinates(args.Target);
        if (target.MapId != origin.MapId
            || Vector2.Distance(origin.Position, target.Position) > ent.Comp.AbilityRange)
            return;

        var direction = target.Position - origin.Position;
        if (direction.LengthSquared() < 0.01f)
            return;

        var projectile = Spawn(ent.Comp.Projectile, origin);
        _guns.ShootProjectile(projectile, direction, Vector2.Zero, ent, ent, ent.Comp.ProjectileSpeed);
        FinishAbility(ent, args);
    }

    [SubscribeLocalEvent]
    private void OnTailSlam(Entity<FireDragonComponent> ent, ref FireDragonTailSlamEvent args)
    {
        if (!CanUse(ent, args) || !TryStartWave(ent, out var wave))
            return;

        wave.Rings = true;
        // Two rings cover a maximum 5x5 footprint, excluding the dragon's tile.
        wave.Length = Math.Clamp(ent.Comp.SlamRadius, 1, 2);
        wave.Interval = ent.Comp.SlamRingInterval;
        wave.GroundFireLifetimeOverride = ent.Comp.SlamFireLifetime;
        FinishAbility(ent, args);
    }

    [SubscribeLocalEvent]
    private void OnFirebreath(Entity<FireDragonComponent> ent, ref FireDragonFirebreathEvent args)
    {
        if (!CanUse(ent, args))
            return;

        var origin = _transform.GetMapCoordinates(ent);
        var target = _transform.ToMapCoordinates(args.Target);
        if (origin.MapId != target.MapId
            || Vector2.Distance(origin.Position, target.Position) > ent.Comp.AbilityRange
            || !_occluder.InRangeUnoccluded(origin, target, 0f, ignoreTouching: true))
            return;

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid)
            return;

        var inverseRotation = new Angle(-_transform.GetWorldRotation(gridUid).Theta);
        var localDirection = inverseRotation.RotateVec(target.Position - origin.Position);
        if (localDirection.LengthSquared() < 0.01f || !TryStartWave(ent, out var wave))
            return;

        wave.Forward = Vector2.Normalize(localDirection);
        wave.BreathOrigin = _transform.ToCoordinates(gridUid, origin).Position;
        wave.Width = ent.Comp.BreathWidth;
        wave.Length = ent.Comp.BreathLength;
        wave.Interval = ent.Comp.BreathRowInterval;
        PrepareBreathPositions(wave, gridUid);
        FinishAbility(ent, args);
    }

    [SubscribeLocalEvent]
    private void OnRoar(Entity<FireDragonComponent> ent, ref FireDragonRoarEvent args)
    {
        if (!CanUse(ent, args))
            return;

        var origin = _transform.GetMapCoordinates(ent);
        foreach (var target in _lookup.GetEntitiesInRange(origin, ent.Comp.RoarRange))
        {
            if (!HasComp<MovementSpeedModifierComponent>(target)
                || !_mobState.IsAlive(target)
                || _factions.IsEntityFriendly(ent.Owner, target)
                || !_occluder.InRangeUnoccluded(origin, _transform.GetMapCoordinates(target), 0f, ignoreTouching: true))
                continue;

            _movementStatus.TryUpdateMovementSpeedModDuration(target, ent.Comp.HuntEffect,
                ent.Comp.RoarDuration, ent.Comp.RoarSpeedModifier);
        }

        FinishAbility(ent, args);
    }

    [SubscribeLocalEvent(before: [typeof(VocalSystem)], after: [typeof(MutingSystem)])]
    private void OnRoarEmote(Entity<FireDragonComponent> ent, ref EmoteEvent args)
    {
        if (args.Handled || args.Emote.ID != ent.Comp.RoarEmote)
            return;

        // Chat and the ability button share the same effect, sound, and cooldown.
        args.Handled = TryPerformAbility(ent, ent.Comp.RoarActionEntity);
    }

    [SubscribeLocalEvent]
    private void OnWingDash(Entity<FireDragonComponent> ent, ref FireDragonWingDashEvent args)
    {
        if (!ent.Comp.WingDashEnabled || !CanUse(ent, args) || !_blocker.CanMove(ent))
            return;

        var dash = new FireDragonDashComponent
        {
            Direction = _transform.GetWorldRotation(ent).ToWorldVec(),
            Speed = ent.Comp.DashSpeed,
            EndTime = _timing.CurTime + ent.Comp.DashDuration,
        };
        AddComp(ent, dash);
        FinishAbility(ent, args);
    }

    private bool TryStartNpcDecision(Entity<FireDragonComponent> ent, EntityUid target, out Vector2 direction)
    {
        direction = default;
        if (!IsReadyForNpcAbility(ent) || !IsEnemyTarget(ent, target))
            return false;

        ent.Comp.NextNpcAction = _timing.CurTime + ent.Comp.NpcDecisionInterval;
        var origin = _transform.GetMapCoordinates(ent);
        var targetPosition = _transform.GetMapCoordinates(target);
        if (origin.MapId != targetPosition.MapId
            || !_occluder.InRangeUnoccluded(origin, targetPosition, 0f, ignoreTouching: true))
            return false;

        direction = targetPosition.Position - origin.Position;
        return true;
    }

    private bool IsReadyForNpcAbility(Entity<FireDragonComponent> ent) =>
        _timing.CurTime >= ent.Comp.NextNpcAction
        && _timing.CurTime >= ent.Comp.NextAbility
        && _mobState.IsAlive(ent)
        && _blocker.CanAttack(ent)
        && !HasComp<FireDragonDashComponent>(ent);

    private bool IsEnemyTarget(EntityUid source, EntityUid target) =>
        Exists(target) && _mobState.IsAlive(target) && !_factions.IsEntityFriendly(source, target);

    private EntityUid? SelectNpcAttack(FireDragonComponent dragon, float distance)
    {
        if (distance <= Math.Clamp(dragon.SlamRadius, 1, 2))
            return dragon.TailSlamActionEntity;

        if (distance <= dragon.NpcBreathRange)
            return dragon.FirebreathActionEntity;

        if (distance <= dragon.AbilityRange)
            return dragon.AncientFlameActionEntity;

        return null;
    }

    private bool TryNpcRoar(Entity<FireDragonComponent> ent, EntityUid target, float distance) =>
        distance <= ent.Comp.RoarRange
        && HasComp<MovementSpeedModifierComponent>(target)
        && !_statusEffects.HasEffectComp<PredatorHuntStatusEffectComponent>(target)
        && TryPerformAbility(ent, ent.Comp.RoarActionEntity);

    private void TryNpcDash(Entity<FireDragonComponent> ent, Vector2 direction, float distance)
    {
        if (!ent.Comp.WingDashEnabled
            || distance <= ent.Comp.NpcDashMinRange
            || distance > ent.Comp.AbilityRange)
            return;

        _transform.SetWorldRotation(ent, direction.ToWorldAngle());
        TryPerformAbility(ent, ent.Comp.WingDashActionEntity);
    }

    private bool TryPerformAbility(EntityUid uid, EntityUid? actionUid, EntityUid? target = null)
    {
        if (_actions.GetAction(actionUid) is not { } action || !_actions.ValidAction(action))
            return false;

        if (HasComp<WorldTargetActionComponent>(action))
        {
            if (target is not { } targetUid)
                return false;

            _actions.SetEventTarget(action, targetUid);
        }

        _actions.PerformAction(uid, action, predicted: false);
        return _actions.IsCooldownActive(action.Comp);
    }

    private bool CanUse(Entity<FireDragonComponent> ent, BaseActionEvent args) =>
        !args.Handled
        && _timing.CurTime >= ent.Comp.NextAbility
        && _mobState.IsAlive(ent)
        && _blocker.CanAttack(ent)
        && !HasComp<FireDragonDashComponent>(ent);

    private void FinishAbility(Entity<FireDragonComponent> ent, BaseActionEvent args)
    {
        args.Handled = true;
        var recovery = ent.Comp.AbilityRecovery;
        var usedAction = args.Action.Owner;
        ent.Comp.NextAbility = _timing.CurTime + recovery;

        SetRecoveryCooldown(ent.Comp.AncientFlameActionEntity, usedAction, recovery);
        SetRecoveryCooldown(ent.Comp.TailSlamActionEntity, usedAction, recovery);
        SetRecoveryCooldown(ent.Comp.RoarActionEntity, usedAction, recovery);
        SetRecoveryCooldown(ent.Comp.WingDashActionEntity, usedAction, recovery);
        SetRecoveryCooldown(ent.Comp.FirebreathActionEntity, usedAction, recovery);

        // These abilities run only on the server, so their sounds cannot rely on client prediction.
        _audio.PlayPvs(ent.Comp.AbilitySound, ent);
    }

    private void SetRecoveryCooldown(EntityUid? actionUid, EntityUid usedAction, TimeSpan recovery)
    {
        // SharedActionsSystem applies the used action's full cooldown after
        // its event returns. Starting a short cooldown on it here would skip that.
        if (actionUid is { } uid && uid != usedAction)
            _actions.SetIfBiggerCooldown(uid, recovery);
    }

    private bool TryStartWave(Entity<FireDragonComponent> ent, out DragonFireWaveComponent wave)
    {
        wave = default!;
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var tile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        var uid = Spawn(null, _map.GridTileToLocal(gridUid, grid, tile));
        wave = AddComp<DragonFireWaveComponent>(uid);
        wave.Source = ent;
        wave.Grid = gridUid;
        wave.Origin = tile;
        wave.GroundFirePrototype = ent.Comp.GroundFirePrototype;
        wave.BreathStepDistance = ent.Comp.BreathStepDistance;
        wave.Distance = ent.Comp.BreathStepDistance;
        wave.NextStep = _timing.CurTime + ent.Comp.WaveStartDelay;

        return true;
    }

    private void PrepareBreathPositions(DragonFireWaveComponent wave, EntityUid gridUid)
    {
        var grid = Comp<MapGridComponent>(gridUid);
        var side = new Vector2(-wave.Forward.Y, wave.Forward.X);
        var halfWidth = (wave.Width - 1) / 2f;
        // Keep the flame centers on the aim vector. Rounding them to tile centers
        // hides shallow angles and shifts the lane when the dragon is off-center.
        for (var row = 1; row <= wave.Length; row++)
        for (var column = 0; column < wave.Width; column++)
        {
            var offset = (wave.Forward * row) + (side * (column - halfWidth));
            wave.BreathPositions.Add((wave.BreathOrigin + (offset * grid.TileSize), row));
        }
    }
}
