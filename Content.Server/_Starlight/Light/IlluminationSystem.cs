using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using Content.Shared._Starlight.Light;
using Robust.Server.GameObjects;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Light;

/// <summary>
/// Server side light index, kept without the engine light tree.
/// </summary>
public sealed partial class IlluminationSystem : SharedIlluminationSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private OccluderSystem _occluder = default!;

    [Dependency] private EntityQuery<PointLightComponent> _lightQuery = default!;
    [Dependency] private EntityQuery<ContainerManagerComponent> _containerManagerQuery = default!;
    [Dependency] private EntityQuery<MapComponent> _mapQuery = default!;
    [Dependency] private EntityQuery<MapLightComponent> _mapLightQuery = default!;

    #region Smth smth useful hardcode
    private const int ChunkSize = 16;
    private const float LightHeight = 1f;
    private const float OffsetMargin = 1f;
    private const float OwnOccluderTolerance = 0.05f;
    #endregion

    private float _maxLightRadius;

    private readonly Dictionary<(EntityUid Grid, Vector2i Chunk), List<Entity<SharedPointLightComponent, TransformComponent>>> _anchored = new();
    private readonly Dictionary<EntityUid, (EntityUid Grid, Vector2i Chunk)> _anchoredIndex = new();

    private readonly List<Entity<SharedPointLightComponent, TransformComponent>> _loose = new();
    private readonly Dictionary<EntityUid, int> _looseIndex = new();

    private readonly List<Candidate> _candidates = new();
    private readonly List<Entity<SharedPointLightComponent, TransformComponent>> _misplaced = new();
    private readonly List<LightContribution> _sources = new();
    private readonly List<RayCastResults> _occluderHits = new();
    private List<Entity<MapGridComponent>> _grids = new();

    private readonly Dictionary<string, MaskImage?> _masks = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CVars.MaxLightRadius, value => _maxLightRadius = value, true);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<LightMaskPrototype>())
            _masks.Clear();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _anchored.Clear();
        _anchoredIndex.Clear();
        _loose.Clear();
        _looseIndex.Clear();
    }

    #region Index

    [SubscribeLocalEvent]
    private void OnLightInit(Entity<PointLightComponent> ent, ref ComponentInit _)
        => AddLight((ent, ent.Comp, Transform(ent)));

    [SubscribeLocalEvent]
    private void OnLightRemove(Entity<PointLightComponent> ent, ref ComponentRemove _)
        => RemoveLight(ent);

    [SubscribeLocalEvent]
    private void OnAnchorChanged(Entity<PointLightComponent> ent, ref AnchorStateChangedEvent args)
        => Relocate((ent, ent.Comp, args.Transform));

    [SubscribeLocalEvent]
    private void OnReAnchor(Entity<PointLightComponent> ent, ref ReAnchorEvent args)
        => Relocate((ent, ent.Comp, args.Xform));

    private void Relocate(Entity<SharedPointLightComponent, TransformComponent> light)
    {
        RemoveLight(light);

        if (light.Comp1.LifeStage <= ComponentLifeStage.Running)
            AddLight(light);
    }

    private void AddLight(Entity<SharedPointLightComponent, TransformComponent> light)
    {
        if (IsAnchoredToGrid(light.Comp2, out var grid))
        {
            var key = (grid, ChunkOf(light.Comp2.LocalPosition));
            if (!_anchored.TryGetValue(key, out var bucket))
            {
                bucket = new();
                _anchored[key] = bucket;
            }

            bucket.Add(light);
            _anchoredIndex[light] = key;
            return;
        }

        _looseIndex[light] = _loose.Count;
        _loose.Add(light);
    }

    private void RemoveLight(EntityUid uid)
    {
        if (_anchoredIndex.Remove(uid, out var key))
        {
            var bucket = _anchored[key];
            for (var i = 0; i < bucket.Count; i++)
            {
                if (bucket[i].Owner != uid)
                    continue;

                bucket[i] = bucket[^1];
                bucket.RemoveAt(bucket.Count - 1);
                break;
            }

            if (bucket.Count == 0)
                _anchored.Remove(key);
            return;
        }

        if (!_looseIndex.Remove(uid, out var index))
            return;

        var last = _loose[^1];
        _loose.RemoveAt(_loose.Count - 1);
        if (last.Owner == uid)
            return;

        _loose[index] = last;
        _looseIndex[last] = index;
    }

    private static bool IsAnchoredToGrid(TransformComponent xform, out EntityUid grid)
    {
        grid = xform.GridUid ?? default;
        return xform.Anchored && xform.GridUid != null && xform.ParentUid == grid;
    }

    private static Vector2i ChunkOf(Vector2 local)
        => new((int) MathF.Floor(local.X / ChunkSize), (int) MathF.Floor(local.Y / ChunkSize));

    #endregion

    #region Queries

    public override void GetLightSources(MapCoordinates point, List<LightContribution> results, IlluminationQuery query = default)
    {
        if (point.MapId == MapId.Nullspace)
            return;

        CollectCandidates(point, _maxLightRadius + OffsetMargin);

        foreach (var candidate in _candidates)
        {
            if (TryEvaluate(candidate, point.Position, query, out var contribution)
                && (!contribution.Light.Comp1.CastShadows || !IsOccluded(point.MapId, contribution.Position, point.Position)))
                results.Add(contribution);
        }
    }

    public override void GetLightSources(EntityUid uid, List<LightContribution> results, IlluminationQuery query = default)
    {
        if (TryGetSealingContainer(uid, out var container))
        {
            GetLightsInContainer(container, _transform.GetWorldPosition(uid), results, query);
            return;
        }

        GetLightSources(_transform.GetMapCoordinates(uid), results, query);
    }

    public override void GetLightsInRange(MapCoordinates center, float range, List<Entity<SharedPointLightComponent, TransformComponent>> results)
    {
        if (center.MapId == MapId.Nullspace)
            return;

        CollectCandidates(center, range);

        foreach (var candidate in _candidates)
        {
            results.Add(candidate.Light);
        }
    }

    public override bool TryGetLightColor(MapCoordinates point, out Color color, IlluminationQuery query = default)
    {
        if (!TryGetAmbient(point.MapId, out var ambient, out var lightingEnabled))
        {
            color = default;
            return false;
        }

        if (!lightingEnabled)
        {
            color = Color.White;
            return true;
        }

        _sources.Clear();
        GetLightSources(point, _sources, query);
        color = SumColor(query.IgnoreAmbient ? Vector4.Zero : ambient.RGBA);
        return true;
    }

    public override bool TryGetLightColor(EntityUid uid, out Color color, IlluminationQuery query = default)
    {
        if (!TryGetSealingContainer(uid, out _))
            return TryGetLightColor(_transform.GetMapCoordinates(uid), out color, query);

        _sources.Clear();
        GetLightSources(uid, _sources, query);
        color = SumColor(Vector4.Zero);
        return true;
    }

    private Color SumColor(Vector4 color)
    {
        foreach (var source in _sources)
        {
            color += source.Light.Comp1.Color.RGBA * source.Intensity;
        }

        return new Color(color);
    }

    private void CollectCandidates(MapCoordinates point, float range)
    {
        _candidates.Clear();
        _misplaced.Clear();

        var rangeSquared = range * range;
        var bounds = new Box2(point.Position, point.Position).Enlarged(range);

        _grids.Clear();
        _map.FindGridsIntersecting(point.MapId, bounds, ref _grids, approx: true, includeMap: true);

        foreach (var grid in _grids)
        {
            var (_, gridRot, gridMatrix) = _transform.GetWorldPositionRotationMatrix(grid.Owner);
            var local = Vector2.Transform(point.Position, _transform.GetInvWorldMatrix(grid.Owner));
            var min = ChunkOf(local - new Vector2(range));
            var max = ChunkOf(local + new Vector2(range));

            for (var x = min.X; x <= max.X; x++)
            {
                for (var y = min.Y; y <= max.Y; y++)
                {
                    if (!_anchored.TryGetValue((grid.Owner, new Vector2i(x, y)), out var bucket))
                        continue;

                    foreach (var light in bucket)
                    {
                        var xform = light.Comp2;

                        if (!IsAnchoredToGrid(xform, out var lightGrid)
                            || lightGrid != grid.Owner
                            || ChunkOf(xform.LocalPosition) != new Vector2i(x, y))
                        {
                            _misplaced.Add(light);
                            TryAddLoose(light, point, rangeSquared);
                            continue;
                        }

                        if (!light.Comp1.Enabled)
                            continue;

                        var position = Vector2.Transform(xform.LocalPosition, gridMatrix);
                        if ((position - point.Position).LengthSquared() > rangeSquared)
                            continue;

                        _candidates.Add(new(light, position, gridRot + xform.LocalRotation));
                    }
                }
            }
        }

        foreach (var light in _loose)
        {
            if (light.Comp1.Enabled && IsAnchoredToGrid(light.Comp2, out _))
                _misplaced.Add(light);

            TryAddLoose(light, point, rangeSquared);
        }

        foreach (var light in _misplaced)
        {
            Relocate(light);
        }
    }

    private void TryAddLoose(Entity<SharedPointLightComponent, TransformComponent> light, MapCoordinates point, float rangeSquared)
    {
        if (!light.Comp1.Enabled || light.Comp2.MapID != point.MapId)
            return;

        var (position, rotation) = _transform.GetWorldPositionRotation(light.Comp2);
        if ((position - point.Position).LengthSquared() > rangeSquared)
            return;

        if (IsHiddenInContainer(light))
            return;

        _candidates.Add(new(light, position, rotation));
    }

    private bool TryEvaluate(Candidate candidate, Vector2 target, IlluminationQuery query, out LightContribution contribution)
    {
        contribution = default;
        var light = candidate.Light.Comp1;

        if (light.Energy == 0f || light.Radius <= 0f)
            return false;

        if (query.Ignore?.Invoke(candidate.Light) == true)
            return false;

        var position = candidate.Position + candidate.Rotation.RotateVec(light.Offset);
        var delta = target - position;
        var radius = MathF.Min(light.Radius, _maxLightRadius);
        var distanceSquared = delta.LengthSquared();
        if (distanceSquared > radius * radius)
            return false;

        var mask = GetMaskFactor(light, delta, candidate.Rotation);
        if (mask <= 0f)
            return false;

        var intensity = GetFalloff(light, distanceSquared, radius) * light.Energy * mask;
        if (intensity == 0f)
            return false;

        contribution = new(candidate.Light, position, MathF.Sqrt(distanceSquared), radius, mask, intensity);
        return true;
    }

    private static float GetFalloff(SharedPointLightComponent light, float distanceSquared, float radius)
    {
        var s = Math.Clamp(MathF.Sqrt(distanceSquared + LightHeight) / radius, 0f, 1f);
        var s2 = s * s;
        var curve = MathHelper.Lerp(s, s2, Math.Clamp(light.CurveFactor, 0f, 1f));
        return Math.Clamp((1f - s2) * (1f - s2) / (1f + (light.Falloff * curve)), 0f, 1f);
    }

    private float GetMaskFactor(SharedPointLightComponent light, Vector2 delta, Angle worldRotation)
    {
        if (light.LightMask is not { } maskId || !_proto.TryIndex(maskId, out var mask))
            return 1f;

        var maskRotation = SharedPointLightSystem.GetMaskWorldRotation(light, worldRotation);

        if (GetMaskImage(mask) is { } image)
        {
            var local = (-maskRotation).RotateVec(delta) / light.Radius;
            var u = (local.X + 1f) * 0.5f;
            var v = (local.Y + 1f) * 0.5f;
            if (u < 0f || u >= 1f || v < 0f || v >= 1f)
                return 0f;

            return image.Values[((int) (v * image.Height) * image.Width) + (int) (u * image.Width)] / 255f;
        }

        var relativeAngle = MathHelper.CloseTo(delta.LengthSquared(), 0f)
            ? Angle.Zero
            : Angle.FromWorldVec(delta) - maskRotation;

        var factor = 0d;
        foreach (var cone in mask.LightCones)
        {
            var offset = Math.Abs(Angle.ShortestDistance(relativeAngle, cone.Direction));
            if (offset > cone.OuterWidth)
                continue;

            if (offset < cone.InnerWidth)
                return 1f;

            factor = Math.Max(factor, (cone.OuterWidth - offset) / (cone.OuterWidth - cone.InnerWidth));
        }

        return MathF.Min(1f, (float) factor);
    }

    private MaskImage? GetMaskImage(LightMaskPrototype mask)
    {
        if (_masks.TryGetValue(mask.ID, out var cached))
            return cached;

        MaskImage? image = null;
        try
        {
            if (_resources.TryContentFileRead(mask.MaskPath, out var stream))
            {
                using var buffer = new MemoryStream();
                using (stream)
                    stream.CopyTo(buffer);

                buffer.Position = 0;
                if (LightMaskReader.TryRead(buffer, out var width, out var height, out var values))
                    image = new MaskImage(width, height, values);
                else
                    Log.Warning($"Light mask {mask.ID} texture {mask.MaskPath} isn't a plain 8-bit PNG, falling back to its cones.");
            }
            else
            {
                Log.Warning($"Light mask {mask.ID} texture {mask.MaskPath} not found, falling back to its cones.");
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to read light mask {mask.ID} texture {mask.MaskPath}, falling back to its cones: {e}");
        }

        _masks[mask.ID] = image;
        return image;
    }

    private bool IsOccluded(MapId map, Vector2 from, Vector2 to)
    {
        var delta = to - from;
        var length = delta.Length();
        if (MathHelper.CloseTo(length, 0f))
            return false;

        _occluderHits.Clear();
        _occluder.IntersectRay(_occluderHits, map, new Ray(to, -delta / length), length);

        foreach (var hit in _occluderHits)
        {
            if (hit.Distance < length - OwnOccluderTolerance)
                return true;
        }

        return false;
    }

    private bool TryGetAmbient(MapId mapId, out Color color, out bool lightingEnabled)
    {
        color = default;
        lightingEnabled = false;

        if (!_map.TryGetMap(mapId, out var mapUid) || !_mapQuery.TryComp(mapUid, out var map))
            return false;

        lightingEnabled = map.LightingEnabled;
        color = _mapLightQuery.CompOrNull(mapUid.Value)?.AmbientLightColor ?? MapLightComponent.DefaultColor;
        return true;
    }

    #endregion

    #region Containers

    private bool IsHiddenInContainer(EntityUid uid)
    {
        var current = uid;
        while (_container.TryGetContainingContainer(current, out var container))
        {
            if (container.OccludesLight)
                return true;

            current = container.Owner;
        }

        return false;
    }

    private bool TryGetSealingContainer(EntityUid uid, [NotNullWhen(true)] out BaseContainer? sealing)
    {
        var current = uid;
        while (_container.TryGetContainingContainer(current, out sealing))
        {
            if (sealing.OccludesLight)
                return true;

            current = sealing.Owner;
        }

        return false;
    }

    private void GetLightsInContainer(BaseContainer container, Vector2 target, List<LightContribution> results, IlluminationQuery query)
    {
        foreach (var contained in container.ContainedEntities)
        {
            if (_lightQuery.TryComp(contained, out var light) && light.Enabled)
            {
                var xform = Transform(contained);
                var (position, rotation) = _transform.GetWorldPositionRotation(xform);
                if (TryEvaluate(new((contained, light, xform), position, rotation), target, query, out var contribution))
                    results.Add(contribution);
            }

            if (!_containerManagerQuery.TryComp(contained, out var manager))
                continue;

            foreach (var inner in manager.Containers.Values)
            {
                if (!inner.OccludesLight)
                    GetLightsInContainer(inner, target, results, query);
            }
        }
    }

    #endregion

    private sealed record MaskImage(int Width, int Height, byte[] Values);

    private readonly record struct Candidate(
        Entity<SharedPointLightComponent, TransformComponent> Light,
        Vector2 Position,
        Angle Rotation);
}
