// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Server.Administration.Systems;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Editor;
using Content.Shared._Starlight.Body.Preferences;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Systems;

public sealed class BodyPartVisualizerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MarkingManager _markings = default!;
    [Dependency] private readonly StarlightEntitySystem _sl = default!;

    public void BakeAppearance(
        Entity<SLBodyComponent> body,
        Entity<SLBodyPartComponent> part,
        BodyPartAddress address)
    {
        if (!_sl.TryEntity<SLBodyComponent, BodyVisualizerComponent>(body, out var bodyVis))
            return;

        if (!_sl.TryEntity<SLBodyPartComponent, BodyPartVisualizerComponent>(part, out var partVis))
            return;

        var appearance = bodyVis.Comp2.Appearance;
        if (appearance == null)
            return;

        if (TryGetPreference(appearance.Root, address, out var preference))
            BakeMarkings(partVis.Comp2, preference);

        foreach (var layer in partVis.Comp2.BodyVisualLayers)
            partVis.Comp2.BodyVisualLayers[layer.Key] = WithColor(layer.Value, appearance, address);

        Dirty(part, partVis.Comp2);
    }

    private void BakeMarkings(BodyPartVisualizerComponent partVis, BodyPartPreference preference)
    {
        foreach (var marking in preference.Markings)
        {
            if (!marking.Visible || !_markings.TryGetMarking(marking, out var prototype))
                continue;

            var colorIndex = 0;
            foreach (var (key, sprite) in prototype.Sprites)
            {
                var baked = new BodySpriteSpecifier
                {
                    Sprite = sprite.Sprite,
                    SpriteColor = colorIndex < marking.MarkingColors.Count ? marking.MarkingColors[colorIndex] : sprite.SpriteColor,
                    SpriteScale = sprite.SpriteScale,
                    SpriteRotation = sprite.SpriteRotation,
                    Offset = sprite.Offset,
                    ColorSource = sprite.ColorSource,
                };
                partVis.BodyVisualLayers[GetMarkingLayerKey(partVis, key)] = baked;
                colorIndex++;
            }
        }
    }

    private static VisualLayerKey GetMarkingLayerKey(BodyPartVisualizerComponent partVis, VisualLayerKey key)
    {
        if (!partVis.BodyVisualLayers.ContainsKey(key))
            return key;

        var index = key.Index ?? 1;
        VisualLayerKey candidate;
        do
        {
            index++;
            candidate = new VisualLayerKey(key.Layer, index, key.Displacement);
        } while (partVis.BodyVisualLayers.ContainsKey(candidate));

        return candidate;
    }

    private BodySpriteSpecifier WithColor(BodySpriteSpecifier value, BodyProfile appearance, BodyPartAddress address)
    {
        var color = GetColor(value, appearance, address);
        if (color == value.SpriteColor)
            return value;

        return new BodySpriteSpecifier
        {
            Sprite = value.Sprite,
            SpriteColor = color,
            SpriteScale = value.SpriteScale,
            SpriteRotation = value.SpriteRotation,
            Offset = value.Offset,
            ColorSource = value.ColorSource,
        };
    }

    private Color GetColor(BodySpriteSpecifier value, BodyProfile appearance, BodyPartAddress address)
    {
        if (value.ColorSource is not { } source)
            return value.SpriteColor;

        if (_proto.TryIndex(source, out ColorAppearanceParameterPrototype? proto))
        {
            var key = ColorAppearanceParameterPrototype.ResolveKey(source, proto, address.ToString());
            if (appearance.Parameters.TryGetValue(key, out var color))
                return color;
            return proto.DefaultColor;
        }

        return appearance.Parameters.TryGetValue(source, out var fallback) ? fallback : value.SpriteColor;
    }

    private static bool TryGetPreference(BodyPartPreference root, BodyPartAddress address, out BodyPartPreference preference)
    {
        preference = root;
        foreach (var segment in address.Segments)
        {
            if (!preference.Children.TryGetValue(segment, out var child))
                return false;
            preference = child;
        }
        return true;
    }
}
