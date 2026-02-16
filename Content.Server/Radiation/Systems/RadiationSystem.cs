using Content.Server.Radiation.Components;
using Content.Shared.Radiation.Components;
using Content.Shared.Radiation.Events;
using Content.Shared.Stacks;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
// Funkystation Start
using Robust.Shared.Timing;
// Funkystation End

namespace Content.Server.Radiation.Systems;

public sealed partial class RadiationSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!; // Funkystation: Funky atmos - /tg/ gases

    private EntityQuery<RadiationBlockingContainerComponent> _blockerQuery;
    private EntityQuery<RadiationGridResistanceComponent> _resistanceQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<StackComponent> _stackQuery;
    private EntityQuery<TransformComponent> _xformQuery; // Funkystation: Funky atmos - /tg/ gases

    private float _accumulator;
    private List<SourceData> _sources = new();
    private readonly Dictionary<EntityUid, Dictionary<Vector2i, (float Radiation, TimeSpan ExpiresAt)>> _tileRadiationCache = new(); // Funky atmos - /tg/ gases

    public override void Initialize()
    {
        base.Initialize();
        SubscribeCvars();
        InitRadBlocking();

        _blockerQuery = GetEntityQuery<RadiationBlockingContainerComponent>();
        _resistanceQuery = GetEntityQuery<RadiationGridResistanceComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _stackQuery = GetEntityQuery<StackComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>(); // Funkystation: Funky atmos - /tg/ gases
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

        entity.Comp.Enabled = val;
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
