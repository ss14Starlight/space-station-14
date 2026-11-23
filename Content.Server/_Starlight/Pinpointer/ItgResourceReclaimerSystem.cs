using System.Numerics;
using Content.Shared._Starlight.Implants.Components;
using Content.Shared._Starlight.Pinpointer.Components;
using Content.Shared.Examine;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Verbs;
using Robust.Shared.Map;

namespace Content.Server._Starlight.Pinpointer;

public sealed partial class ItgResourceReclaimerSystem : EntitySystem
{
    [Dependency] private SharedInteractionSystem _interaction = null!;
    [Dependency] private SharedPinpointerSystem _pinpointer = null!;
    [Dependency] private SharedTransformSystem _transform = null!;
    [Dependency] private SharedAppearanceSystem _appearance = null!;

    private EntityQuery<TransformComponent> _xformQuery;

    private static readonly VerbCategory _modeCategory = new("itg-resource-reclaimer-select-mode", null);

    private readonly Dictionary<MapId, List<CachedTarget>> _targetsByMap = new();
    private readonly List<EntityUid> _cachedReclaimers = [];
    private const float CacheRefreshInterval = 1f;
    private const float TargetUpdateInterval = 0.25f;
    private float _cacheAccumulator = CacheRefreshInterval;
    private float _targetUpdateAccumulator;

    private readonly record struct CachedTarget(EntityUid Uid, Vector2 Position, MobState State);

    public override void Initialize()
    {
        base.Initialize();

        _xformQuery = GetEntityQuery<TransformComponent>();
    }

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

    [SubscribeLocalEvent]
    private void OnGetVerbs(Entity<ItgResourceReclaimerComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || args.Hands == null || !_interaction.InRangeUnobstructed(args.User, args.Target))
            return;

        args.Verbs.Add(CreateModeVerb(ent, ItgResourceReclaimerMode.Alive));
        args.Verbs.Add(CreateModeVerb(ent, ItgResourceReclaimerMode.Crit));
        args.Verbs.Add(CreateModeVerb(ent, ItgResourceReclaimerMode.Dead));
    }

    [SubscribeLocalEvent]
    private void OnGetAlternativeVerb(Entity<ItgResourceReclaimerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.Using.HasValue || args.Using.Value != ent.Owner)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("itg-resource-reclaimer-verb-switch-mode"),
            Act = () => SetMode(ent.Owner, GetNextMode(ent.Comp.Mode))
        });
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<ItgResourceReclaimerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("itg-resource-reclaimer-examine-mode",
            ("mode", Loc.GetString(GetModeLocalization(ent.Comp.Mode)))));
    }

    private Verb CreateModeVerb(Entity<ItgResourceReclaimerComponent> ent, ItgResourceReclaimerMode mode) =>
        new()
        {
            Text = Loc.GetString(GetModeLocalization(mode)),
            Disabled = ent.Comp.Mode == mode,
            Priority = -(int) mode,
            Category = _modeCategory,
            Act = () => SetMode(ent.Owner, mode)
        };

    private void SetMode(EntityUid uid, ItgResourceReclaimerMode mode)
    {
        if (!TryComp<ItgResourceReclaimerComponent>(uid, out var reclaimer) || reclaimer.Mode == mode)
            return;

        reclaimer.Mode = mode;
        Dirty(uid, reclaimer);

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

            targets.Add(new CachedTarget(wearer, _transform.GetWorldPosition(wearerXform), mobState.CurrentState));
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
                !MatchesMode(target.State, mode))
                continue;

            var distance = (target.Position - reclaimerPosition).LengthSquared();
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

    private static ItgResourceReclaimerMode GetNextMode(ItgResourceReclaimerMode mode) =>
        mode switch
        {
            ItgResourceReclaimerMode.Alive => ItgResourceReclaimerMode.Crit,
            ItgResourceReclaimerMode.Crit => ItgResourceReclaimerMode.Dead,
            _ => ItgResourceReclaimerMode.Alive
        };

    private static string GetModeLocalization(ItgResourceReclaimerMode mode) =>
        mode switch
        {
            ItgResourceReclaimerMode.Alive => "itg-resource-reclaimer-mode-alive",
            ItgResourceReclaimerMode.Crit => "itg-resource-reclaimer-mode-crit",
            ItgResourceReclaimerMode.Dead => "itg-resource-reclaimer-mode-dead",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
}
