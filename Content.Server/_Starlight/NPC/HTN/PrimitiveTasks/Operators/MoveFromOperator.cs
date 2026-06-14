using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._Starlight.Actions.EntitySystems;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Shared._Starlight.Actions.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server._Starlight.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Moves an NPC away from the specified target key.
/// Hands the actual steering off to NPCSystem.Steering.
/// </summary>
public sealed partial class MoveFromOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private IEntityManager _entManager = default!;
    private JumpSystem _jumpSystem = default!;
    private NPCSteeringSystem _steering = default!;
    private PathfindingSystem _pathfind = default!;
    private SharedTransformSystem _transform = default!;

    private static readonly float[] _fleeAngles =
        { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f, 180f };

    // Blackboard key used to pass the computed flee position from Plan→Startup.
    private const string FleePosKey = "_FleeTargetCoordinates";

    private const float DefaultSafeDistance = 5f;
    private const float FleeDistanceMultiplier = 1.5f;

    /// <summary>
    /// When to shut the task down.
    /// </summary>
    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    /// <summary>
    /// Should we assume the MovementTarget is reachable during planning or should we pathfind to it?
    /// </summary>
    [DataField]
    public bool PathfindInPlanning = true;

    /// <summary>
    /// When we're finished moving to the target should we remove its key?
    /// </summary>
    [DataField]
    public bool RemoveKeyOnFinish = true;

    /// <summary>
    /// Target Coordinates to move to. This gets removed after execution.
    /// </summary>
    [DataField]
    public string TargetKey = "TargetCoordinates";

    /// <summary>
    /// Where the pathfinding result will be stored (if applicable). This gets removed after execution.
    /// </summary>
    [DataField]
    public string PathfindKey = NPCBlackboard.PathfindKey;

    /// <summary>
    /// How close we need to get before considering movement finished.
    /// </summary>
    [DataField]
    public string RangeKey = "MovementRange";

    /// <summary>
    /// Do we only need to move into line of sight.
    /// </summary>
    [DataField]
    public bool StopOnLineOfSight;

    [DataField]
    public bool UseJump = false;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pathfind = sysManager.GetEntitySystem<PathfindingSystem>();
        _steering = sysManager.GetEntitySystem<NPCSteeringSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
        _jumpSystem = sysManager.GetEntitySystem<JumpSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityCoordinates>(TargetKey, out var threatCoords, _entManager))
            return (false, null);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<TransformComponent>(owner, out var xform) ||
            !_entManager.HasComponent<PhysicsComponent>(owner))
            return (false, null);

        var ownerPos = xform.Coordinates;
        var safeDistance = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);
        if (safeDistance == 0f)
            safeDistance = DefaultSafeDistance;

        // Already far enough — nothing to do.
        if (ownerPos.TryDistance(_entManager, threatCoords, out var dist) && dist >= safeDistance)
            return (true, null);

        if (!PathfindInPlanning)
        {
            // We still need *some* flee position for Startup; compute the raw one.
            var rawFlee = ComputeFleePos(ownerPos, threatCoords, safeDistance, 0f);
            return (true, new Dictionary<string, object>
            {
                { FleePosKey, rawFlee },
                { NPCBlackboard.OwnerCoordinates, rawFlee },
            });
        }

        var flags = _pathfind.GetFlags(blackboard);

        foreach (var angleDeg in _fleeAngles)
        {
            if (cancelToken.IsCancellationRequested)
                break;

            var fleePos = ComputeFleePos(ownerPos, threatCoords, safeDistance, angleDeg);

            var path = await _pathfind.GetPath(
                owner, ownerPos, fleePos, safeDistance, cancelToken, flags);

            if (path.Result != PathResult.Path)
                continue;

            // Found a reachable position — store it so Startup doesn't recompute.
            return (true, new Dictionary<string, object>
            {
                { FleePosKey,  fleePos },
                { PathfindKey, path    },
                { NPCBlackboard.OwnerCoordinates, fleePos },
            });
        }

        // Useful in very tight corridors where every directional probe fails.
        var randomPath = await _pathfind.GetRandomPath(
            owner, safeDistance, cancelToken, flags: flags);

        if (randomPath.Result == PathResult.Path)
        {
            var randomTarget = randomPath.Path[^1].Coordinates;
            return (true, new Dictionary<string, object>
            {
                { FleePosKey,  randomTarget },
                { PathfindKey, randomPath   },
                { NPCBlackboard.OwnerCoordinates, randomTarget },
            });
        }

        // Truly stuck — report failure so the HTN can try another branch.
        return (false, null);
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        // Remove the planning hint (OwnerCoordinates is only used during planning).
        blackboard.Remove<EntityCoordinates>(NPCBlackboard.OwnerCoordinates);

        var uid = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        // Prefer the flee position that was already computed (and pathfound) in Plan().
        // Only recompute if it somehow isn't there (e.g. PathfindInPlanning = false).
        if (!blackboard.TryGetValue<EntityCoordinates>(FleePosKey, out var fleePos, _entManager))
        {
            var threatCoords = blackboard.GetValue<EntityCoordinates>(TargetKey);
            var ownerPos = _transform.GetMoverCoordinates(uid);
            var safeDistance = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);
            if (safeDistance == 0f)
                safeDistance = DefaultSafeDistance;
            fleePos = ComputeFleePos(ownerPos, threatCoords, safeDistance, 0f);
        }

        // Jump ability (if available and enabled).
        if (UseJump && _entManager.TryGetComponent<JumpComponent>(uid, out var jumpComp))
            _jumpSystem.TryJump(new Entity<JumpComponent?>(uid, jumpComp), fleePos, decreaseCharges: true);

        var comp = _steering.Register(uid, fleePos);
        comp.ArriveOnLineOfSight = StopOnLineOfSight;

        if (blackboard.TryGetValue<float>(RangeKey, out var range, _entManager))
            comp.Range = range;

        // Re-use the pre-computed path if available.
        if (blackboard.TryGetValue<PathResultEvent>(PathfindKey, out var result, _entManager))
        {
            var ownerMapCoords = _transform.ToMapCoordinates(_transform.GetMoverCoordinates(uid));
            var fleeMapCoords = _transform.ToMapCoordinates(fleePos);
            var path = result.Path;
            _steering.PrunePath(uid, ownerMapCoords, fleeMapCoords.Position - ownerMapCoords.Position, path);
            comp.CurrentPath = new Queue<PathPoly>(path);
        }
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!_entManager.TryGetComponent<NPCSteeringComponent>(owner, out var steering))
            return HTNOperatorStatus.Failed;

        // Check whether we're already far enough from the threat.
        if (blackboard.TryGetValue<EntityCoordinates>(TargetKey, out var threatCoords, _entManager))
        {
            var xform = _entManager.GetComponent<TransformComponent>(owner);
            if (xform.Coordinates.TryDistance(_entManager, threatCoords, out var dist))
            {
                var safeDistance = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);
                if (dist >= safeDistance)
                    return HTNOperatorStatus.Finished;
            }
        }

        return steering.Status switch
        {
            SteeringStatus.InRange => HTNOperatorStatus.Finished,
            SteeringStatus.NoPath => HTNOperatorStatus.Failed,
            SteeringStatus.Moving => HTNOperatorStatus.Continuing,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        blackboard.Remove<PathResultEvent>(PathfindKey);
        blackboard.Remove<EntityCoordinates>(FleePosKey);

        if (RemoveKeyOnFinish)
            blackboard.Remove<EntityCoordinates>(TargetKey);

        _steering.Unregister(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner));
    }

    #region Helpers

    /// <summary>
    /// Returns a world-space flee coordinate at <paramref name="safeDistance"/> * 1.5
    /// from <paramref name="threatCoords"/>, rotated by <paramref name="angleDeg"/> degrees
    /// relative to the direct-away direction.
    /// </summary>
    private static EntityCoordinates ComputeFleePos(
        EntityCoordinates ownerPos,
        EntityCoordinates threatCoords,
        float safeDistance,
        float angleDeg)
    {
        var away = ownerPos.Position - threatCoords.Position;

        // If owner is exactly on the threat (shouldn't happen, but guard anyway).
        if (away == Vector2.Zero)
            away = Vector2.UnitX;
        else
            away = away.Normalized();

        if (angleDeg != 0f)
        {
            var rad = MathHelper.DegreesToRadians(angleDeg);
            var cos = MathF.Cos(rad);
            var sin = MathF.Sin(rad);
            away = new Vector2(
                (away.X * cos) - (away.Y * sin),
                (away.X * sin) + (away.Y * cos));
        }

        return threatCoords.Offset(away * safeDistance * FleeDistanceMultiplier);
    }

    #endregion
}
