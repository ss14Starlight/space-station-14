// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Shared.Humanoid.Markings;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using System.Numerics;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class BodyTab
{
    private const float MarkingPreviewScale = 3f;

    private Control BuildBodyMarkingPreview(MarkingPrototype prototype, Vector2 cellSize)
    {
        if (_store.State.SelectedPart == null)
            return BuildMarkingPreview(prototype, GetPreviewColor(prototype), MarkingPreviewScale);

        const float BaseSpriteSize = 32f;
        const float Padding = 8f;
        var scale = MathF.Max(1f, MathF.Floor((MathF.Min(cellSize.X, cellSize.Y) - Padding) / BaseSpriteSize));

        return BodyPreview.BuildSnapshot(
            _store.State.BodyProfile,
            scale,
            _store.State.SelectedPart.Path,
            prototype.MarkingCategory,
            prototype);
    }

    private Control BuildSelectedPreviewControl(MarkingPrototype marking, List<Color> colors)
    {
        var size = AvailableMarkingsGrid.ItemSize;
        var preview = BuildMarkingSpritesPreview(marking, colors, size);
        preview.HorizontalAlignment = HAlignment.Center;
        preview.VerticalAlignment = VAlignment.Center;
        preview.MouseFilter = MouseFilterMode.Ignore;
        return preview;
    }

    private Control BuildMarkingSpritesPreview(MarkingPrototype marking, List<Color> colors, Vector2 cellSize)
    {
        const float BaseSpriteSize = 32f;
        const float Padding = 8f;
        var scale = MathF.Max(1f, MathF.Floor((MathF.Min(cellSize.X, cellSize.Y) - Padding) / BaseSpriteSize));

        var stack = new LayoutContainer
        {
            VerticalAlignment = VAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
        };

        var maxW = 0f;
        var maxH = 0f;
        var spriteList = marking.Sprites.Values.ToList();
        for (var i = 0; i < spriteList.Count; i++)
        {
            var spec = spriteList[i].Sprite;
            var texture = _sprite.Frame0(spec);
            var size = new Vector2(texture.Width * scale, texture.Height * scale);
            maxW = MathF.Max(maxW, size.X);
            maxH = MathF.Max(maxH, size.Y);

            stack.AddChild(new TextureRect
            {
                Texture = texture,
                TextureScale = new Vector2(scale, scale),
                Stretch = TextureRect.StretchMode.KeepCentered,
                ModulateSelfOverride = i < colors.Count ? colors[i] : Color.White,
                MouseFilter = MouseFilterMode.Ignore,
                MinSize = size,
                SetSize = size,
            });
        }

        CenterChildren(stack, maxW, maxH);
        return stack;
    }

    private Control BuildMarkingPreview(MarkingPrototype marking, Color color, float scale)
    {
        var stack = new LayoutContainer
        {
            VerticalAlignment = VAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
        };

        var maxW = 0f;
        var maxH = 0f;
        foreach (var spec in marking.Sprites.Values)
        {
            var texture = _sprite.Frame0(spec.Sprite);
            var size = new Vector2(texture.Width * scale, texture.Height * scale);
            maxW = MathF.Max(maxW, size.X);
            maxH = MathF.Max(maxH, size.Y);

            stack.AddChild(new TextureRect
            {
                Texture = texture,
                TextureScale = new Vector2(scale, scale),
                Stretch = TextureRect.StretchMode.KeepCentered,
                ModulateSelfOverride = color,
                MouseFilter = MouseFilterMode.Ignore,
                MinSize = size,
                SetSize = size,
            });
        }

        CenterChildren(stack, maxW, maxH);
        return stack;
    }

    private static void CenterChildren(LayoutContainer stack, float width, float height)
    {
        stack.MinSize = new Vector2(width, height);
        stack.SetSize = new Vector2(width, height);

        foreach (var child in stack.Children)
        {
            LayoutContainer.SetPosition(child, new Vector2(
                (width - child.MinSize.X) * 0.5f,
                (height - child.MinSize.Y) * 0.5f));
        }
    }

    private Color GetPreviewColor(MarkingPrototype marking)
    {
        if (marking.FollowSkinColor && _store.State.Character.HasProfile)
            return _store.State.Character.SkinColor;

        return Color.White;
    }
}
