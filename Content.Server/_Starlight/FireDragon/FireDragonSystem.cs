using System.Numerics;
using Content.Server.NPC.Components;
using Content.Server.Speech.EntitySystems;
using Content.Server.Speech.Muting;
using Content.Shared._Starlight.FireDragon;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Chat;
using Content.Shared.Movement.Components;
using Content.Shared.NPC;
using Content.Shared.StatusEffectNew;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;

namespace Content.Server._Starlight.FireDragon;

public sealed partial class FireDragonSystem : SharedFireDragonSystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedGunSystem _guns = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FireDragonAbilitiesComponent, NPCMeleeCombatComponent, ActiveNPCComponent>();
        while (query.MoveNext(out var uid, out var dragon, out var combat, out _))
        {
            var ent = new Entity<FireDragonAbilitiesComponent>(uid, dragon);
            if (!TryStartNpcDecision(ent, combat.Target, out var direction))
                continue;

            var distance = direction.Length();

            // HTN supplies the combat target; choose one ability for its current
            // distance instead of falling through every ready offensive ability.
            var attack = SelectNpcAttack(ent, distance);
            if (TryPerformAbility(uid, attack, combat.Target) || TryNpcRoar(ent, combat.Target, distance))
                continue;

            TryNpcDash(ent, direction, distance);
        }
    }

    [SubscribeLocalEvent]
    private void OnInit(Entity<FireDragonAbilitiesComponent> ent, ref MapInitEvent args)
    {
        var state = Comp<FireDragonComponent>(ent);
        Actions.AddAction(ent, ref state.AncientFlameActionEntity, ent.Comp.AncientFlameAction);
        Actions.AddAction(ent, ref state.TailSlamActionEntity, ent.Comp.TailSlamAction);
        Actions.AddAction(ent, ref state.RoarActionEntity, state.RoarAction);

        if (ent.Comp.WingDashEnabled)
            Actions.AddAction(ent, ref state.WingDashActionEntity, ent.Comp.WingDashAction);

        Actions.AddAction(ent, ref state.FirebreathActionEntity, ent.Comp.FirebreathAction);
        Dirty(ent, state);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<FireDragonAbilitiesComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<FireDragonComponent>(ent, out var state))
            return;

        Actions.RemoveAction(state.AncientFlameActionEntity);
        Actions.RemoveAction(state.TailSlamActionEntity);
        Actions.RemoveAction(state.RoarActionEntity);
        Actions.RemoveAction(state.WingDashActionEntity);
        Actions.RemoveAction(state.FirebreathActionEntity);
    }

    [SubscribeLocalEvent]
    private void OnAncientFlame(Entity<FireDragonAbilitiesComponent> ent, ref FireDragonAncientFlameEvent args)
    {
        if (!CanUseServerAbility(ent, args))
            return;

        var origin = TransformSystem.GetMapCoordinates(ent);
        var target = TransformSystem.ToMapCoordinates(args.Target);
        if (target.MapId != origin.MapId
            || Vector2.Distance(origin.Position, target.Position) > ent.Comp.AbilityRange)
            return;

        var direction = target.Position - origin.Position;
        if (direction.LengthSquared() < 0.01f)
            return;

        var projectile = Spawn(ent.Comp.Projectile, origin);
        _guns.ShootProjectile(projectile, direction, Vector2.Zero, ent, ent, ent.Comp.ProjectileSpeed);
        FinishServerAbility(ent, args);
    }

    [SubscribeLocalEvent]
    private void OnTailSlam(Entity<FireDragonAbilitiesComponent> ent, ref FireDragonTailSlamEvent args)
    {
        if (!CanUseServerAbility(ent, args) || !TryStartWave(ent, out var wave))
            return;

        wave.Rings = true;
        // Two rings cover a maximum 5x5 footprint, excluding the dragon's tile.
        wave.Length = Math.Clamp(ent.Comp.SlamRadius, 1, 2);
        wave.Interval = ent.Comp.SlamRingInterval;
        wave.GroundFireLifetimeOverride = ent.Comp.SlamFireLifetime;
        FinishServerAbility(ent, args);
    }

    [SubscribeLocalEvent]
    private void OnFirebreath(Entity<FireDragonAbilitiesComponent> ent, ref FireDragonFirebreathEvent args)
    {
        if (!CanUseServerAbility(ent, args))
            return;

        var origin = TransformSystem.GetMapCoordinates(ent);
        var target = TransformSystem.ToMapCoordinates(args.Target);
        if (origin.MapId != target.MapId
            || Vector2.Distance(origin.Position, target.Position) > ent.Comp.AbilityRange
            || !Occluder.InRangeUnoccluded(origin, target, 0f, ignoreTouching: true))
            return;

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid)
            return;

        var inverseRotation = new Angle(-TransformSystem.GetWorldRotation(gridUid).Theta);
        var localDirection = inverseRotation.RotateVec(target.Position - origin.Position);
        if (localDirection.LengthSquared() < 0.01f || !TryStartWave(ent, out var wave))
            return;

        wave.Forward = Vector2.Normalize(localDirection);
        wave.BreathOrigin = TransformSystem.ToCoordinates(gridUid, origin).Position;
        wave.Width = ent.Comp.BreathWidth;
        wave.Length = ent.Comp.BreathLength;
        wave.Interval = ent.Comp.BreathRowInterval;
        PrepareBreathPositions(wave, gridUid);
        FinishServerAbility(ent, args);
    }

    [SubscribeLocalEvent(before: [typeof(VocalSystem)], after: [typeof(MutingSystem)])]
    private void OnRoarEmote(Entity<FireDragonAbilitiesComponent> ent, ref EmoteEvent args)
    {
        if (args.Handled || args.Emote.ID != ent.Comp.RoarEmote
            || !TryComp<FireDragonComponent>(ent, out var state))
            return;

        // Chat and the ability button share the same effect, sound, and cooldown.
        args.Handled = TryPerformAbility(ent, state.RoarActionEntity);
    }

    [SubscribeLocalEvent]
    private void OnWingDash(Entity<FireDragonAbilitiesComponent> ent, ref FireDragonWingDashEvent args)
    {
        if (!ent.Comp.WingDashEnabled || !CanUseServerAbility(ent, args) || !Blocker.CanMove(ent))
            return;

        var dash = new FireDragonDashComponent
        {
            Direction = TransformSystem.GetWorldRotation(ent).ToWorldVec(),
            Speed = ent.Comp.DashSpeed,
            EndTime = Timing.CurTime + ent.Comp.DashDuration,
        };
        AddComp(ent, dash);
        FinishServerAbility(ent, args);
    }

    private bool TryStartNpcDecision(Entity<FireDragonAbilitiesComponent> ent, EntityUid target, out Vector2 direction)
    {
        direction = default;
        if (!IsReadyForNpcAbility(ent) || !IsEnemyTarget(ent, target))
            return false;

        ent.Comp.NextNpcAction = Timing.CurTime + ent.Comp.NpcDecisionInterval;
        var origin = TransformSystem.GetMapCoordinates(ent);
        var targetPosition = TransformSystem.GetMapCoordinates(target);
        if (origin.MapId != targetPosition.MapId
            || !Occluder.InRangeUnoccluded(origin, targetPosition, 0f, ignoreTouching: true))
            return false;

        direction = targetPosition.Position - origin.Position;
        return true;
    }

    private bool IsReadyForNpcAbility(Entity<FireDragonAbilitiesComponent> ent) =>
        Timing.CurTime >= ent.Comp.NextNpcAction
        && TryComp<FireDragonComponent>(ent, out var state)
        && IsReadyForAbility((ent.Owner, state));

    private bool IsEnemyTarget(EntityUid source, EntityUid target) =>
        Exists(target) && MobState.IsAlive(target) && !Factions.IsEntityFriendly(source, target);

    private EntityUid? SelectNpcAttack(Entity<FireDragonAbilitiesComponent> ent, float distance)
    {
        var state = Comp<FireDragonComponent>(ent);
        if (distance <= Math.Clamp(ent.Comp.SlamRadius, 1, 2))
            return state.TailSlamActionEntity;

        if (distance <= ent.Comp.NpcBreathRange)
            return state.FirebreathActionEntity;

        if (distance <= ent.Comp.AbilityRange)
            return state.AncientFlameActionEntity;

        return null;
    }

    private bool TryNpcRoar(Entity<FireDragonAbilitiesComponent> ent, EntityUid target, float distance) =>
        TryComp<FireDragonComponent>(ent, out var state)
        && distance <= state.RoarRange
        && HasComp<MovementSpeedModifierComponent>(target)
        && !_statusEffects.HasEffectComp<PredatorHuntStatusEffectComponent>(target)
        && TryPerformAbility(ent, state.RoarActionEntity);

    private void TryNpcDash(Entity<FireDragonAbilitiesComponent> ent, Vector2 direction, float distance)
    {
        if (!ent.Comp.WingDashEnabled
            || distance <= ent.Comp.NpcDashMinRange
            || distance > ent.Comp.AbilityRange)
            return;

        TransformSystem.SetWorldRotation(ent, direction.ToWorldAngle());
        if (TryComp<FireDragonComponent>(ent, out var state))
            TryPerformAbility(ent, state.WingDashActionEntity);
    }

    private bool TryPerformAbility(EntityUid uid, EntityUid? actionUid, EntityUid? target = null)
    {
        if (Actions.GetAction(actionUid) is not { } action || !Actions.ValidAction(action))
            return false;

        var input = new RequestPerformActionEvent(GetNetEntity(action));
        if (HasComp<WorldTargetActionComponent>(action))
        {
            if (target is not { } targetUid || !Exists(targetUid))
                return false;

            input = new RequestPerformActionEvent(GetNetEntity(action), GetNetCoordinates(Transform(targetUid).Coordinates));
        }

        // NPCs and emotes must respect the same restrictions as the ability button.
        var attempt = new ActionAttemptEvent(uid);
        RaiseLocalEvent(action, ref attempt);
        if (attempt.Cancelled)
            return false;

        var validate = new ActionValidateEvent
        {
            Input = input,
            User = uid,
            Provider = action.Comp.Container ?? uid,
        };
        RaiseLocalEvent(action, ref validate);
        if (validate.Invalid)
            return false;

        Actions.PerformAction(uid, action, predicted: false);
        return Actions.IsCooldownActive(action.Comp);
    }

    private bool CanUseServerAbility(Entity<FireDragonAbilitiesComponent> ent, BaseActionEvent args) =>
        TryComp<FireDragonComponent>(ent, out var state) && CanUse((ent.Owner, state), args);

    private void FinishServerAbility(Entity<FireDragonAbilitiesComponent> ent, BaseActionEvent args)
    {
        FinishAbility((ent.Owner, Comp<FireDragonComponent>(ent)), args);
        _audio.PlayPvs(ent.Comp.AbilitySound, ent);
    }

    private bool TryStartWave(Entity<FireDragonAbilitiesComponent> ent, out DragonFireWaveComponent wave)
    {
        wave = default!;
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var tile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        var uid = SpawnAttachedTo(null, _map.GridTileToLocal(gridUid, grid, tile));
        wave = AddComp<DragonFireWaveComponent>(uid);
        wave.Source = ent;
        wave.Grid = gridUid;
        wave.Origin = tile;
        wave.GroundFirePrototype = ent.Comp.GroundFirePrototype;
        wave.BreathStepDistance = ent.Comp.BreathStepDistance;
        wave.Distance = ent.Comp.BreathStepDistance;
        wave.NextStep = Timing.CurTime + ent.Comp.WaveStartDelay;

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
