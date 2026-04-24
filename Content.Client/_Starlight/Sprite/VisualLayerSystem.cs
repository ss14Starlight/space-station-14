// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Linq;
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

    private List<ProtoId<VisualLayerPrototype>> _layerOrderCache = [];

    /// <summary>
    /// Layer IDs that could not be sorted due to circular dependencies. Empty if no cycles exist.
    /// </summary>
    public IReadOnlyList<string> CyclicLayers { get; private set; } = [];

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

        if (IsOrdered(ent!))
            return;

        var copy = list.ToArray();
        list.Clear();

        foreach (var layerId in _layerOrderCache)
        {
            if (_sprite.LayerMapTryGet(ent, layerId, out var index, false))
            {
                list.Add(copy[index]);
                _sprite.LayerMapSet(ent, layerId, list.Count - 1);
            }
        }
    }

    private bool IsOrdered(Entity<SpriteComponent?> ent)
    {
        var lastIndex = -1;
        foreach (var layerId in _layerOrderCache)
        {
            if (!_sprite.LayerMapTryGet(ent, layerId, out var index, false))
                continue;

            if (index <= lastIndex)
                return false;

            lastIndex = index;
        }
        return true;
    }

    private void BuildLayerOrderCache()
    {
        var inDegree = new Dictionary<string, int>();
        var edges = new Dictionary<string, List<string>>();

        foreach (var proto in _prototypeManager.EnumeratePrototypes<VisualLayerPrototype>())
        {
            inDegree.TryAdd(proto.ID, 0);
            edges.TryAdd(proto.ID, []);

            if (proto.AboveLayers != null)
            {
                foreach (var below in proto.AboveLayers)
                {
                    edges.TryAdd(below.Id, []);
                    inDegree.TryAdd(below.Id, 0);
                    edges[below.Id].Add(proto.ID);
                    inDegree[proto.ID]++;
                }
            }

            if (proto.BelowLayers != null)
            {
                foreach (var above in proto.BelowLayers)
                {
                    inDegree.TryAdd(above.Id, 0);
                    edges[proto.ID].Add(above.Id);
                    inDegree[above.Id]++;
                }
            }
        }

        var queue = new Queue<string>();
        foreach (var (id, degree) in inDegree)
        {
            if (degree == 0)
                queue.Enqueue(id);
        }

        var sorted = new List<string>();
        while (queue.TryDequeue(out var current))
        {
            sorted.Add(current);
            foreach (var next in edges[current])
            {
                inDegree[next]--;
                if (inDegree[next] == 0)
                    queue.Enqueue(next);
            }
        }

        var cyclic = new List<string>();
        foreach (var (id, degree) in inDegree)
        {
            if (degree > 0)
            {
                DebugTools.Assert(false, $"BodyVisualLayer cycle detected: '{id}' is part of a circular dependency.");
                cyclic.Add(id);
                sorted.Add(id);
            }
        }

        CyclicLayers = cyclic;
        _layerOrderCache = [.. sorted.Select(id => new ProtoId<VisualLayerPrototype>(id))];
    }
}
