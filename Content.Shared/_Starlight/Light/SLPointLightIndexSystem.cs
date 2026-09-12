using System.Numerics;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;

namespace Content.Shared._Starlight.Light;

public sealed partial class SLPointLightIndexSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    private readonly Dictionary<EntityUid, Dictionary<EntityUid, Vector2>> _lights = new();

    private readonly Dictionary<EntityUid, EntityUid> _lightTrees = new();

    private readonly List<EntityUid> _staleTrees = new();

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    public bool Enabled { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        if (_net.IsClient)
            return;

        Enabled = true;
        _xformQuery = GetEntityQuery<TransformComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();

        SubscribeLocalEvent<SLPointLightComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SLPointLightComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SLPointLightComponent, MoveEvent>(OnMove);
    }

    private void OnStartup(Entity<SLPointLightComponent> ent, ref ComponentStartup args)
        => Update(ent.Owner);

    private void OnShutdown(Entity<SLPointLightComponent> ent, ref ComponentShutdown args)
        => Remove(ent.Owner);

    private void OnMove(Entity<SLPointLightComponent> ent, ref MoveEvent args)
        => Update(ent.Owner, args.Component);

    private void Update(EntityUid uid, TransformComponent? xform = null)
    {
        if (!_xformQuery.Resolve(uid, ref xform, false))
            return;

        var tree = xform.GridUid ?? xform.MapUid;
        if (tree == null || _container.IsEntityInContainer(uid))
        {
            Remove(uid);
            return;
        }

        if (_lightTrees.TryGetValue(uid, out var oldTree) && oldTree != tree.Value)
            RemoveFromTree(oldTree, uid);

        _lightTrees[uid] = tree.Value;

        if (!_lights.TryGetValue(tree.Value, out var lights))
            _lights[tree.Value] = lights = new Dictionary<EntityUid, Vector2>();

        lights[uid] = _transform.GetRelativePosition(xform, tree.Value);
    }

    private void Remove(EntityUid uid)
    {
        if (!_lightTrees.Remove(uid, out var tree))
            return;

        RemoveFromTree(tree, uid);
    }

    private void RemoveFromTree(EntityUid tree, EntityUid uid)
    {
        if (_lights.TryGetValue(tree, out var lights) && lights.Remove(uid) && lights.Count == 0)
            _lights.Remove(tree);
    }

    public void GetLightsInRange(MapCoordinates coordinates, float range, List<EntityUid> lights)
    {
        if (!Enabled || coordinates.MapId == MapId.Nullspace)
            return;

        var rangeSquared = range * range;

        foreach (var (tree, treeLights) in _lights)
        {
            if (!_xformQuery.TryGetComponent(tree, out var treeXform) || treeXform.MapID != coordinates.MapId)
            {
                if (!_xformQuery.HasComponent(tree))
                    _staleTrees.Add(tree);

                continue;
            }

            var localPos = _gridQuery.HasComponent(tree)
                ? Vector2.Transform(coordinates.Position, _transform.GetInvWorldMatrix(treeXform))
                : coordinates.Position;

            foreach (var (light, position) in treeLights)
            {
                if ((position - localPos).LengthSquared() <= rangeSquared)
                    lights.Add(light);
            }
        }

        foreach (var tree in _staleTrees)
        {
            if (_lights.Remove(tree, out var removed))
            {
                foreach (var light in removed.Keys)
                {
                    _lightTrees.Remove(light);
                }
            }
        }

        _staleTrees.Clear();
    }
}
