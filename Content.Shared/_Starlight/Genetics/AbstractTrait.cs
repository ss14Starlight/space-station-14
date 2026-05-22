using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Genetics;

/// <summary>
/// The general trait that all traits inherit from. Makes some things less repetitive.
/// </summary>
[Serializable, NetSerializable]
[DataDefinition]
public abstract partial class AbstractTrait
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The name of the trait. Shown in the gene analyzer. KEEP IT SHORT AND MEANINGFUL.
    /// </summary>
    [DataField]
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// The description of the trait. Shown in the gene analyzer.
    /// </summary>
    [DataField]
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// The minimum value of a trait before it is activated. Can be negative.
    /// If not present, will always be active.
    /// </summary>
    [DataField]
    public FixedPoint2? Threshold = null;
}
