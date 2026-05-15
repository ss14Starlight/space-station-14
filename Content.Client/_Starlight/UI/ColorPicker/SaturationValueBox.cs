// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.UI;

internal sealed class SaturationValueBox : ColorPickerSlider
{
    private readonly TextureRect _gradient;
    private readonly Control _marker;
    private float _hue = -1f;

    public SaturationValueBox(StarlightColorPicker owner) : base(owner)
    {
        _gradient = new TextureRect
        {
            Stretch = TextureRect.StretchMode.Scale,
            MouseFilter = MouseFilterMode.Ignore,
        };
        SetAnchorPreset(_gradient, LayoutPreset.Wide);
        AddChild(_gradient);

        ColorPickerStyles.BorderOverlay(this);

        var outerPanel = new PanelContainer
        {
            MinSize = new Vector2(12, 12),
            MouseFilter = MouseFilterMode.Ignore,
            PanelOverride = ColorPickerStyles.Border(Color.Black, thickness: 2),
        };
        var innerPanel = new PanelContainer
        {
            MouseFilter = MouseFilterMode.Ignore,
            PanelOverride = ColorPickerStyles.Border(Color.White),
        };
        SetAnchorPreset(innerPanel, LayoutPreset.Wide);
        outerPanel.AddChild(innerPanel);

        _marker = outerPanel;
        AddChild(_marker);
    }

    public void Invalidate() => _hue = -1f;

    public void UpdateFor(float hue, float saturation, float value)
    {
        if (Math.Abs(hue - _hue) > 0.0001f)
        {
            _hue = hue;
            _gradient.Texture = ColorPickerTextures.Sv(hue, Owner.AllowedPredicate);
        }

        var pos = new Vector2((saturation * Width) - 6f, ((1f - value) * Height) - 6f);
        SetPosition(_marker, pos);
    }

    protected override void HandleAt(Vector2 relative)
    {
        var s = Width > 0 ? Math.Clamp(relative.X / Width, 0f, 1f) : 0f;
        var v = Height > 0 ? 1f - Math.Clamp(relative.Y / Height, 0f, 1f) : 0f;
        Owner.OnSaturationValueChanged(s, v);
    }
}
