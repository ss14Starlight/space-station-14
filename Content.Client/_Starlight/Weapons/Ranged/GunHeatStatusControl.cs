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
        => (_fill, _jam) = GetFractions(heat);

    /// <summary>
    /// Calculates the fill and jam fractions for the heat gauge based on the current temperature, jam temperature, and melt temperature of the gun.
    /// </summary>
    public static (float Fill, float Jam) GetFractions(GunHeatComponent heat)
    {
        var range = heat.MeltTemperature - Atmospherics.T20C;
        if (range <= 0f)
            return (0f, 1f);

        return (Math.Clamp((heat.Temperature - Atmospherics.T20C) / range, 0f, 1f),
            Math.Clamp((heat.JamTemperature - Atmospherics.T20C) / range, 0f, 1f));
    }

    /// <summary>
    /// Draws the heat gauge on the screen.
    /// </summary>
    public static void DrawGauge(DrawingHandleScreen handle, UIBox2 box, float fill, float jam, float marker)
    {
        handle.DrawRect(box, _background);

        if (fill > 0f)
        {
            var color = fill < jam ? Color.InterpolateBetween(_cool, _hot, fill / MathF.Max(jam, 0.01f) * 0.5f) : _hot;
            handle.DrawRect(new UIBox2(box.Left + 1, box.Top + 1, box.Left + 1 + ((box.Width - 2) * fill), box.Bottom - 1), color);
        }

        var jamX = box.Left + ((box.Width - 2) * jam);
        handle.DrawRect(new UIBox2(jamX, box.Top, jamX + marker, box.Bottom), _jamMarker);
        handle.DrawRect(new UIBox2(box.Right - marker, box.Top, box.Right, box.Bottom), _meltMarker);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        DrawGauge(handle, new UIBox2(0, 0, PixelWidth, PixelHeight), _fill, _jam, MathF.Max(1f, UIScale));
    }
}
