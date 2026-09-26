// ReSharper disable once CheckNamespace
namespace Content.Shared.Humanoid;

public static class EyeColor
{
    public const float ShadekinBrightness = 0.251f;
    public const float MinSawianBrightness = 1f;
    public const float BrighteyeBrightness = 1;

    public static bool VerifyBrighteye(Color color)
    {
        var colorHsv = Color.ToHsv(color);

        if (colorHsv.Z < BrighteyeBrightness)
            return false;

        return true;
    }

    public static Color MakeBrighteyeValid(Color color)
    {
        var hsv = Color.ToHsv(color);

        hsv.Z = BrighteyeBrightness;

        return Color.FromHsv(hsv);
    }

    public static bool VerifyShadekin(Color color)
    {
        var colorHsv = Color.ToHsv(color);

        if (colorHsv.Z > ShadekinBrightness)
            return false;

        return true;
    }

    public static Color MakeShadekinValid(Color color)
    {
        var hsv = Color.ToHsv(color);

        hsv.Z = Math.Clamp(hsv.Z, 0, ShadekinBrightness);

        return Color.FromHsv(hsv);
    }

    public static bool VerifyFullWhite(Color color)
    {
        return color == Color.White;
    }

    public static Color MakeFullWhiteValid(Color color)
    {
        return Color.White;
    }

    public static bool VerifySawianColor(Color color, bool? glow) =>
        color.R >= MinSawianBrightness &&
        color.G >= MinSawianBrightness &&
        color.B >= MinSawianBrightness &&
        (glow ?? false);

    public static Color ClosestSawianColor(Color color) =>
        new( MathF.Max(color.R, MinSawianBrightness),
            MathF.Max(color.G, MinSawianBrightness),
            MathF.Max(color.B, MinSawianBrightness));

    public static bool VerifyEyeColor(HumanoidEyeColor type, Color color, bool? glow = null)
    {
        return type switch
        {
            HumanoidEyeColor.Shadekin => VerifyShadekin(color),
            HumanoidEyeColor.FullWhite => VerifyFullWhite(color),
            HumanoidEyeColor.Sawian => VerifySawianColor(color, glow),
            _ => false,
        };
    }

    public static Color ValidEyeColor(HumanoidEyeColor type, Color color)
    {
        return type switch
        {
            HumanoidEyeColor.Shadekin => MakeShadekinValid(color),
            HumanoidEyeColor.FullWhite => MakeFullWhiteValid(color),
            HumanoidEyeColor.Sawian => ClosestSawianColor(color),
            _ => color
        };
    }

    public static bool? ValidEyeGlow(HumanoidEyeColor type, bool? glow)
    {
        return type switch
        {
            HumanoidEyeColor.Sawian => true,
            _ => glow
        };
    }
}

public enum HumanoidEyeColor : byte
{
    Standard,
    Shadekin,
    FullWhite,
    Sawian,
}

[ByRefEvent]
public record struct EyeColorInitEvent();
