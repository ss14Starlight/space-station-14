using Content.Shared._Starlight.Weapons.Ranged.Components;
using Content.Shared.Atmos;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Weapons.Ranged;

public sealed class GunHeatStatusControl : Control
{
    private static readonly Color _background = Color.Black.WithAlpha(0.45f);
    private static readonly Color _cool = Color.FromHex("#e0b050");
    private static readonly Color _hot = Color.FromHex("#ff3b1f");
    private static readonly Color _jamMarker = Color.FromHex("#ffd34d");
    private static readonly Color _meltMarker = Color.FromHex("#ff4d4d");

    private float _fill;
    private float _jam;

    public GunHeatStatusControl()
    {
        MinHeight = 7;
        HorizontalExpand = true;
        Margin = new Thickness(0, 2, 0, 0);
    }

    public void Update(GunHeatComponent heat)
    {
        var range = heat.MeltTemperature - Atmospherics.T20C;
        if (range <= 0f)
            return;

        _fill = Math.Clamp((heat.Temperature - Atmospherics.T20C) / range, 0f, 1f);
        _jam = Math.Clamp((heat.JamTemperature - Atmospherics.T20C) / range, 0f, 1f);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var width = PixelWidth;
        var height = PixelHeight;
        handle.DrawRect(new UIBox2(0, 0, width, height), _background);

        if (_fill > 0f)
        {
            var color = _fill < _jam ? Color.InterpolateBetween(_cool, _hot, _fill / MathF.Max(_jam, 0.01f) * 0.5f) : _hot;
            handle.DrawRect(new UIBox2(1, 1, 1 + ((width - 2) * _fill), height - 1), color);
        }

        var marker = MathF.Max(1f, UIScale);
        var jamX = (width - 2) * _jam;
        handle.DrawRect(new UIBox2(jamX, 0, jamX + marker, height), _jamMarker);
        handle.DrawRect(new UIBox2(width - marker, 0, width, height), _meltMarker);
    }
}
