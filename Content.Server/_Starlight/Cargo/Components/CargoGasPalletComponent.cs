using Content.Server.Cargo.Components;
using Content.Shared.Atmos;

namespace Content.Server._Starlight.Cargo.Components;

/// <summary>
/// Takes input gas and stores it for sale
/// </summary>
[RegisterComponent]
public sealed partial class CargoGasPalletComponent : Component, IGasMixtureHolder
{
    /// <summary>
    /// The name of the pipe node
    /// </summary>
    [ViewVariables]
    [DataField("pipeNode")]
    public string PipeNodeName { get; set; } = "pipe";

    /// <summary>
    /// A gas mixture representing the remote resivoir.
    /// </summary>
    [DataField("gasMixture")]
    public GasMixture Air { get; set; } = new();

    /// <summary>
    /// The maximum pressure to which this will accept and/or output gasses
    /// </summary>
    [DataField]
    public float MaxPressure { get; set; } = Atmospherics.MaxOutputPressure;

    /// <summary>
    /// Whether this is a buying, selling, or both type.
    /// </summary>
    [DataField]
    public BuySellType PalletType = BuySellType.Sell;
}
