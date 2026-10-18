using Robust.Shared.Prototypes;

namespace Content.Shared._Blimpuf.Contraband;

/// <summary>
/// Defines an optional origin or category appended to a contraband tier.
/// </summary>
[Prototype]
public sealed partial class ContrabandTypePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Localized name inserted into the contraband classification.
    /// </summary>
    [DataField(required: true)]
    public LocId Name;
}
