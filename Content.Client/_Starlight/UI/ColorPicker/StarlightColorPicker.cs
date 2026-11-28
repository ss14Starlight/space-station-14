// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Numerics;
using Content.Client._Starlight.UserInterface.Controls;
using Content.Shared._Starlight.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

// ReSharper disable CheckNamespace
namespace Content.Client._Starlight.UI;

/// <summary>
/// The color models the picker can show numbers for. The square and hue bar stay the same in every mode.
/// </summary>
public enum ColorPickerMode
{
    Rgb,
    Hsv,
    Hsl,
    Grayscale,
}

public sealed class StarlightColorPicker : BoxContainer
{
    private const int SvBoxSize = 150;
    private const int HueBarWidth = 16;
    private const int SwatchHeight = 14;
    private const int RecentColumns = 8;

    internal readonly RecentColorsSystem Recents;
    private readonly SaturationValueBox _svBox;
    private readonly HueBar _hueBar;
    private readonly PanelContainer _swatch;
    private readonly RecentPalette _recentPalette;
    private readonly LineEdit _hexEdit;
    private readonly IConfigurationManager _cfg;
    private readonly SegmentedButtons _modeButtons = new();
    private readonly BoxContainer _channels = new()
    {
        Orientation = LayoutOrientation.Vertical,
        SeparationOverride = 4,
        HorizontalExpand = true,
    };
    private readonly List<ChannelRow> _rows = [];
    private ColorPickerMode _mode;
    private bool _syncingRows;

    private sealed record ChannelRow(Slider Slider, LineEdit Edit, Func<float> Get, Action<float> Set);

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
        _cfg = IoCManager.Resolve<IConfigurationManager>();

        // Stacked, so it fits a side panel.
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;

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

        var savedMode = (ColorPickerMode) _cfg.GetCVar(StarlightCCVars.ColorPickerMode);
        _mode = Enum.IsDefined(savedMode) ? savedMode : ColorPickerMode.Hsv;
        _modeButtons.AddItem(Loc.GetString("starlight-color-picker-mode-rgb"), (int) ColorPickerMode.Rgb);
        _modeButtons.AddItem(Loc.GetString("starlight-color-picker-mode-hsv"), (int) ColorPickerMode.Hsv);
        _modeButtons.AddItem(Loc.GetString("starlight-color-picker-mode-hsl"), (int) ColorPickerMode.Hsl);
        _modeButtons.AddItem(Loc.GetString("starlight-color-picker-mode-grayscale"), (int) ColorPickerMode.Grayscale);
        _modeButtons.SelectId((int) _mode);
        _modeButtons.OnItemSelected += args =>
        {
            _mode = (ColorPickerMode) args.Id;
            _cfg.SetCVar(StarlightCCVars.ColorPickerMode, args.Id);
            BuildChannels();
        };

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
                _modeButtons,
                _channels,
                _hexEdit,
                _recentPalette,
            },
        };
        AddChild(right);

        ClaimInitialSlot();
        BuildChannels();
        SyncFromState(raise: false);
    }

    private void BuildChannels()
    {
        _channels.RemoveAllChildren();
        _rows.Clear();
        switch (_mode)
        {
            case ColorPickerMode.Rgb:
                AddChannel("R", 255, () => Color.R * 255, v => SetColor(Color with { R = v / 255 }));
                AddChannel("G", 255, () => Color.G * 255, v => SetColor(Color with { G = v / 255 }));
                AddChannel("B", 255, () => Color.B * 255, v => SetColor(Color with { B = v / 255 }));
                break;
            case ColorPickerMode.Hsv:
                AddChannel("H", 360, () => _hue * 360, v => OnHueChanged(v / 360));
                AddChannel("S", 100, () => _saturation * 100, v => OnSaturationChanged(v / 100));
                AddChannel("V", 100, () => _value * 100, v => OnValueChanged(v / 100));
                break;
            case ColorPickerMode.Hsl:
                // Hue comes from the picker's own state, so it survives grays where HSL has no hue.
                AddChannel("H", 360, () => _hue * 360, v => OnHueChanged(v / 360));
                AddChannel("S", 100, () => Color.ToHsl(Color).Y * 100, v => SetHsl(1, v / 100));
                AddChannel("L", 100, () => Color.ToHsl(Color).Z * 100, v => SetHsl(2, v / 100));
                break;
            case ColorPickerMode.Grayscale:
                AddChannel("L", 255, () => Luma(Color) * 255, v => SetColor(new Color(v / 255, v / 255, v / 255, _alpha)));
                break;
        }

        UpdateChannels();
    }

    private void AddChannel(string label, float max, Func<float> get, Action<float> set)
    {
        var slider = new Slider { MinValue = 0, MaxValue = max, HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
        var edit = new LineEdit { SetWidth = 44 };
        var row = new ChannelRow(slider, edit, get, set);

        slider.OnValueChanged += _ =>
        {
            if (!_syncingRows)
                set(slider.Value);
        };
        slider.OnReleased += _ => PersistCurrent();
        edit.OnTextEntered += _ => CommitChannel(row, max);
        edit.OnFocusExit += _ => CommitChannel(row, max);

        _rows.Add(row);
        _channels.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            Children =
            {
                new Label { Text = label, MinSize = new Vector2(12, 0), VerticalAlignment = VAlignment.Center },
                slider,
                edit,
            },
        });
    }

    private void CommitChannel(ChannelRow row, float max)
    {
        if (_syncingRows)
            return;

        if (float.TryParse(row.Edit.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            row.Set(Math.Clamp(value, 0, max));
            PersistCurrent();
        }

        UpdateChannels();
    }

    private void UpdateChannels()
    {
        _syncingRows = true;
        foreach (var row in _rows)
        {
            var value = row.Get();
            row.Slider.Value = value;
            if (!row.Edit.HasKeyboardFocus())
                row.Edit.Text = MathF.Round(value).ToString(CultureInfo.InvariantCulture);
        }
        _syncingRows = false;
    }

    private void SetHsl(int channel, float value)
    {
        var hsl = Color.ToHsl(Color);
        hsl.X = _hue;
        if (channel == 1)
            hsl.Y = Math.Clamp(value, 0f, 1f);
        else
            hsl.Z = Math.Clamp(value, 0f, 1f);
        SetColor(Color.FromHsl(hsl));
    }

    private static float Luma(Color color)
        => (0.299f * color.R) + (0.587f * color.G) + (0.114f * color.B);

    private void SetColor(Color color)
    {
        Color = color;
        OnColorChanged?.Invoke(Color);
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

    private void OnSaturationChanged(float saturation)
    {
        _saturation = Math.Clamp(saturation, 0f, 1f);
        SyncFromState(raise: true);
    }

    private void OnValueChanged(float value)
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

        if (!Color.TryFromHex(_hexEdit.Text, out var color))
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
        UpdateChannels();
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
        UpdateChannels();
    }
}
