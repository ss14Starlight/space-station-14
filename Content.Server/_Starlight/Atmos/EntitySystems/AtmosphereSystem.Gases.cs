using Content.Shared.Atmos;
using Content.Shared.Atmos.EntitySystems;

// ReSharper disable once CheckNamespace
namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    public override bool IsMixtureModerator(GasMixture mixture, float epsilon = Atmospherics.Epsilon)
    {
        Span<float> tmp = stackalloc float[Atmospherics.AdjustedNumberOfGases];
        NumericsHelpers.Multiply(mixture.Moles, GasModeratorMask, tmp);
        return NumericsHelpers.HorizontalAdd(tmp) > epsilon;
    }
}
