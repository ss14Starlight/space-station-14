using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// The strain a batch of antipathogen serum was cultured against, carried by the liquid
/// itself rather than by whatever is holding it.
///
/// This is what makes the cure behave like an ordinary chemical: draw it into a syringe,
/// split it between beakers, load a hypospray - the serum keeps knowing what it treats,
/// because the strain rides in the reagent the same way DNA rides in blood.
/// </summary>
[ImplicitDataDefinitionForInheritors, Serializable, NetSerializable]
public sealed partial class PathogenCureData : ReagentData
{
    [DataField]
    public int Strain;

    public override ReagentData Clone()
    {
        return new PathogenCureData
        {
            Strain = Strain,
        };
    }

    public override bool Equals(ReagentData? other)
    {
        return other is PathogenCureData data && data.Strain == Strain;
    }

    public override int GetHashCode()
    {
        return Strain.GetHashCode();
    }
}
