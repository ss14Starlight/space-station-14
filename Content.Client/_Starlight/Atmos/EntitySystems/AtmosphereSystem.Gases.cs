using Content.Shared.Atmos;

// ReSharper disable once CheckNamespace
namespace Content.Client.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    public override bool IsMixtureModerator(GasMixture mixture)
    {
        var tmp = new float[Atmospherics.AdjustedNumberOfGases];
        NumericsHelpers.Multiply(mixture.Moles, GasModeratorMask, tmp);
        return NumericsHelpers.HorizontalAdd(tmp) > 5.0f;
    }
}
