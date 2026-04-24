// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared.Starlight.Utility;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Body.Systems;

public abstract class SharedBodyVisualizerSystem : EntitySystem
{
    private static readonly Dictionary<ProtoId<VisualLayerPrototype>, ExtendedSpriteSpecifier> _emptyModified = [];

    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyVisualizerComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<BodyVisualizerComponent, ComponentInit>(OnInit);
    }

    /// <summary>
    /// Sets or updates a visual layer, tracking the change tick for delta networking.
    /// </summary>
    public void SetLayer(
        EntityUid uid,
        ProtoId<VisualLayerPrototype> layerId,
        ExtendedSpriteSpecifier specifier,
        BodyVisualizerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.LayerData[layerId] = specifier;
        component.LayerKeys.Add(layerId);
        component.LayerModifiedTicks[layerId] = _timing.CurTick;
        Dirty(uid, component);
    }

    /// <summary>
    /// Removes a visual layer. Removal is detected by clients via the delta's full key set.
    /// </summary>
    public void RemoveLayer(
        EntityUid uid,
        ProtoId<VisualLayerPrototype> layerId,
        BodyVisualizerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!component.LayerData.Remove(layerId))
            return;

        component.LayerKeys.Remove(layerId);
        component.LayerModifiedTicks.Remove(layerId);
        Dirty(uid, component);
    }

    /// <summary>
    /// Sets the sprite offset.
    /// </summary>
    public void SetOffset(EntityUid uid, Vector2 offset, BodyVisualizerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.Offset == offset)
            return;

        component.Offset = offset;
        Dirty(uid, component);
    }

    private static void OnInit(EntityUid uid, BodyVisualizerComponent component, ComponentInit args)
    {
        if (component.LayerKeys.Count != 0)
            return;

        foreach (var key in component.LayerData.Keys)
            component.LayerKeys.Add(key);
    }

    private void OnGetState(EntityUid uid, BodyVisualizerComponent component, ref ComponentGetState args)
    {
        if (args.FromTick <= component.CreationTick)
        {
            args.State = new BodyVisualizerFullState(component.Offset, component.LayerData);
            return;
        }

        Dictionary<ProtoId<VisualLayerPrototype>, ExtendedSpriteSpecifier>? modifiedLayers = null;
        foreach (var (key, tick) in component.LayerModifiedTicks)
        {
            if (tick < args.FromTick)
                continue;

            if (!component.LayerData.TryGetValue(key, out var value))
                continue;

            (modifiedLayers ??= [])[key] = value;
        }

        args.State = new BodyVisualizerDeltaState(component.Offset, modifiedLayers ?? _emptyModified, component.LayerKeys);
    }

    }
