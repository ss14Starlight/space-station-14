// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared.Starlight.Utility;

namespace Content.Shared._Starlight.Body.Systems;

public abstract partial class SharedBodyVisualizerSystem
{
    /// <summary>
    /// Sets or updates a visual layer on the body part. If the part is currently attached to a
    /// body with a <see cref="BodyVisualizerComponent"/>, the change is also propagated there.
    /// </summary>
    public void SetPartLayer(
        EntityUid partUid,
        VisualLayerKey layerId,
        BodySpriteSpecifier specifier,
        BodyPartVisualizerComponent? partVis = null,
        SLBodyPartComponent? part = null)
    {
        if (!Resolve(partUid, ref partVis, logMissing: false))
            return;

        if (partVis.BodyVisualLayers.TryGetValue(layerId, out var existing)
            && existing.Equals(specifier))
            return;

        partVis.BodyVisualLayers[layerId] = specifier;
        Dirty(partUid, partVis);

        if (!Resolve(partUid, ref part, logMissing: false))
            return;

        if (!TryComp<BodyVisualizerComponent>(part.Body, out var bodyVis))
            return;

        SetLayer(part.Body, layerId, specifier, bodyVis);
    }

    /// <summary>
    /// Removes a visual layer from the body part. If the part is currently attached to a
    /// body with a <see cref="BodyVisualizerComponent"/>, the removal is also propagated there.
    /// </summary>
    public void RemovePartLayer(
        EntityUid partUid,
        VisualLayerKey layerId,
        BodyPartVisualizerComponent? partVis = null,
        SLBodyPartComponent? part = null)
    {
        if (!Resolve(partUid, ref partVis, logMissing: false))
            return;

        if (!partVis.BodyVisualLayers.Remove(layerId))
            return;

        Dirty(partUid, partVis);

        if (!Resolve(partUid, ref part, logMissing: false))
            return;

        if (!TryComp<BodyVisualizerComponent>(part.Body, out var bodyVis))
            return;

        RemoveLayer(part.Body, layerId, bodyVis);
    }

    /// <summary>
    /// Sets the sprite offset on the body visualizer.
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

    internal void SetLayer(
        EntityUid uid,
        VisualLayerKey layerId,
        ExtendedSpriteSpecifier specifier,
        BodyVisualizerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.LayerData.TryGetValue(layerId, out var existing)
            && existing.Equals(specifier))
            return;

        component.LayerData[layerId] = specifier;
        component.LayerKeys.Add(layerId);
        component.LayerModifiedTicks[layerId] = _timing.CurTick;
        Dirty(uid, component);
    }

    internal void RemoveLayer(
        EntityUid uid,
        VisualLayerKey layerId,
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
}
