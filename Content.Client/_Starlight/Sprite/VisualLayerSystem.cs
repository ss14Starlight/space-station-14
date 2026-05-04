// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Server.Administration.Systems;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Prototypes;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._Starlight.Sprite;

public sealed class VisualLayerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly StarlightEntitySystem _sl = default!;

    private readonly Dictionary<string, int> _layerOrder = [];
    private readonly List<(VisualLayerKey Key, string Str, int Order)> _scratch = [];

    private Layer[] _layerSwapBuffer = [];

    /// <summary>
    /// Layer IDs that could not be sorted due to circular dependencies. Empty if no cycles exist.
    /// </summary>
    public IReadOnlyList<string> CyclicLayers { get; private set; } = [];

    private sealed class LayerDependencyGraph
    {
        private readonly Dictionary<string, int> _inDegree = [];
        private readonly Dictionary<string, HashSet<string>> _edges = [];

        public void AddNode(string id)
        {
            _inDegree.TryAdd(id, 0);
            _edges.TryAdd(id, []);
        }

        public void AddEdge(string from, string to)
        {
            AddNode(from);
            AddNode(to);

            // Only count the edge once: duplicates (symmetric Above/Below or repeated entries)
            // must not inflate in-degree, otherwise nodes never reach zero and look cyclic.
            if (_edges[from].Add(to))
                _inDegree[to]++;
        }

        public TopologicalSortResult Sort()
        {
            var inDegree = new Dictionary<string, int>(_inDegree);
            var order = new Dictionary<string, int>();
            var queue = new Queue<string>();

            foreach (var (id, degree) in inDegree)
            {
                if (degree == 0)
                    queue.Enqueue(id);
            }

            var nextOrder = 0;
            while (queue.TryDequeue(out var current))
            {
                order[current] = nextOrder++;
                foreach (var next in _edges[current])
                {
                    if (--inDegree[next] == 0)
                        queue.Enqueue(next);
                }
            }

            List<string>? cyclic = null;
            foreach (var (id, degree) in inDegree)
            {
                if (degree > 0)
                {
                    (cyclic ??= []).Add(id);
                    order[id] = nextOrder++;
                }
            }

            return new TopologicalSortResult(order, (IReadOnlyList<string>?)cyclic ?? []);
        }
    }

    private readonly record struct TopologicalSortResult(
        Dictionary<string, int> Order,
        IReadOnlyList<string> CyclicNodes);

    public override void Initialize()
    {
        base.Initialize();
        BuildLayerOrderCache();
        _prototypeManager.PrototypesReloaded += _ => BuildLayerOrderCache();
    }

    public void ReorderSpriteLayers(EntityUid uid, BodyVisualizerComponent component)
    {
        var ent = _sl.Entity<SpriteComponent>(uid);

        // Yeah, these are horrible hacks and a war crime against encapsulation.
        // But what are we supposed to do? The sprite API is the worst API in the entire engine.
        if (ent.Comp?.AllLayers is not List<Layer> list)
            return;

        var keys = component.LayerKeys;
        if (keys.Count == 0)
            return;

        _scratch.Clear();
        if (_scratch.Capacity < keys.Count)
            _scratch.Capacity = keys.Count;

        foreach (var key in keys)
        {
            if (!_layerOrder.TryGetValue(key.Layer.Id, out var order))
                order = int.MaxValue;
            _scratch.Add((key, key.ToString(), order));
        }

        _scratch.Sort(static (a, b) =>
        {
            var c = a.Order.CompareTo(b.Order);
            if (c != 0)
                return c;

            var ai = a.Key.Index ?? int.MinValue;
            var bi = b.Key.Index ?? int.MinValue;
            c = ai.CompareTo(bi);
            if (c != 0)
                return c;

            return b.Key.Displacement.CompareTo(a.Key.Displacement);
        });

        if (IsOrdered(ent!))
            return;

        if (_layerSwapBuffer.Length < list.Count)
        {
            var newSize = _layerSwapBuffer.Length == 0 ? 16 : _layerSwapBuffer.Length * 2;
            while (newSize < list.Count)
                newSize *= 2;
            _layerSwapBuffer = new Layer[newSize];
        }

        var copy = _layerSwapBuffer;
        list.CopyTo(copy);
        var oldCount = list.Count;
        list.Clear();

        for (var i = 0; i < _scratch.Count; i++)
        {
            var (Key, Str, Order) = _scratch[i];
            if (_sprite.LayerMapTryGet(ent, Str, out var index, false))
            {
                list.Add(copy[index]);
                _sprite.LayerMapSet(ent, Str, list.Count - 1);
            }
        }

        Array.Clear(copy, 0, oldCount);
    }

    private bool IsOrdered(Entity<SpriteComponent?> ent)
    {
        var lastIndex = -1;
        for (var i = 0; i < _scratch.Count; i++)
        {
            if (!_sprite.LayerMapTryGet(ent, _scratch[i].Str, out var index, false))
                continue;

            if (index <= lastIndex)
                return false;

            lastIndex = index;
        }
        return true;
    }

    private void BuildLayerOrderCache()
    {
        var graph = new LayerDependencyGraph();

        foreach (var proto in _prototypeManager.EnumeratePrototypes<VisualLayerPrototype>())
        {
            graph.AddNode(proto.ID);

            if (proto.AboveLayers != null)
            {
                foreach (var below in proto.AboveLayers)
                    graph.AddEdge(below.Id, proto.ID);
            }

            if (proto.BelowLayers != null)
            {
                foreach (var above in proto.BelowLayers)
                    graph.AddEdge(proto.ID, above.Id);
            }
        }

        var result = graph.Sort();
        _layerOrder.Clear();
        foreach (var (id, order) in result.Order)
            _layerOrder[id] = order;

        foreach (var id in result.CyclicNodes)
        {
            DebugTools.Assert(false, $"BodyVisualLayer cycle detected: '{id}' is part of a circular dependency.");
        }

        CyclicLayers = result.CyclicNodes;
    }
}

