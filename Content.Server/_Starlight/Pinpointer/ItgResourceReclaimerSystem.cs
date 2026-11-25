using Content.Shared._Starlight.Pinpointer;
using Content.Shared._Starlight.Implants.Components;
using Content.Shared._Starlight.Pinpointer.Components;
using Content.Shared.Implants.Components;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Pinpointer;
using Robust.Shared.Map;

namespace Content.Server._Starlight.Pinpointer;

public sealed partial class ItgResourceReclaimerSystem : SharedItgResourceReclaimerSystem
{
    [Dependency] private SharedPinpointerSystem _pinpointer = null!;
    [Dependency] private SharedTransformSystem _transform = null!;
    [Dependency] private SharedAppearanceSystem _appearance = null!;

    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    private readonly Dictionary<MapId, List<CachedTarget>> _targetsByMap = new();
    private readonly List<EntityUid> _cachedReclaimers = [];
    private const float CacheRefreshInterval = 1f;
    private const float TargetUpdateInterval = 0.25f;
    private float _cacheAccumulator = CacheRefreshInterval;
    private float _targetUpdateAccumulator;

    private readonly record struct CachedTarget(EntityUid Uid, MobState State);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _cacheAccumulator += frameTime;
        if (_cacheAccumulator >= CacheRefreshInterval)
        {
            _cacheAccumulator %= CacheRefreshInterval;
            RefreshTargetCache();
            RefreshReclaimerCache();

            _targetUpdateAccumulator = 0f;
            UpdateCachedReclaimers();
            return;
        }

        _targetUpdateAccumulator += frameTime;
        if (_targetUpdateAccumulator < TargetUpdateInterval)
            return;

        _targetUpdateAccumulator %= TargetUpdateInterval;
        UpdateCachedReclaimers();
    }

    protected override void OnModeChanged(EntityUid uid, ItgResourceReclaimerComponent reclaimer)
    {
        if (!TryComp<PinpointerComponent>(uid, out var pinpointer))
            return;

        ResetPinpointer(uid);

        if (!pinpointer.IsActive || !_xformQuery.TryGetComponent(uid, out var xform))
            return;

        UpdateTarget(uid, reclaimer, xform);
    }

    private void UpdateTarget(EntityUid uid, ItgResourceReclaimerComponent reclaimer, TransformComponent xform)
    {
        var target = FindNearestTarget(xform, reclaimer.Mode);
        if (target == null)
        {
            ResetPinpointer(uid);
            return;
        }

        _pinpointer.SetTarget(uid, target);
    }

    private void RefreshTargetCache()
    {
        foreach (var targets in _targetsByMap.Values)
            targets.Clear();

        var query = EntityQueryEnumerator<ItgTrackingImplantComponent, SubdermalImplantComponent, SuitSensorComponent>();
        while (query.MoveNext(out _, out var implant, out var sensor))
        {
            if (implant.ImplantedEntity is not { } wearer ||
                sensor.User != wearer ||
                sensor.Mode == SuitSensorMode.SensorOff ||
                !TryComp<MobStateComponent>(wearer, out var mobState) ||
                !_xformQuery.TryGetComponent(wearer, out var wearerXform) ||
                wearerXform.MapID == MapId.Nullspace)
                continue;

            var mapId = wearerXform.MapID;
            if (!_targetsByMap.TryGetValue(mapId, out var targets))
            {
                targets = [];
                _targetsByMap.Add(mapId, targets);
            }

            targets.Add(new CachedTarget(wearer, mobState.CurrentState));
        }
    }

    private void RefreshReclaimerCache()
    {
        _cachedReclaimers.Clear();

        var query = EntityQueryEnumerator<ItgResourceReclaimerComponent, PinpointerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out _))
            _cachedReclaimers.Add(uid);
    }

    private void UpdateCachedReclaimers()
    {
        foreach (var uid in _cachedReclaimers)
        {
            if (!TryComp<ItgResourceReclaimerComponent>(uid, out var reclaimer) ||
                !TryComp<PinpointerComponent>(uid, out var pinpointer) ||
                !pinpointer.IsActive ||
                !_xformQuery.TryGetComponent(uid, out var xform))
                continue;

            UpdateTarget(uid, reclaimer, xform);
        }
    }

    private void ResetPinpointer(EntityUid uid)
    {
        _pinpointer.SetTarget(uid, null);
        _pinpointer.SetDistance(uid, Distance.Unknown);

        if (TryComp<AppearanceComponent>(uid, out var appearance))
            _appearance.SetData(uid, PinpointerVisuals.TargetDistance, Distance.Unknown, appearance);
    }

    private EntityUid? FindNearestTarget(TransformComponent reclaimerXform, ItgResourceReclaimerMode mode)
    {
        var mapId = reclaimerXform.MapID;
        if (mapId == MapId.Nullspace || !_targetsByMap.TryGetValue(mapId, out var targets))
            return null;

        var reclaimerPosition = _transform.GetWorldPosition(reclaimerXform);
        var bestDistance = float.MaxValue;
        EntityUid? bestTarget = null;

        foreach (var target in targets)
        {
            if (!Exists(target.Uid) ||
                IsCarriedBy(reclaimerXform, target.Uid) ||
                !MatchesMode(target.State, mode) ||
                !_xformQuery.TryGetComponent(target.Uid, out var targetXform) ||
                targetXform.MapID != mapId)
                continue;

            var targetPosition = _transform.GetWorldPosition(targetXform);
            var distance = (targetPosition - reclaimerPosition).LengthSquared();
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestTarget = target.Uid;
        }

        return bestTarget;
    }

    private bool IsCarriedBy(TransformComponent itemXform, EntityUid wearer)
    {
        var parent = itemXform.ParentUid;
        while (parent.IsValid())
        {
            if (parent == wearer)
                return true;

            if (!_xformQuery.TryGetComponent(parent, out var parentXform))
                break;

            parent = parentXform.ParentUid;
        }

        return false;
    }

    private static bool MatchesMode(MobState state, ItgResourceReclaimerMode mode) =>
        mode switch
        {
            ItgResourceReclaimerMode.Alive => state == MobState.Alive,
            ItgResourceReclaimerMode.Crit => state is MobState.SoftCritical or MobState.Critical,
            ItgResourceReclaimerMode.Dead => state == MobState.Dead,
            _ => false
        };
}
