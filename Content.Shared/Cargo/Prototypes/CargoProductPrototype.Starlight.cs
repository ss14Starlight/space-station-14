using Content.Shared.Atmos;
using Content.Shared.Atmos.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Cargo.Prototypes;

public sealed partial class CargoProductPrototype : IPrototype, IInheritingPrototype
{
    /// <summary>
    ///     The type of gas purchased, if any.
    /// </summary>
    [DataField]
    public ProtoId<GasPrototype>? GasType { get; private set; }

    /// <summary>
    ///     The amount of moles purchased.
    /// </summary>
    [DataField]
    public float GasMoles { get; private set; }

    /// <summary>
    ///     The temperature the moles will have when spawned.
    /// </summary>
    [DataField]
    public float GasTemperature { get; private set; } = Atmospherics.T20C;
}
