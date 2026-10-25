// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;
using Robust.Client.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Starlight.UI;

// After testing, generating textures for every color we could find, it took 1.5 MB.
// Yeah, that’s crappy, but Sandbox is cornering us.
internal static class ColorPickerTextures
{
    public const int GradientSize = 64;
    public const int HueHeight = 256;

    private const float DisallowedTint = 0.25f;

    public static Texture Sv(float hue, Func<Color, bool>? allowed)
    {
        var hueColor = Color.FromHsv(new Vector4(hue, 1f, 1f, 1f));

        using var image = new Image<Rgba32>(GradientSize, GradientSize);
        for (var y = 0; y < GradientSize; y++)
        {
            var v = 1f - (y / (float)(GradientSize - 1));
            for (var x = 0; x < GradientSize; x++)
            {
                var s = x / (float)(GradientSize - 1);
                var r = (1f - s + (s * hueColor.R)) * v;
                var g = (1f - s + (s * hueColor.G)) * v;
                var b = (1f - s + (s * hueColor.B)) * v;
                image[x, y] = ApplyMask(new Color(r, g, b, 1f), allowed);
            }
        }

        return Texture.LoadFromImage(image, "starlight-color-picker-sv");
    }

    public static Texture Hue(Func<Color, bool>? allowed)
    {
        using var image = new Image<Rgba32>(1, HueHeight);
        for (var y = 0; y < HueHeight; y++)
        {
            var hue = y / (float)(HueHeight - 1);
            image[0, y] = ApplyMask(Color.FromHsv(new Vector4(hue, 1f, 1f, 1f)), allowed);
        }

        return Texture.LoadFromImage(image, "starlight-color-picker-hue");
    }

    public static Texture Channel(float hue, float saturation, float value, bool sweepValue, Func<Color, bool>? allowed)
    {
        using var image = new Image<Rgba32>(GradientSize, 1);
        for (var x = 0; x < GradientSize; x++)
        {
            var t = x / (float)(GradientSize - 1);
            var c = sweepValue
                ? Color.FromHsv(new Vector4(hue, saturation, t, 1f))
                : Color.FromHsv(new Vector4(hue, t, value, 1f));
            image[x, 0] = ApplyMask(c, allowed);
        }

        return Texture.LoadFromImage(image, sweepValue ? "starlight-color-picker-v" : "starlight-color-picker-s");
    }

    private static Rgba32 ApplyMask(Color color, Func<Color, bool>? allowed)
    {
        var r = color.R;
        var g = color.G;
        var b = color.B;
        if (allowed != null && !allowed(color))
        {
            r *= DisallowedTint;
            g *= DisallowedTint;
            b *= DisallowedTint;
        }
        return new Rgba32(r, g, b, 1f);
    }
}
