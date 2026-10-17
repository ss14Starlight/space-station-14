using System.Numerics;
using Content.Server._Starlight.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._Starlight.NPC.Systems;

/// <summary>
/// Puts HTN NPCs to sleep while no player is within <see cref="StarlightCCVars.NPCProximitySleepRange"/> and wakes them
/// once one comes close. Mobs on expeditions, asteroids and empty parts of the map otherwise plan, pathfind and steer
/// for the whole round, which makes NPC, pathfinding, steering and physics cost scale with the size of the world
/// instead of with where players actually are.
/// </summary>
public sealed partial class NPCProximitySleepSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private EntityQuery<ActiveNPCComponent> _activeQuery;
    private EntityQuery<NPCProximityDormantComponent> _dormantQuery;
    private EntityQuery<NPCProximitySleepExemptComponent> _exemptQuery;
    private EntityQuery<NPCPointDefenseComponent> _pointDefenseQuery;
    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<MindContainerComponent> _mindQuery;

    private readonly Dictionary<MapId, List<Vector2>> _playerPositions = new();
    private readonly List<List<Vector2>> _positionListPool = new();

    private bool _enabled;
    private float _rangeSquared;
    private float _interval;
    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();

        _activeQuery = GetEntityQuery<ActiveNPCComponent>();
        _dormantQuery = GetEntityQuery<NPCProximityDormantComponent>();
        _exemptQuery = GetEntityQuery<NPCProximitySleepExemptComponent>();
        _pointDefenseQuery = GetEntityQuery<NPCPointDefenseComponent>();
        _actorQuery = GetEntityQuery<ActorComponent>();
        _mindQuery = GetEntityQuery<MindContainerComponent>();

        Subs.CVar(_cfg, StarlightCCVars.NPCProximitySleep, OnEnabledChanged, true);
        Subs.CVar(_cfg, StarlightCCVars.NPCProximitySleepRange, value => _rangeSquared = value * value, true);
        Subs.CVar(_cfg, StarlightCCVars.NPCProximitySleepInterval, value => _interval = MathF.Max(value, 0.1f), true);
    }

    private void OnEnabledChanged(bool value)
    {
        _enabled = value;

        if (!value)
            WakeAllDormant();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled)
            return;

        _accumulator += frameTime;
        if (_accumulator < _interval)
            return;

        _accumulator = 0f;

        CollectPlayerPositions();

        var query = EntityQueryEnumerator<HTNComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var htn, out var xform))
        {
            var dormant = _dormantQuery.HasComp(uid);

            if (_exemptQuery.HasComp(uid) || _pointDefenseQuery.HasComp(uid))
            {
                if (dormant)
                    Wake(uid, htn);

                continue;
            }

            var awake = _activeQuery.HasComp(uid);

            // Asleep for some other reason (dead, controlled by a player, ...), not ours to touch.
            if (!awake && !dormant)
                continue;

            var near = IsNearPlayer(_transform.GetMapCoordinates(uid, xform));

            if (near)
            {
                if (dormant)
                    Wake(uid, htn);

                continue;
            }

            // Something else woke it up while it was dormant (revived, mind left, ...), or it just wandered off.
            if (awake && !_actorQuery.HasComp(uid))
            {
                _npc.SleepNPC(uid, htn);
                EnsureComp<NPCProximityDormantComponent>(uid);
            }
        }
    }

    private void Wake(EntityUid uid, HTNComponent htn)
    {
        RemComp<NPCProximityDormantComponent>(uid);

        // Mirror the conditions NPCSystem uses, so a mob that died or got a mind while dormant stays asleep.
        if (_actorQuery.HasComp(uid) || _mobState.IsIncapacitated(uid))
            return;

        if (_mindQuery.TryComp(uid, out var mind) && mind.HasMind)
            return;

        _npc.WakeNPC(uid, htn);
    }

    private void WakeAllDormant()
    {
        var query = EntityQueryEnumerator<NPCProximityDormantComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out _, out var htn))
        {
            Wake(uid, htn);
        }
    }

    private void CollectPlayerPositions()
    {
        foreach (var list in _playerPositions.Values)
        {
            list.Clear();
            _positionListPool.Add(list);
        }

        _playerPositions.Clear();

        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            var coords = _transform.GetMapCoordinates(uid, xform);
            if (coords.MapId == MapId.Nullspace)
                continue;

            if (!_playerPositions.TryGetValue(coords.MapId, out var list))
            {
                if (_positionListPool.Count > 0)
                {
                    list = _positionListPool[^1];
                    _positionListPool.RemoveAt(_positionListPool.Count - 1);
                }
                else
                {
                    list = new List<Vector2>();
                }

                _playerPositions[coords.MapId] = list;
            }

            list.Add(coords.Position);
        }
    }

    private bool IsNearPlayer(MapCoordinates coords)
    {
        if (!_playerPositions.TryGetValue(coords.MapId, out var positions))
            return false;

        foreach (var position in positions)
        {
            if (Vector2.DistanceSquared(position, coords.Position) <= _rangeSquared)
                return true;
        }

        return false;
    }
}
