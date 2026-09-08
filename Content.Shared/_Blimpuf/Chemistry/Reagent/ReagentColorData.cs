using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Blimpuf.Chemistry.Reagent;

[ImplicitDataDefinitionForInheritors, Serializable, NetSerializable]
public sealed partial class ReagentColorData : ReagentData
{
    [DataField]
    public Color Color = Color.White;

    public override ReagentData Clone()
    {
        return new ReagentColorData
        {
            Color = Color,
        };
    }

    public override bool Equals(ReagentData? other)
    {
        return other is ReagentColorData colorData && colorData.Color == Color;
    }

    public override int GetHashCode()
    {
        return Color.GetHashCode();
    }

    public override string ToString(string prototype, FixedPoint2 quantity)
    {
        return $"{prototype}:{GetType().Name}:{Color.ToHex()}:{quantity}";
    }

    public override string ToString(string prototype)
    {
        return $"{prototype}:{GetType().Name}:{Color.ToHex()}";
    }
}
