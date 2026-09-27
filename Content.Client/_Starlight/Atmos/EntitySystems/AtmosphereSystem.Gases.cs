using Content.Shared.Atmos;

namespace Content.Client.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    public override bool IsMixtureModerator(GasMixture mixture, float epsilon = Atmospherics.Epsilon)
    {
        var tmp = new float[Atmospherics.AdjustedNumberOfGases];
        NumericsHelpers.Multiply(mixture.Moles, GasModeratorMask, tmp);
        return NumericsHelpers.HorizontalAdd(tmp) > epsilon;
    }
}
