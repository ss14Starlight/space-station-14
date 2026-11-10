// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Editor;
using Content.Shared._Starlight.Body.Events;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Utility;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;

namespace Content.Shared._Starlight.Body.Systems;

public abstract partial class SharedBodyVisualizerSystem : EntitySystem
{
    public static ComponentRegistry CreateAppearanceOverride(BodyProfile? appearance)
    {
        return new ComponentRegistry
        {
            ["BodyVisualizer"] = new EntityPrototype.ComponentRegistryEntry(
                new BodyVisualizerComponent { Appearance = appearance?.Clone() })
        };
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyVisualizerComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<BodyVisualizerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<BodyPartVisualizerComponent, SLBodyPartAddedEvent>(OnBodyPartAdded);
        SubscribeLocalEvent<BodyPartVisualizerComponent, SLBodyPartRemovedEvent>(OnBodyPartRemoved);
    }

    private void OnBodyPartAdded(EntityUid uid, BodyPartVisualizerComponent partVis, ref SLBodyPartAddedEvent args)
    {
        if (!TryComp<SLBodyPartComponent>(uid, out var part))
            return;

        if (!TryComp<BodyVisualizerComponent>(part.Body, out var bodyVis))
            return;

        var allowed = GetAllowedLayers(partVis, part);
        foreach (var (layerId, specifier) in partVis.BodyVisualLayers)
        {
            if (!IsLayerAllowed(layerId, allowed))
                continue;
            SetLayer(part.Body.Value, layerId, specifier, bodyVis);
        }
    }

    private void OnBodyPartRemoved(EntityUid uid, BodyPartVisualizerComponent partVis, ref SLBodyPartRemovedEvent args)
    {
        if (!TryComp<SLBodyPartComponent>(uid, out var part))
            return;

        if (!TryComp<BodyVisualizerComponent>(part.Body, out var bodyVis))
            return;

        var allowed = GetAllowedLayers(partVis, part);
        foreach (var layerId in partVis.BodyVisualLayers.Keys)
        {
            if (!IsLayerAllowed(layerId, allowed))
                continue;
            RemoveLayer(part.Body.Value, layerId, bodyVis);
        }
    }

    private List<VisualLayerKey>? GetAllowedLayers(BodyPartVisualizerComponent vis, SLBodyPartComponent part)
    {
        if (vis.SocketLayers.Count == 0)
            return null;

        var socketId = part.ParentSocket?.SocketId;
        if (socketId == null)
            return vis.SocketLayers.TryGetValue("root", out var rootLayers) ? rootLayers : null;

        return vis.SocketLayers.TryGetValue(socketId, out var layers) ? layers : null;
    }

    private bool IsLayerAllowed(VisualLayerKey layerId, List<VisualLayerKey>? allowed)
    {
        if (allowed == null)
            return true;

        foreach (var allowedKey in allowed)
        {
            if (allowedKey.Layer == layerId.Layer)
                return true;
        }

        return false;
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
        // Always a full state: the component usually comes from the entity prototype, whose CreationTick is
        // cleared to zero, so a FromTick/CreationTick delta check would send a delta as the very first state.
        args.State = new BodyVisualizerFullState(component.Offset, component.LayerData);
    }

    internal void SetPartLayerColor(Entity<SLBodyComponent, BodyVisualizerComponent> bodyVis, Entity<SLBodyPartComponent, BodyPartVisualizerComponent> partVis, VisualLayerKey key, Color color)
    {
        if (!partVis.Comp2.BodyVisualLayers.TryGetValue(key, out var existing))
            return;

        if (existing.SpriteColor == color)
            return;

        existing.SpriteColor = color;
        partVis.Comp2.BodyVisualLayers[key] = existing;
        Dirty(partVis, partVis.Comp2);

        var allowed = GetAllowedLayers(partVis.Comp2, partVis.Comp1);
        if (!IsLayerAllowed(key, allowed))
            return;

        SetLayer(bodyVis, key, existing, bodyVis.Comp2);
    }
}

