using System.Numerics;
using Content.Server.Radiation.Components;
using Content.Shared.Radiation.Components;
using Content.Shared.Radiation.Events;
using Content.Shared.Stacks;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Threading;
using Robust.Shared.Timing;

namespace Content.Server.Radiation.Systems;

public sealed partial class RadiationSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private IParallelManager _parallel = default!;
    [Dependency] private IGameTiming _gameTiming = default!; // Funkystation: Funky atmos - /tg/ gases

    [Dependency] private EntityQuery<RadiationReceiverComponent> _receiverQuery = default!;
    private EntityQuery<RadiationBlockingContainerComponent> _blockerQuery;
    private EntityQuery<RadiationGridResistanceComponent> _resistanceQuery;
    private EntityQuery<MapGridComponent> _gridQuery; //FunkyStation
    private EntityQuery<StackComponent> _stackQuery;
    private EntityQuery<TransformComponent> _xformQuery; // Funkystation: Funky atmos - /tg/ gases

    private readonly B2DynamicTree<EntityUid> _sourceTree = new();
    private readonly Dictionary<EntityUid, SourceData> _sourceDataMap = new();
    private readonly List<EntityUid> _activeReceivers = new();

    private float _accumulator;
    private readonly Dictionary<EntityUid, Dictionary<Vector2i, (float Radiation, TimeSpan ExpiresAt)>> _tileRadiationCache = new(); // Funky atmos - /tg/ gases

    public override void Initialize()
    {
        base.Initialize();
        SubscribeCvars();
        InitRadBlocking();

        _blockerQuery = GetEntityQuery<RadiationBlockingContainerComponent>();
        _resistanceQuery = GetEntityQuery<RadiationGridResistanceComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>(); //FunkyStation
        _stackQuery = GetEntityQuery<StackComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>(); // Funkystation: Funky atmos - /tg/ gases

        SubscribeLocalEvent<RadiationSourceComponent, ComponentInit>(OnSourceInit);
        SubscribeLocalEvent<RadiationSourceComponent, ComponentShutdown>(OnSourceShutdown);
        SubscribeLocalEvent<RadiationSourceComponent, MoveEvent>(OnSourceMove);
        SubscribeLocalEvent<RadiationSourceComponent, StackCountChangedEvent>(OnSourceStackChanged);

        SubscribeLocalEvent<RadiationReceiverComponent, ComponentInit>(OnReceiverInit);
        SubscribeLocalEvent<RadiationReceiverComponent, ComponentShutdown>(OnReceiverShutdown);
    }

    private void OnSourceInit(Entity<RadiationSourceComponent> entity, ref ComponentInit args)
    {
        UpdateSource(entity);
    }

    private void OnSourceShutdown(EntityUid uid, RadiationSourceComponent component, ComponentShutdown args)
    {
        if (component.Proxy != DynamicTree.Proxy.Free)
        {
            _sourceTree.DestroyProxy(component.Proxy);
            component.Proxy = DynamicTree.Proxy.Free;
        }
        _sourceDataMap.Remove(uid);
    }

    private void OnSourceMove(Entity<RadiationSourceComponent> entity, ref MoveEvent args)
    {
        if (args.NewPosition.EntityId == args.OldPosition.EntityId &&
            args.NewPosition.Position.EqualsApprox(args.OldPosition.Position))
            return;

        UpdateSource(entity);
    }

    private void OnSourceStackChanged(Entity<RadiationSourceComponent> entity, ref StackCountChangedEvent args)
    {
        UpdateSource(entity);
    }

    private void OnReceiverInit(EntityUid uid, RadiationReceiverComponent component, ComponentInit args)
    {
        _activeReceivers.Add(uid);
    }

    private void OnReceiverShutdown(EntityUid uid, RadiationReceiverComponent component, ComponentShutdown args)
    {
        _activeReceivers.Remove(uid);
    }

    private void UpdateSource(Entity<RadiationSourceComponent> entity)
    {
        var (uid, component) = entity;
        var xform = Transform(uid);

        if (!component.Enabled || Terminating(uid))
        {
            if (component.Proxy != DynamicTree.Proxy.Free)
            {
                _sourceTree.DestroyProxy(component.Proxy);
                component.Proxy = DynamicTree.Proxy.Free;
            }
            _sourceDataMap.Remove(uid);
            return;
        }

        var worldPos = _transform.GetWorldPosition(xform);
        var intensity = component.Intensity * _stack.GetCount(uid);
        intensity = GetAdjustedRadiationIntensity(uid, intensity);

        if (intensity <= 0)
        {
            if (component.Proxy != DynamicTree.Proxy.Free)
            {
                _sourceTree.DestroyProxy(component.Proxy);
                component.Proxy = DynamicTree.Proxy.Free;
            }
            _sourceDataMap.Remove(uid);
            return;
        }

        // Avoid division by 0
        var maxRange = component.Slope >= float.Epsilon ? intensity / component.Slope : GridcastMaxDistance;
        maxRange = Math.Min(maxRange, GridcastMaxDistance);

        _sourceDataMap[uid] = new SourceData(intensity, component.Slope, maxRange, (uid, component, xform), worldPos);
        var aabb = Box2.CenteredAround(worldPos, new Vector2(maxRange * 2, maxRange * 2));

        if (component.Proxy != DynamicTree.Proxy.Free)
        {
            _sourceTree.MoveProxy(component.Proxy, in aabb);
        }
        else
        {
            component.Proxy = _sourceTree.CreateProxy(in aabb, uint.MaxValue, uid);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < GridcastUpdateRate)
            return;

        UpdateGridcast();
        UpdateResistanceDebugOverlay();
        _accumulator = 0f;
    }

    public void IrradiateEntity(EntityUid uid, float radsPerSecond, float time, EntityUid? origin = null)
    {
        var msg = new OnIrradiatedEvent(time, radsPerSecond, origin);
        RaiseLocalEvent(uid, msg);
    }

    public void SetSourceEnabled(Entity<RadiationSourceComponent?> entity, bool val)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;
        if (entity.Comp.Enabled == val)
            return;

        entity.Comp.Enabled = val;
        UpdateSource((entity.Owner, entity.Comp));
    }

    /// <summary>
    ///     Marks entity to receive/ignore radiation rays.
    /// </summary>
    public void SetCanReceive(EntityUid uid, bool canReceive)
    {
        if (canReceive)
        {
            EnsureComp<RadiationReceiverComponent>(uid);
        }
        else
        {
            RemComp<RadiationReceiverComponent>(uid);
        }
    }

    /// <summary>
    ///     Funky atmos - /tg/ gases
    ///     Get approximate radiation level at given tile coordinates.
    ///     Returns 0 if no data or tile is not cached.
    /// </summary>
    public float GetRadiationAtCoordinates(EntityCoordinates coordinates)
    {
        if (!_gridQuery.TryGetComponent(coordinates.EntityId, out var grid))
            return 0f;

        var tilePos = _maps.TileIndicesFor(coordinates.EntityId, grid, coordinates);

        if (!_tileRadiationCache.TryGetValue(coordinates.EntityId, out var tileCache))
            return 0f;

        return tileCache.TryGetValue(tilePos, out var data) ? data.Radiation : 0f;
    }

    /// <summary>
    ///     Funky atmos - /tg/ gases
    ///     Requests radiation for a specific tile during the next radiation update.
    ///     The tile will be treated as a virtual receiver and its radiation value cached.
    ///     Automatically expires if not refreshed within the timeout.
    /// </summary>
    /// <param name="coordinates">The tile coordinates to sample</param>
    /// <param name="timeoutSeconds">How long this request remains active if not refreshed (default 5s)</param>
    public void RequestTileRadiationSampling(EntityCoordinates coordinates, float timeoutSeconds = 5f)
    {
        var gridUid = coordinates.GetGridUid(EntityManager);
        if (gridUid == null || !_gridQuery.TryGetComponent(gridUid.Value, out var gridComp))
            return;

        var tilePos = _maps.TileIndicesFor((gridUid.Value, gridComp), coordinates);
        var expires = _gameTiming.CurTime + TimeSpan.FromSeconds(timeoutSeconds);

        if (!_tileRadiationCache.TryGetValue(gridUid.Value, out var samples))
        {
            samples = new Dictionary<Vector2i, (float, TimeSpan)>();
            _tileRadiationCache[gridUid.Value] = samples;
        }

        // Check if we already have a value here
        if (samples.TryGetValue(tilePos, out var existing))
        {
            // Preserve the existing radiation value, just push the expiration date back
            samples[tilePos] = (existing.Radiation, expires);
        }
        else
        {
            // Only set to 0 if it's a brand new entry
            samples[tilePos] = (0f, expires);
        }
    }
}
