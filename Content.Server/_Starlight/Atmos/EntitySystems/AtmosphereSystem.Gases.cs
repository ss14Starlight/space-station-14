using Content.Shared.Atmos;
using Content.Shared.Atmos.EntitySystems;

// ReSharper disable once CheckNamespace
namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    public override bool IsMixtureModerator(GasMixture mixture)
    {
        Span<float> tmp = stackalloc float[Atmospherics.AdjustedNumberOfGases];
        NumericsHelpers.Multiply(mixture.Moles, GasModeratorMask, tmp);
        return NumericsHelpers.HorizontalAdd(tmp) > 5.0f;
    }
}
