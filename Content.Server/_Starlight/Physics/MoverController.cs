using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Content.Server.Shuttles.Systems;
using Content.Shared._Starlight.Movement;
using Content.Shared._Starlight.Sound;
using Content.Shared.Maps;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared._Starlight.CCVar;
using Prometheus;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Threading;
using Robust.Shared.Timing;
using DependencyAttribute = Robust.Shared.IoC.DependencyAttribute;
using DroneConsoleComponent = Content.Server.Shuttles.DroneConsoleComponent;
using Stopwatch = System.Diagnostics.Stopwatch;
using Content.Shared._Starlight.Shuttles.Components;

namespace Content.Server._Starlight.Physics;

public sealed partial class SLMoverController : SharedMoverController
{
    private static readonly Gauge _activeMoverGauge = Metrics.CreateGauge(
        "physics_active_mover_count",
        "InputMovers processed in parallel by MoverController");

    private static readonly Gauge _activePrioritizedMoverGauge = Metrics.CreateGauge(
        "physics_active_prioritized_mover_count",
        "Prioritized (relay-source) InputMovers processed serially by MoverController");

    private static readonly Gauge _movedMoverGauge = Metrics.CreateGauge(
        "physics_moved_mover_count",
        "InputMovers whose velocity actually changed this tick");

    // per-phase wall time of UpdateBeforeSolve. histogram so we get percentiles over a scrape window
    // instead of the old log line. children are cached below so the hot path skips the label lookup.
    private static readonly Histogram _moverUpdateDuration = Metrics.CreateHistogram(
        "physics_mover_update_duration_seconds",
        "Time spent per phase of MoverController.UpdateBeforeSolve",
        new HistogramConfiguration
        {
            LabelNames = ["phase"],
            Buckets = Histogram.ExponentialBuckets(0.00005, 2, 10), // 50us .. ~25ms
        });

    private readonly IHistogram _durBuild = _moverUpdateDuration.WithLabels("build");
    private readonly IHistogram _durPrio = _moverUpdateDuration.WithLabels("prio_serial");
    private readonly IHistogram _durParallel = _moverUpdateDuration.WithLabels("parallel");
    private readonly IHistogram _durScatter = _moverUpdateDuration.WithLabels("scatter");
    private readonly IHistogram _durDirtySound = _moverUpdateDuration.WithLabels("dirty_sound");
    private readonly IHistogram _durShuttle = _moverUpdateDuration.WithLabels("shuttle");
    private readonly IHistogram _durTotal = _moverUpdateDuration.WithLabels("total");

    [Dependency] private ThrusterSystem _thruster = default!;
    [Dependency] private SharedTransformSystem _xformSystem = default!;
    [Dependency] private IParallelManager _parallel = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private HandleMobMovementJob _handleMobMovementJob;

    // this runs once per substep (2 by default). input doesn't change mid-tick and only the last
    // substep gets networked, so just solve once and coast the rest.
    private bool _substepGating;
    private GameTick _lastUpdateTick;
    // who was active on the first substep. UpdateAfterSolve nukes the used-set every substep so we
    // re-stamp it on coasted ones, otherwise friction/conveyor start grabbing our movers.
    private readonly List<EntityUid> _usedMovers = new();

    // Worker threads must not touch shared dirty/PVS state. Dirty calls raised during the parallel
    // mover pass are deferred here and flushed on the main thread.
    private volatile bool _inParallelMove;
    private readonly ConcurrentQueue<(EntityUid Uid, InputMoverComponent Mover)> _deferredDirty = new();

    private Dictionary<EntityUid, (ShuttleComponent, List<(EntityUid, PilotComponent, TransformComponent)>)> _shuttlePilots = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RelayInputMoverComponent, PlayerAttachedEvent>(OnRelayPlayerAttached);
        SubscribeLocalEvent<RelayInputMoverComponent, PlayerDetachedEvent>(OnRelayPlayerDetached);
        SubscribeLocalEvent<InputMoverComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<InputMoverComponent, PlayerDetachedEvent>(OnPlayerDetached);

        Subs.CVar(_cfg, StarlightCCVars.PhysicsMoverSubstepGating, value => _substepGating = value, true);

        _handleMobMovementJob = new HandleMobMovementJob(this);
    }

    private void OnRelayPlayerAttached(Entity<RelayInputMoverComponent> entity, ref PlayerAttachedEvent args)
    {
        if (MoverQuery.TryGetComponent(entity.Comp.RelayEntity, out var inputMover))
            SetMoveInput((entity.Comp.RelayEntity, inputMover), MoveButtons.None);
    }

    private void OnRelayPlayerDetached(Entity<RelayInputMoverComponent> entity, ref PlayerDetachedEvent args)
    {
        if (MoverQuery.TryGetComponent(entity.Comp.RelayEntity, out var inputMover))
            SetMoveInput((entity.Comp.RelayEntity, inputMover), MoveButtons.None);
    }

    private void OnPlayerAttached(Entity<InputMoverComponent> entity, ref PlayerAttachedEvent args)
        => SetMoveInput(entity, MoveButtons.None);

    private void OnPlayerDetached(Entity<InputMoverComponent> entity, ref PlayerDetachedEvent args)
        => SetMoveInput(entity, MoveButtons.None);

    protected override bool CanSound()
        => true;

    protected override void DirtyMover(EntityUid uid, InputMoverComponent mover)
    {
        // Dirty -> EntityDirtied -> PVS is not thread-safe and contends across cores. While movers are
        // processed in parallel we queue the dirty and apply it on the main thread after the job ends.
        if (_inParallelMove)
            _deferredDirty.Enqueue((uid, mover));
        else
            base.DirtyMover(uid, mover);
    }

    private readonly HashSet<EntityUid> _moverAdded = new();
    private readonly List<Entity<InputMoverComponent>> _movers = [];
    private readonly List<Entity<InputMoverComponent>> _prioritizedMovers = [];

    private void InsertMover(Entity<InputMoverComponent> source, bool prioritized = false)
    {
        if (TryComp(source, out MovementRelayTargetComponent? relay))
        {
            if (TryComp(relay.Source, out InputMoverComponent? relayMover))
            {
                InsertMover((relay.Source, relayMover), true);
            }
        }

        // Already added
        if (!_moverAdded.Add(source.Owner))
            return;

        // A relay source mutates *another* entity's mover component (its relay target), so it can't be
        // run concurrently with that target. Everything else — including ordinary players — is pure
        // value computation in the parallel pass (writes are applied on the main thread during scatter),
        // so it no longer needs to be spun serially.
        if (prioritized || RelayQuery.HasComp(source.Owner))
            _prioritizedMovers.Add(source);
        else
            _movers.Add(source);
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        // already did this tick -> coast. velocity's still on the bodies, just put the used-set back.
        // shuttles can't coast (they integrate force every substep) so keep running those.
        if (_substepGating)
        {
            if (Timing.CurTick == _lastUpdateTick)
            {
                for (var i = 0; i < _usedMovers.Count; i++)
                    UsedMobMovement[_usedMovers[i]] = true;

                HandleShuttleMovement(frameTime);
                return;
            }
            _lastUpdateTick = Timing.CurTick;
        }

        // solve once but physics still integrates every substep, so feed it the whole tick. a substep
        // slice would run accel at half rate and the client (which sees full ticks) starts rubber-banding.
        var moverFrameTime = _substepGating ? (float)Timing.TickPeriod.TotalSeconds : frameTime;

        var perfStart = Stopwatch.GetTimestamp();

        _usedMovers.Clear();
        _moverAdded.Clear();
        _prioritizedMovers.Clear();
        _movers.Clear();

        // Players used to be forced onto the serial path; they now flow through InsertMover like any
        // other mover and only end up prioritized if they're a relay source.
        foreach ( var session in _players.Sessions)
            if(session.AttachedEntity.HasValue
                && TryComp<InputMoverComponent>(session.AttachedEntity.Value, out var mover))
                InsertMover((session.AttachedEntity.Value, mover));

        var inputQueryEnumerator = EntityQueryEnumerator<InputMoverComponent>();
        while (inputQueryEnumerator.MoveNext(out var uid, out var mover))
            InsertMover((uid, mover));

        var perfBuilt = Stopwatch.GetTimestamp();

        foreach (var mover in _prioritizedMovers)
        {
            HandleMobMovement(mover, moverFrameTime);
            // base already wrote UsedMobMovement, grab the active ones so we can replay them while coasting.
            if (_substepGating && UsedMobMovement.TryGetValue(mover.Owner, out var prioUsed) && prioUsed)
                _usedMovers.Add(mover.Owner);
        }

        var perfPrio = Stopwatch.GetTimestamp();

        _handleMobMovementJob.FrameTime = moverFrameTime;
        _handleMobMovementJob.Prepare(_movers);

        _inParallelMove = true;
        try
        {
            _parallel.ProcessNow(_handleMobMovementJob, _movers.Count);
        }
        finally
        {
            _inParallelMove = false;
        }

        var perfParallel = Stopwatch.GetTimestamp();

        var velocities = _handleMobMovementJob.Velocities;
        var rotations = _handleMobMovementJob.Rotations;
        var used = _handleMobMovementJob.Used;
        var bodies = _handleMobMovementJob.Bodies;
        var xforms = _handleMobMovementJob.Xforms;
        var movedCount = 0;
        for (var i = 0; i < velocities.Length; i++)
        {
            ref readonly var velocity = ref velocities[i];
            ref readonly var rotation = ref rotations[i];
            var (uid, mover) = _movers[i];

            // workers only read this, write it back here on the main thread.
            UsedMobMovement[uid] = used[i];
            if (_substepGating && used[i])
                _usedMovers.Add(uid);

            if (velocity.HasValue)
            {
                movedCount++;
                // hand it the body the worker already resolved so it doesn't do the dict lookup again (x2).
                var body = bodies[i];
                PhysicsSystem.SetLinearVelocity(uid, velocity.Value, body: body);
                // nothing spins normally, don't bother with the angular write unless there's spin to kill.
                if (body!.AngularVelocity != 0f)
                    PhysicsSystem.SetAngularVelocity(uid, 0, body: body);
            }
            if (rotation.HasValue)
                _transform.SetLocalRotation(uid, rotation.Value, xforms[i]);

        }

        var perfScatter = Stopwatch.GetTimestamp();

        // Flush the dirties deferred from the worker threads.
        while (_deferredDirty.TryDequeue(out var deferred))
            Dirty(deferred.Uid, deferred.Mover);

        foreach (ref readonly var sound in _handleMobMovementJob.Sounds)
            if (sound.HasValue)
                _audio.PlayPredicted(sound.Value.SoundSpecifier, sound.Value.Source, sound.Value.User, sound.Value.AudioParams);

        var perfDirtySounds = Stopwatch.GetTimestamp();

        HandleShuttleMovement(frameTime);

        var perfEnd = Stopwatch.GetTimestamp();

        _activeMoverGauge.Set(_movers.Count);
        _activePrioritizedMoverGauge.Set(_prioritizedMovers.Count);
        _movedMoverGauge.Set(movedCount);

        static double Sec(long a, long b) => Stopwatch.GetElapsedTime(a, b).TotalSeconds;
        _durBuild.Observe(Sec(perfStart, perfBuilt));
        _durPrio.Observe(Sec(perfBuilt, perfPrio));
        _durParallel.Observe(Sec(perfPrio, perfParallel));
        _durScatter.Observe(Sec(perfParallel, perfScatter));
        _durDirtySound.Observe(Sec(perfScatter, perfDirtySounds));
        _durShuttle.Observe(Sec(perfDirtySounds, perfEnd));
        _durTotal.Observe(Sec(perfStart, perfEnd));
    }

    public Vector2? HandleAIMobMovement(
        Entity<InputMoverComponent> entity,
        float frameTime,
        out SoundEvent? soundEvent,
        out Angle? rotation,
        out bool used,
        out PhysicsComponent? resolvedBody,
        out TransformComponent? resolvedXform)
    {
        soundEvent = null;
        rotation = null;
        used = false;
        // give these back so the scatter loop reuses them instead of re-querying.
        resolvedBody = null;
        resolvedXform = null;
        var uid = entity.Owner;
        var mover = entity.Comp;

        if (!XformQuery.TryComp(entity.Owner, out var xform))
            return null;

        // Update relative movement
        if (mover.LerpTarget < Timing.CurTime)
        {
            TryUpdateRelative(uid, mover, xform);
        }

        LerpRotation(uid, mover, frameTime);

        // If we can't move then just use tile-friction / no movement handling.
        if (!mover.CanMove
            || !PhysicsQuery.TryComp(uid, out var physicsComponent)
            || (PullableQuery.TryGetComponent(uid, out var pullable) && pullable.BeingPulled))
        {
            return null;
        }

        // If the body is in air but isn't weightless then it can't move
        var weightless = _gravity.IsWeightless(uid);
        var inAirHelpless = false;

        if (physicsComponent.BodyStatus != BodyStatus.OnGround && !CanMoveInAirQuery.HasComponent(uid))
        {
            if (!weightless)
            {
                return null;
            }
            inAirHelpless = true;
        }

        used = true;

        var moveSpeedComponent = ModifierQuery.CompOrNull(uid);

        // Idle fast-path. The overwhelming majority of movers stand still on any given tick, and the
        // controller runs at physics rate (2x networking), so the heavy work below is mostly wasted.
        // When there is no movement input AND the body has already settled (linear speed under the
        // friction floor, no residual spin) the full pass is provably a no-op: Friction() early-returns
        // below MinimumFrictionSpeed, Accelerate() early-returns with a zero wish dir, and the
        // rotation/footstep blocks only run for a non-zero wish dir. So we skip IsWeightless, the
        // weightless event, tile lookups and sound resolution entirely and leave velocity untouched.
        var (idleWalk, idleSprint) = GetVelocityInput(mover);
        if (idleWalk == Vector2.Zero
            && idleSprint == Vector2.Zero
            && physicsComponent.AngularVelocity == 0f)
        {
            var minFrictionSpeed = moveSpeedComponent?.MinimumFrictionSpeed ?? MovementSpeedModifierComponent.DefaultMinimumFrictionSpeed;
            if (physicsComponent.LinearVelocity.LengthSquared() < minFrictionSpeed * minFrictionSpeed)
            {
                // WishDir is already zero for a settled mover; this is a no-op in the common case and
                // only clears a stale value (deferred-dirtied on the main thread) on the stopping tick.
                SetWishDir((uid, mover), Vector2.Zero);
                return null;
            }
        }

        float friction;
        float accel;
        Vector2 wishDir;
        var velocity = physicsComponent.LinearVelocity;

        // Get current tile def for things like speed/friction mods
        ContentTileDefinition? tileDef = null;

        var touching = false;
        // Whether we use tilefriction or not
        if (weightless || inAirHelpless)
        {
            // Find the speed we should be moving at and make sure we're not trying to move faster than that
            var walkSpeed = moveSpeedComponent?.WeightlessWalkSpeed ?? MovementSpeedModifierComponent.DefaultBaseWalkSpeed;
            var sprintSpeed = moveSpeedComponent?.WeightlessSprintSpeed ?? MovementSpeedModifierComponent.DefaultBaseSprintSpeed;

            wishDir = AssertValidWish(mover, walkSpeed, sprintSpeed);

            var ev = new CanWeightlessMoveEvent(uid);
            RaiseLocalEvent(uid, ref ev, true);

            touching = ev.CanMove || xform.GridUid != null || MapGridQuery.HasComp(xform.GridUid);

            // If we're not on a grid, and not able to move in space, check if we're close enough to a
            // grid to push off it. Now safe in parallel since IsAroundCollider no longer shares a buffer.
            if (!touching && MobMoverQuery.TryComp(uid, out var weightlessMobMover))
                touching |= IsAroundCollider(_lookup, (uid, physicsComponent, weightlessMobMover, xform));

            // If we're touching then use the weightless values
            if (touching)
            {
                touching = true;
                if (wishDir != Vector2.Zero)
                    friction = moveSpeedComponent?.WeightlessFriction ?? _airDamping;
                else
                    friction = moveSpeedComponent?.WeightlessFrictionNoInput ?? _airDamping;
            }
            // Otherwise use the off-grid values.
            else
            {
                friction = moveSpeedComponent?.OffGridFriction ?? _offGridDamping;
            }

            accel = moveSpeedComponent?.WeightlessAcceleration ?? MovementSpeedModifierComponent.DefaultWeightlessAcceleration;
        }
        else
        {
            if (MapGridQuery.TryComp(xform.GridUid, out var gridComp)
                && _mapSystem.TryGetTileRef(xform.GridUid.Value, gridComp, xform.Coordinates, out var tile)
                && physicsComponent.BodyStatus == BodyStatus.OnGround)
                tileDef = (ContentTileDefinition)_tileDefinitionManager[tile.Tile.TypeId];

            var walkSpeed = moveSpeedComponent?.CurrentWalkSpeed ?? MovementSpeedModifierComponent.DefaultBaseWalkSpeed;
            var sprintSpeed = moveSpeedComponent?.CurrentSprintSpeed ?? MovementSpeedModifierComponent.DefaultBaseSprintSpeed;

            wishDir = AssertValidWish(mover, walkSpeed, sprintSpeed);

            if (wishDir != Vector2.Zero)
            {
                friction = moveSpeedComponent?.Friction ?? MovementSpeedModifierComponent.DefaultFriction;
                friction *= tileDef?.MobFriction ?? tileDef?.Friction ?? 1f;
            }
            else
            {
                friction = moveSpeedComponent?.FrictionNoInput ?? MovementSpeedModifierComponent.DefaultFrictionNoInput;
                friction *= tileDef?.Friction ?? 1f;
            }

            accel = moveSpeedComponent?.Acceleration ?? MovementSpeedModifierComponent.DefaultAcceleration;
            accel *= tileDef?.MobAcceleration ?? 1f;
        }
        // This way friction never exceeds acceleration when you're trying to move.
        // If you want to slow down an entity with "friction" you shouldn't be using this system.
        if (wishDir != Vector2.Zero)
            friction = Math.Min(friction, accel);
        friction = Math.Max(friction, _minDamping);
        var minimumFrictionSpeed = moveSpeedComponent?.MinimumFrictionSpeed ?? MovementSpeedModifierComponent.DefaultMinimumFrictionSpeed;

        var solve = new MoverSolver.MoverParams
        {
            Velocity = velocity,
            AngularVelocity = physicsComponent.AngularVelocity,
            WishDir = wishDir,
            FrictionInput = friction,
            FrictionNoInput = friction,
            Accel = accel,
            MinimumFrictionSpeed = minimumFrictionSpeed,
            MinDamping = _minDamping,
            Weightless = weightless,
            Touching = touching,
        };
        MoverSolver.TrySolve(in solve, frameTime, out velocity);

        SetWishDir((uid, mover), wishDir);

        /*
         * SNAKING!!! >-( 0 ================>
         * Snaking is a feature where you can move faster by strafing in a direction perpendicular to the
         * direction you intend to move while still holding the movement key for the direction you're trying to move.
         * Snaking only works if acceleration exceeds friction, and it's effectiveness scales as acceleration continues
         * to exceed friction.
         * Snaking works because friction is applied first in the direction of our current velocity, while acceleration
         * is applied after in our "Wish Direction" and is capped by the dot of our wish direction and current direction.
         * This means when you change direction, you're technically able to accelerate more than what the velocity cap
         * allows, but friction normally eats up the extra movement you gain.
         * By strafing as stated above you can increase your speed by about 1.4 (square root of 2).
         * This only works if friction is low enough so be sure that anytime you are letting a mob move in a low friction
         * environment you take into account the fact they can snake! Also be sure to lower acceleration as well to
         * prevent jerky movement!
         */
        //PhysicsSystem.SetLinearVelocity(uid, velocity, body: physicsComponent);

        // Ensures that players do not spiiiiiiin
        //PhysicsSystem.SetAngularVelocity(uid, 0, body: physicsComponent);

        // Handle footsteps at the end
        if (wishDir != Vector2.Zero)
        {
            if (!NoRotateQuery.HasComponent(uid))
            {
                // TODO apparently this results in a duplicate move event because "This should have its event run during
                // island solver"??. So maybe SetRotation needs an argument to avoid raising an event?
                var worldRot = _transform.GetWorldRotation(xform);

                rotation = xform.LocalRotation + wishDir.ToWorldAngle() - worldRot;
            }

            if (!weightless && MobMoverQuery.TryGetComponent(uid, out var mobMover) &&
                TryGetSound(weightless, uid, mover, mobMover, xform, out var sound, tileDef: tileDef))
            {
                var soundModifier = mover.Sprinting ? 3.5f : 1.5f;

                var audioParams = sound.Params
                    .WithVolume(sound.Params.Volume + soundModifier)
                    .WithVariation(sound.Params.Variation ?? mobMover.FootstepVariation);

                soundEvent = new SoundEvent(sound, uid, uid, audioParams);
            }
        }

        // stash what we resolved, scatter loop picks it up.
        resolvedBody = physicsComponent;
        resolvedXform = xform;
        return velocity;
    }

    public (Vector2 Strafe, float Rotation, float Brakes) GetPilotVelocityInput(PilotComponent component)
    {
        if (!Timing.InSimulation)
        {
            // Outside of simulation we'll be running client predicted movement per-frame.
            // So return a full-length vector as if it's a full tick.
            // Physics system will have the correct time step anyways.
            ResetSubtick(component);
            ApplyTick(component, 1f);
            return (component.CurTickStrafeMovement, component.CurTickRotationMovement, component.CurTickBraking);
        }

        float remainingFraction;

        if (Timing.CurTick > component.LastInputTick)
        {
            component.CurTickStrafeMovement = Vector2.Zero;
            component.CurTickRotationMovement = 0f;
            component.CurTickBraking = 0f;
            remainingFraction = 1;
        }
        else
        {
            remainingFraction = (ushort.MaxValue - component.LastInputSubTick) / (float) ushort.MaxValue;
        }

        ApplyTick(component, remainingFraction);

        // Logger.Info($"{curDir}{walk}{sprint}");
        return (component.CurTickStrafeMovement, component.CurTickRotationMovement, component.CurTickBraking);
    }

    private void ResetSubtick(PilotComponent component)
    {
        if (Timing.CurTick <= component.LastInputTick) return;

        component.CurTickStrafeMovement = Vector2.Zero;
        component.CurTickRotationMovement = 0f;
        component.CurTickBraking = 0f;
        component.LastInputTick = Timing.CurTick;
        component.LastInputSubTick = 0;
    }

    protected override void HandleShuttleInput(EntityUid uid, ShuttleButtons button, ushort subTick, bool state)
    {
        if (!TryComp<PilotComponent>(uid, out var pilot) || pilot.Console == null)
            return;

        ResetSubtick(pilot);

        if (subTick >= pilot.LastInputSubTick)
        {
            var fraction = (subTick - pilot.LastInputSubTick) / (float) ushort.MaxValue;

            ApplyTick(pilot, fraction);
            pilot.LastInputSubTick = subTick;
        }

        var buttons = pilot.HeldButtons;

        if (state)
        {
            buttons |= button;
        }
        else
        {
            buttons &= ~button;
        }

        pilot.HeldButtons = buttons;
    }

    private static void ApplyTick(PilotComponent component, float fraction)
    {
        var x = 0;
        var y = 0;
        var rot = 0;
        int brake;

        if ((component.HeldButtons & ShuttleButtons.StrafeLeft) != 0x0)
        {
            x -= 1;
        }

        if ((component.HeldButtons & ShuttleButtons.StrafeRight) != 0x0)
        {
            x += 1;
        }

        component.CurTickStrafeMovement.X += x * fraction;

        if ((component.HeldButtons & ShuttleButtons.StrafeUp) != 0x0)
        {
            y += 1;
        }

        if ((component.HeldButtons & ShuttleButtons.StrafeDown) != 0x0)
        {
            y -= 1;
        }

        component.CurTickStrafeMovement.Y += y * fraction;

        if ((component.HeldButtons & ShuttleButtons.RotateLeft) != 0x0)
        {
            rot -= 1;
        }

        if ((component.HeldButtons & ShuttleButtons.RotateRight) != 0x0)
        {
            rot += 1;
        }

        component.CurTickRotationMovement += rot * fraction;

        if ((component.HeldButtons & ShuttleButtons.Brake) != 0x0)
        {
            brake = 1;
        }
        else
        {
            brake = 0;
        }

        component.CurTickBraking += brake * fraction;
    }

    /// <summary>
    /// Helper function to extrapolate max velocity for a given Vector2 (really, its angle) and shuttle.
    /// </summary>
    private Vector2 ObtainMaxVel(Vector2 vel, ShuttleComponent shuttle)
    {
        if (vel.Length() == 0f)
            return Vector2.Zero;

        // this math could PROBABLY be simplified for performance
        // probably
        //             __________________________________
        //            / /    __   __ \2   /    __   __ \2
        // O = I : _ /  |I * | 1/H | |  + |I * |  0  | |
        //          V   \    |_ 0 _| /    \    |_1/V_| /

        var horizIndex = vel.X > 0 ? 1 : 3; // east else west
        var vertIndex = vel.Y > 0 ? 2 : 0; // north else south
        var horizComp = vel.X != 0 ? MathF.Pow(Vector2.Dot(vel, new (shuttle.LinearThrust[horizIndex] / shuttle.LinearThrust[horizIndex], 0f)), 2) : 0;
        var vertComp = vel.Y != 0 ? MathF.Pow(Vector2.Dot(vel, new (0f, shuttle.LinearThrust[vertIndex] / shuttle.LinearThrust[vertIndex])), 2) : 0;

        return shuttle.BaseMaxLinearVelocity * vel * MathF.ReciprocalSqrtEstimate(horizComp + vertComp);
    }

    private void HandleShuttleMovement(float frameTime)
    {
        var newPilots = new Dictionary<EntityUid, (ShuttleComponent Shuttle, List<(EntityUid PilotUid, PilotComponent Pilot, TransformComponent ConsoleXform)>)>();

        // We just mark off their movement and the shuttle itself does its own movement
        var activePilotQuery = EntityQueryEnumerator<PilotComponent>();
        var shuttleQuery = GetEntityQuery<ShuttleComponent>();
        while (activePilotQuery.MoveNext(out var uid, out var pilot))
        {
            var consoleEnt = pilot.Console;

            // TODO: This is terrible. Just make a new mover and also make it remote piloting + device networks
            if (TryComp<DroneConsoleComponent>(consoleEnt, out var cargoConsole))
            {
                consoleEnt = cargoConsole.Entity;
            }

            if (!TryComp(consoleEnt, out TransformComponent? xform)) continue;

            var gridId = xform.GridUid;
            // This tries to see if the grid is a shuttle and if the console should work.
            if (!TryComp<MapGridComponent>(gridId, out var _) ||
                !shuttleQuery.TryGetComponent(gridId, out var shuttleComponent) ||
                !shuttleComponent.Enabled)
                continue;

            if (!newPilots.TryGetValue(gridId!.Value, out var pilots))
            {
                pilots = (shuttleComponent, new List<(EntityUid, PilotComponent, TransformComponent)>());
                newPilots[gridId.Value] = pilots;
            }

            pilots.Item2.Add((uid, pilot, xform));
        }

        // Reset inputs for non-piloted shuttles.
        foreach (var (shuttleUid, (shuttle, _)) in _shuttlePilots)
        {
            if (newPilots.ContainsKey(shuttleUid) || CanPilot(shuttleUid))
                continue;

            _thruster.DisableLinearThrusters(shuttle);
        }

        _shuttlePilots = newPilots;

        // Collate all of the linear / angular velocites for a shuttle
        // then do the movement input once for it.
        var xformQuery = GetEntityQuery<TransformComponent>();
        foreach (var (shuttleUid, (shuttle, pilots)) in _shuttlePilots)
        {
            if (Paused(shuttleUid) || CanPilot(shuttleUid) || !TryComp<PhysicsComponent>(shuttleUid, out var body))
                continue;

            var shuttleNorthAngle = _xformSystem.GetWorldRotation(shuttleUid, xformQuery);

            // Collate movement linear and angular inputs together
            var linearInput = Vector2.Zero;
            var brakeInput = 0f;
            var angularInput = 0f;
            var linearCount = 0;
            var brakeCount = 0;
            var angularCount = 0;

            foreach (var (pilotUid, pilot, consoleXform) in pilots)
            {
                var (strafe, rotation, brakes) = GetPilotVelocityInput(pilot);

                if (brakes > 0f)
                {
                    brakeInput += brakes;
                    brakeCount++;
                }

                if (strafe.Length() > 0f)
                {
                    var offsetRotation = consoleXform.LocalRotation;
                    linearInput += offsetRotation.RotateVec(strafe);
                    linearCount++;
                }

                if (rotation != 0f)
                {
                    angularInput += rotation;
                    angularCount++;
                }
            }

            // Don't slow down the shuttle if there's someone just looking at the console
            linearInput /= Math.Max(1, linearCount);
            angularInput /= Math.Max(1, angularCount);
            brakeInput /= Math.Max(1, brakeCount);

            // Handle shuttle movement
            if (brakeInput > 0f)
            {
                if (body.LinearVelocity.Length() > 0f)
                {
                    // Minimum brake velocity for a direction to show its thrust appearance.
                    const float AppearanceThreshold = 0.1f;

                    // Get velocity relative to the shuttle so we know which thrusters to fire
                    var shuttleVelocity = (-shuttleNorthAngle).RotateVec(body.LinearVelocity);
                    var force = Vector2.Zero;

                    if (shuttleVelocity.X < 0f)
                    {
                        _thruster.DisableLinearThrustDirection(shuttle, DirectionFlag.West);

                        if (shuttleVelocity.X < -AppearanceThreshold)
                            _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.East);

                        var index = (int) Math.Log2((int) DirectionFlag.East);
                        force.X += shuttle.LinearThrust[index];
                    }
                    else if (shuttleVelocity.X > 0f)
                    {
                        _thruster.DisableLinearThrustDirection(shuttle, DirectionFlag.East);

                        if (shuttleVelocity.X > AppearanceThreshold)
                            _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.West);

                        var index = (int) Math.Log2((int) DirectionFlag.West);
                        force.X -= shuttle.LinearThrust[index];
                    }

                    if (shuttleVelocity.Y < 0f)
                    {
                        _thruster.DisableLinearThrustDirection(shuttle, DirectionFlag.South);

                        if (shuttleVelocity.Y < -AppearanceThreshold)
                            _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.North);

                        var index = (int) Math.Log2((int) DirectionFlag.North);
                        force.Y += shuttle.LinearThrust[index];
                    }
                    else if (shuttleVelocity.Y > 0f)
                    {
                        _thruster.DisableLinearThrustDirection(shuttle, DirectionFlag.North);

                        if (shuttleVelocity.Y > AppearanceThreshold)
                            _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.South);

                        var index = (int) Math.Log2((int) DirectionFlag.South);
                        force.Y -= shuttle.LinearThrust[index];
                    }

                    var impulse = force * brakeInput * ShuttleComponent.BrakeCoefficient;
                    impulse = shuttleNorthAngle.RotateVec(impulse);
                    var forceMul = frameTime * body.InvMass;
                    var maxVelocity = (-body.LinearVelocity).Length() / forceMul;

                    // Don't overshoot
                    if (impulse.Length() > maxVelocity)
                        impulse = impulse.Normalized() * maxVelocity;

                    PhysicsSystem.ApplyForce(shuttleUid, impulse, body: body);
                }
                else
                {
                    _thruster.DisableLinearThrusters(shuttle);
                }

                if (body.AngularVelocity != 0f)
                {
                    var torque = shuttle.AngularThrust * brakeInput * (body.AngularVelocity > 0f ? -1f : 1f) * ShuttleComponent.BrakeCoefficient;
                    var torqueMul = body.InvI * frameTime;

                    if (body.AngularVelocity > 0f)
                    {
                        torque = MathF.Max(-body.AngularVelocity / torqueMul, torque);
                    }
                    else
                    {
                        torque = MathF.Min(-body.AngularVelocity / torqueMul, torque);
                    }

                    if (!torque.Equals(0f))
                    {
                        PhysicsSystem.ApplyTorque(shuttleUid, torque, body: body);
                        _thruster.SetAngularThrust(shuttle, true);
                    }
                }
                else
                {
                    _thruster.SetAngularThrust(shuttle, false);
                }
            }

            if (linearInput.Length().Equals(0f))
            {
                PhysicsSystem.SetSleepingAllowed(shuttleUid, body, true);

                if (brakeInput.Equals(0f))
                    _thruster.DisableLinearThrusters(shuttle);
            }
            else
            {
                PhysicsSystem.SetSleepingAllowed(shuttleUid, body, false);
                var angle = linearInput.ToWorldAngle();
                var linearDir = angle.GetDir();
                var dockFlag = linearDir.AsFlag();
                var totalForce = Vector2.Zero;

                // Won't just do cardinal directions.
                foreach (DirectionFlag dir in Enum.GetValues(typeof(DirectionFlag)))
                {
                    // Brain no worky but I just want cardinals
                    switch (dir)
                    {
                        case DirectionFlag.South:
                        case DirectionFlag.East:
                        case DirectionFlag.North:
                        case DirectionFlag.West:
                            break;
                        default:
                            continue;
                    }

                    if ((dir & dockFlag) == 0x0)
                    {
                        _thruster.DisableLinearThrustDirection(shuttle, dir);
                        continue;
                    }

                    var force = Vector2.Zero;
                    var index = (int) Math.Log2((int) dir);
                    var thrust = shuttle.LinearThrust[index];

                    switch (dir)
                    {
                        case DirectionFlag.North:
                            force.Y += thrust;
                            break;
                        case DirectionFlag.South:
                            force.Y -= thrust;
                            break;
                        case DirectionFlag.East:
                            force.X += thrust;
                            break;
                        case DirectionFlag.West:
                            force.X -= thrust;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Attempted to apply thrust to shuttle {shuttleUid} along invalid dir {dir}.");
                    }

                    _thruster.EnableLinearThrustDirection(shuttle, dir);
                    var impulse = force * linearInput.Length();
                    totalForce += impulse;
                }

                var forceMul = frameTime * body.InvMass;

                var localVel = (-shuttleNorthAngle).RotateVec(body.LinearVelocity);
                var maxVelocity = ObtainMaxVel(localVel, shuttle); // max for current travel dir
                var maxWishVelocity = ObtainMaxVel(totalForce, shuttle);
                var properAccel = (maxWishVelocity - localVel) / forceMul;

                var finalForce = Vector2Dot(totalForce, properAccel.Normalized()) * properAccel.Normalized();

                if (localVel.Length() >= maxVelocity.Length() && Vector2.Dot(totalForce, localVel) > 0f)
                    finalForce = Vector2.Zero; // burn would be faster if used as such

                if (finalForce.Length() > properAccel.Length())
                    finalForce = properAccel; // don't overshoot

                //Log.Info($"shuttle: maxVelocity {maxVelocity} totalForce {totalForce} finalForce {finalForce} forceMul {forceMul} properAccel {properAccel}");

                finalForce = shuttleNorthAngle.RotateVec(finalForce);

                if (finalForce.Length() > 0f)
                    PhysicsSystem.ApplyForce(shuttleUid, finalForce, body: body);
            }

            if (MathHelper.CloseTo(angularInput, 0f))
            {
                PhysicsSystem.SetSleepingAllowed(shuttleUid, body, true);

                if (brakeInput <= 0f)
                    _thruster.SetAngularThrust(shuttle, false);
            }
            else
            {
                PhysicsSystem.SetSleepingAllowed(shuttleUid, body, false);
                var torque = shuttle.AngularThrust * -angularInput;

                // Need to cap the velocity if 1 tick of input brings us over cap so we don't continuously
                // edge onto the cap over and over.
                var torqueMul = body.InvI * frameTime;

                torque = Math.Clamp(torque,
                    (-ShuttleComponent.MaxAngularVelocity - body.AngularVelocity) / torqueMul,
                    (ShuttleComponent.MaxAngularVelocity - body.AngularVelocity) / torqueMul);

                if (!torque.Equals(0f))
                {
                    PhysicsSystem.ApplyTorque(shuttleUid, torque, body: body);
                    _thruster.SetAngularThrust(shuttle, true);
                }
            }
        }
    }

    // .NET 8 seem to miscompile usage of Vector2.Dot above. This manual outline fixes it pending an upstream fix.
    // See PR #24008
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float Vector2Dot(Vector2 value1, Vector2 value2)
        => Vector2.Dot(value1, value2);

    private bool CanPilot(EntityUid shuttleUid)
    => (TryComp<FTLComponent>(shuttleUid, out var ftl)
        && (ftl.State & (FTLState.Starting | FTLState.Travelling | FTLState.Arriving)) != 0x0)
            || HasComp<PreventPilotComponent>(shuttleUid);

}
