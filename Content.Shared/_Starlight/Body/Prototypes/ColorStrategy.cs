// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;
using JetBrains.Annotations;

namespace Content.Shared._Starlight.Body.Prototypes;

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class ColorStrategy
{
    public abstract bool IsAllowed(Color color);

    public abstract Color Clamp(Color color);
}

public sealed partial class ClampedHsvColorStrategy : ColorStrategy
{
    [DataField] public Vector2? Hue;
    [DataField] public Vector2? Saturation;
    [DataField] public Vector2? Value;

    public override bool IsAllowed(Color color)
    {
        var hsv = Color.ToHsv(color);
        return InRange(Hue, hsv.X)
            && InRange(Saturation, hsv.Y)
            && InRange(Value, hsv.Z);
    }

    public override Color Clamp(Color color)
    {
        var hsv = Color.ToHsv(color);
        if (Hue is { } h)
            hsv.X = Math.Clamp(hsv.X, h.X, h.Y);
        if (Saturation is { } s)
            hsv.Y = Math.Clamp(hsv.Y, s.X, s.Y);
        if (Value is { } v)
            hsv.Z = Math.Clamp(hsv.Z, v.X, v.Y);
        return Color.FromHsv(hsv);
    }

    private static bool InRange(Vector2? range, float value)
        => range is not { } r || (value >= r.X && value <= r.Y);
}
