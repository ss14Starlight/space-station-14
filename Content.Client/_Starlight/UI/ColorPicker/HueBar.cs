// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._Starlight.UI;

/// <summary>
/// Vertical rainbow strip with a horizontal marker bar.
/// </summary>
internal sealed class HueBar : ColorPickerSlider
{
    private readonly TextureRect _gradient;
    private readonly Control _marker;

    public HueBar(StarlightColorPicker owner, int width) : base(owner)
    {
        _gradient = new TextureRect
        {
            Texture = ColorPickerTextures.Hue(Owner.AllowedPredicate),
            Stretch = TextureRect.StretchMode.Scale,
            MouseFilter = MouseFilterMode.Ignore,
        };
        SetAnchorPreset(_gradient, LayoutPreset.Wide);
        AddChild(_gradient);

        ColorPickerStyles.BorderOverlay(this);

        _marker = new PanelContainer
        {
            MinSize = new Vector2(width + 4, 4),
            MouseFilter = MouseFilterMode.Ignore,
            PanelOverride = ColorPickerStyles.Filled(Color.White, Color.Black),
        };
        AddChild(_marker);
    }

    public void RefreshTexture() => _gradient.Texture = ColorPickerTextures.Hue(Owner.AllowedPredicate);

    public void UpdateFor(float hue)
    {
        var y = (hue * Height) - 2f;
        SetPosition(_marker, new Vector2(-2f, y));
    }

    protected override void HandleAt(Vector2 relative)
    {
        var hue = Height > 0 ? Math.Clamp(relative.Y / Height, 0f, 1f) : 0f;
        Owner.OnHueChanged(hue);
    }
}
