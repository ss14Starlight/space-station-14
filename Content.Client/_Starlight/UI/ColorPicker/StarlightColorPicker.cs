// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.UI;

public sealed class StarlightColorPicker : BoxContainer
{
    private const int SvBoxSize = 150;
    private const int HueBarWidth = 16;
    private const int SwatchHeight = 14;
    private const int RecentColumns = 16;

    internal readonly RecentColorsSystem Recents;
    private readonly SaturationValueBox _svBox;
    private readonly HueBar _hueBar;
    private readonly PanelContainer _swatch;
    private readonly RecentPalette _recentPalette;
    private readonly MiniBar _saturationBar;
    private readonly MiniBar _valueBar;
    private readonly LineEdit _hexEdit;

    private float _hue;
    private float _saturation;
    private float _value = 1f;
    private float _alpha = 1f;
    private bool _suppressHexEvent;

    public event Action<Color>? OnColorChanged;

    public Func<Color, Color>? Constrain
    {
        get;
        set
        {
            field = value;
            AllowedPredicate = value is null ? null : IsColorAllowed;
            _svBox.Invalidate();
            _hueBar.RefreshTexture();
            _saturationBar.Invalidate();
            _valueBar.Invalidate();
            _recentPalette.Refresh();
            SyncFromState(raise: false);
        }
    }

    public Color Color
    {
        get => Color.FromHsv(new Vector4(_hue, _saturation, _value, _alpha));
        set
        {
            var hsv = Color.ToHsv(value);
            if (hsv.Y > 0)
                _hue = hsv.X;
            if (hsv.Z > 0)
                _saturation = hsv.Y;
            _value = hsv.Z;
            _alpha = hsv.W;
            SyncFromState(raise: false);
        }
    }

    internal int ActiveSlot { get; private set; } = -1;

    internal Func<Color, bool>? AllowedPredicate { get; private set; }

    public StarlightColorPicker()
    {
        Recents = IoCManager.Resolve<IEntityManager>().System<RecentColorsSystem>();

        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 8;

        _svBox = new SaturationValueBox(this)
        {
            SetSize = new Vector2(SvBoxSize, SvBoxSize),
            VerticalAlignment = VAlignment.Top,
            HorizontalAlignment = HAlignment.Left,
        };
        _hueBar = new HueBar(this, HueBarWidth)
        {
            SetSize = new Vector2(HueBarWidth, SvBoxSize),
            VerticalAlignment = VAlignment.Top,
            HorizontalAlignment = HAlignment.Left,
        };
        var left = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            VerticalAlignment = VAlignment.Top,
            Children = { _svBox, _hueBar },
        };
        AddChild(left);

        _swatch = new PanelContainer
        {
            MinSize = new Vector2(0, SwatchHeight),
            HorizontalExpand = true,
            PanelOverride = ColorPickerStyles.Filled(Color.White),
        };

        _saturationBar = new MiniBar(this, isValue: false) { HorizontalExpand = true };
        _valueBar = new MiniBar(this, isValue: true) { HorizontalExpand = true };

        _hexEdit = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = "#RRGGBB",
        };
        _hexEdit.OnTextEntered += _ => CommitHex();
        _hexEdit.OnFocusExit += _ => CommitHex();

        _recentPalette = new RecentPalette(this, RecentColumns);

        var right = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Top,
            Children =
            {
                _swatch,
                MakeLabeled("S", _saturationBar),
                MakeLabeled("V", _valueBar),
                _hexEdit,
                _recentPalette,
            },
        };
        AddChild(right);

        ClaimInitialSlot();
        SyncFromState(raise: false);
    }

    private void ClaimInitialSlot()
    {
        var current = Color;
        var slots = Recents.Slots;
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i] is { } existing && existing == current)
            {
                ActiveSlot = i;
                return;
            }
        }

        ActiveSlot = Recents.Reserve();
        if (ActiveSlot >= 0)
            Recents.SetSlot(ActiveSlot, current);
    }

    private static BoxContainer MakeLabeled(string label, Control bar) => new()
    {
        Orientation = LayoutOrientation.Horizontal,
        SeparationOverride = 4,
        HorizontalExpand = true,
        Children =
        {
            new Label { Text = label, MinSize = new Vector2(10, 0), VerticalAlignment = VAlignment.Center },
            bar,
        },
    };

    internal void OnSaturationValueChanged(float saturation, float value)
    {
        _saturation = Math.Clamp(saturation, 0f, 1f);
        _value = Math.Clamp(value, 0f, 1f);
        SyncFromState(raise: true);
    }

    internal void OnSaturationChanged(float saturation)
    {
        _saturation = Math.Clamp(saturation, 0f, 1f);
        SyncFromState(raise: true);
    }

    internal void OnValueChanged(float value)
    {
        _value = Math.Clamp(value, 0f, 1f);
        SyncFromState(raise: true);
    }

    internal void OnHueChanged(float hue)
    {
        _hue = Math.Clamp(hue, 0f, 1f);
        SyncFromState(raise: true);
    }

    internal void CommitToRecent() => PersistCurrent();

    public void ApplyExternalColor(Color color)
    {
        Color = color;
        OnColorChanged?.Invoke(Color);
        PersistCurrent();
    }

    internal void SelectSlot(int slot, Color? stored)
    {
        ActiveSlot = slot;
        if (stored is { } existing)
        {
            Color = existing;
            OnColorChanged?.Invoke(Color);
        }
        Recents.SetSlot(slot, Color);
        _recentPalette.Refresh();
    }

    private void PersistCurrent()
    {
        if (ActiveSlot < 0)
            return;
        Recents.SetSlot(ActiveSlot, Color);
    }

    internal bool IsColorAllowed(Color color)
    {
        if (Constrain is not { } c)
            return true;
        var clamped = c(color);
        return Math.Abs(clamped.R - color.R) < 0.005f
            && Math.Abs(clamped.G - color.G) < 0.005f
            && Math.Abs(clamped.B - color.B) < 0.005f;
    }

    private void CommitHex()
    {
        if (_suppressHexEvent)
            return;

        var parsed = Color.TryFromHex(_hexEdit.Text);
        if (parsed is not { } color)
        {
            UpdateHexText();
            return;
        }

        Color = color;
        OnColorChanged?.Invoke(Color);
        PersistCurrent();
    }

    private void SyncFromState(bool raise)
    {
        var picked = Color;
        if (Constrain is { } c)
        {
            var constrained = c(picked);
            if (constrained != picked)
            {
                var hsv = Color.ToHsv(constrained);
                if (hsv.Y > 0)
                    _hue = hsv.X;
                if (hsv.Z > 0)
                    _saturation = hsv.Y;
                _value = hsv.Z;
                _alpha = hsv.W;
                picked = constrained;
            }
        }

        _swatch.PanelOverride = ColorPickerStyles.Filled(picked);
        _svBox.UpdateFor(_hue, _saturation, _value);
        _hueBar.UpdateFor(_hue);
        _saturationBar.UpdateFor(_hue, _saturation, _value);
        _valueBar.UpdateFor(_hue, _saturation, _value);
        UpdateHexText();

        if (raise)
            OnColorChanged?.Invoke(picked);
    }

    private void UpdateHexText()
    {
        _suppressHexEvent = true;
        _hexEdit.Text = Color.ToHexNoAlpha();
        _suppressHexEvent = false;
    }

    internal void SyncMarkers()
    {
        _svBox.UpdateFor(_hue, _saturation, _value);
        _hueBar.UpdateFor(_hue);
        _saturationBar.UpdateFor(_hue, _saturation, _value);
        _valueBar.UpdateFor(_hue, _saturation, _value);
    }
}
