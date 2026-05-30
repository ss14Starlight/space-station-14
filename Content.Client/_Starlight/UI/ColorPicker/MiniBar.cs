// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._Starlight.UI;

/// <summary>
/// Horizontal mini slider showing a HSV channel sweep (saturation or value)
/// </summary>
internal sealed class MiniBar : ColorPickerSlider
{
    private const int BarHeight = 14;

    private readonly bool _isValue;
    private readonly TextureRect _gradient;
    private readonly Control _marker;

    private float _cachedFixedA = float.NaN;
    private float _cachedFixedB = float.NaN;
    private Func<Color, bool>? _cachedAllowed;

    public MiniBar(StarlightColorPicker owner, bool isValue) : base(owner)
    {
        _isValue = isValue;
        MinSize = new Vector2(60, BarHeight);

        _gradient = new TextureRect
        {
            Stretch = TextureRect.StretchMode.Scale,
            MouseFilter = MouseFilterMode.Ignore,
        };
        SetAnchorPreset(_gradient, LayoutPreset.Wide);
        AddChild(_gradient);

        ColorPickerStyles.BorderOverlay(this);

        _marker = new PanelContainer
        {
            MinSize = new Vector2(4, BarHeight + 4),
            MouseFilter = MouseFilterMode.Ignore,
            PanelOverride = ColorPickerStyles.Filled(Color.White, Color.Black),
        };
        AddChild(_marker);
    }

    public void Invalidate() => _cachedFixedA = float.NaN;

    public void UpdateFor(float hue, float saturation, float value)
    {
        var fixedA = hue;
        var fixedB = _isValue ? saturation : value;
        var allowed = Owner.AllowedPredicate;
        if (fixedA != _cachedFixedA || fixedB != _cachedFixedB || !ReferenceEquals(allowed, _cachedAllowed))
        {
            _cachedFixedA = fixedA;
            _cachedFixedB = fixedB;
            _cachedAllowed = allowed;
            _gradient.Texture = ColorPickerTextures.Channel(hue, saturation, value, _isValue, allowed);
        }

        var t = _isValue ? value : saturation;
        SetPosition(_marker, new Vector2((t * Width) - 2f, -2f));
    }

    protected override void HandleAt(Vector2 relative)
    {
        var t = Width > 0 ? Math.Clamp(relative.X / Width, 0f, 1f) : 0f;
        if (_isValue)
            Owner.OnValueChanged(t);
        else
            Owner.OnSaturationChanged(t);
    }
}
