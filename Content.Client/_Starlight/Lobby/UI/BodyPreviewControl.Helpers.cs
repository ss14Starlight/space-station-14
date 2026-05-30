// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Shared._Starlight.Body.Prototypes;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class BodyPreviewControl
{
    private List<PreviewLayer> SortLayers(List<PreviewLayer> layers)
        => [.. layers
            .Select((layer, index) => (layer, index))
            .OrderBy(t => t, Comparer<(PreviewLayer layer, int index)>.Create((a, b) =>
            {
                var cmp = _visualLayers.CompareLayers(a.layer.LayerId, b.layer.LayerId);
                return cmp != 0 ? cmp : a.index.CompareTo(b.index);
            }))
            .Select(t => t.layer)];

    private static void CenterChildren(LayoutContainer container)
    {
        var width = 0f;
        var height = 0f;
        foreach (var child in container.Children)
        {
            width = MathF.Max(width, child.MinSize.X);
            height = MathF.Max(height, child.MinSize.Y);
        }

        container.MinSize = new Vector2(width, height);
        container.SetSize = new Vector2(width, height);

        foreach (var child in container.Children)
        {
            LayoutContainer.SetPosition(child, new Vector2(
                (width - child.MinSize.X) * 0.5f,
                (height - child.MinSize.Y) * 0.5f));
        }
    }

    private Robust.Client.Graphics.Texture GetTexture(SpriteSpecifier sprite)
    {
        if (_sprite == null)
            throw new InvalidOperationException("Body preview is not initialized.");

        var state = _sprite.RsiStateLike(sprite);
        return state.RsiDirections == RsiDirectionType.Dir1
            ? state.Default
            : state.GetFrame(_store?.State.Direction ?? RsiDirection.South, 0);
    }

    private RsiDirection? GetDirection(SpriteSpecifier sprite)
    {
        if (_sprite == null)
            throw new InvalidOperationException("Body preview is not initialized.");

        return _sprite.RsiStateLike(sprite).RsiDirections == RsiDirectionType.Dir1
            ? null
            : _store?.State.Direction ?? RsiDirection.South;
    }

    private static string GetVisualSocketId(string slotId, string? parentSocket)
        => slotId switch
        {
            "Hand" when parentSocket is "LeftArm" or "RightArm" => parentSocket,
            "Foot" when parentSocket is "LeftLeg" or "RightLeg" => parentSocket,
            _ => slotId,
        };

    private Color GetLayerColor(PreviewLayer layer)
    {
        if (layer.ColorSource is not { } source)
            return layer.SpriteColor;

        var profile = _store?.State.BodyProfile;
        if (profile != null && profile.Parameters.TryGetValue(source, out var color))
            return color;

        var lookup = source;
        var sepIdx = source.Id.IndexOf(ColorAppearanceParameterPrototype.PerInstanceSeparator);
        if (sepIdx >= 0)
            lookup = new ProtoId<ColorAppearanceParameterPrototype>(source.Id[..sepIdx]);

        return _prototype != null && _prototype.TryIndex(lookup, out var proto) ? proto.DefaultColor : layer.SpriteColor;
    }

    private ProtoId<ColorAppearanceParameterPrototype> ResolvePerInstanceColorKey(
        ProtoId<ColorAppearanceParameterPrototype> source,
        string markingId,
        VisualLayerKey layerKey)
    {
        if (_prototype != null && _prototype.TryIndex(source, out var proto))
        {
            var address = $"{markingId}{ColorAppearanceParameterPrototype.PerInstanceSeparator}{layerKey}";
            return ColorAppearanceParameterPrototype.ResolveKey(source, proto, address);
        }
        return source;
    }
}
