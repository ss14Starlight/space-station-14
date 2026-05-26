// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Editor;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared.Humanoid.Markings;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using System.Numerics;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class BodyPreviewControl
{
    public Control BuildSnapshot(
        BodyProfile profile,
        float scale,
        BodyPartAddress? hidePath = null,
        MarkingCategories? hideCategory = null,
        MarkingPrototype? extraMarking = null)
    {
        var canvas = new LayoutContainer
        {
            MouseFilter = MouseFilterMode.Ignore,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };

        if (_prototype == null || _sprite == null)
            return canvas;

        if (!_prototype.TryIndex<BodyPrefabPrototype>(GetBodyPrefab(), out var bodyPrefab))
            return canvas;

        var layers = new List<PreviewLayer>();
        AddPart(bodyPrefab.Root, profile.Root, "root", new BodyPartAddress("/root"), null, 0, layers, hidePath, hideCategory, extraMarking);

        foreach (var layer in SortLayers(layers))
        {
            var texture = GetTexture(layer.Sprite);
            var size = new Vector2(texture.Width * scale, texture.Height * scale);
            canvas.AddChild(new TextureRect
            {
                Texture = texture,
                TextureScale = new Vector2(scale, scale),
                Stretch = TextureRect.StretchMode.KeepCentered,
                ModulateSelfOverride = GetLayerColor(layer),
                MouseFilter = MouseFilterMode.Ignore,
                MinSize = size,
                SetSize = size,
            });
        }

        CenterChildren(canvas);
        return canvas;
    }
}
