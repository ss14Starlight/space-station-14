using Robust.Shared.Prototypes;

namespace Content.Shared._Blimpuf.Contraband;

/// <summary>
/// Defines a contraband severity tier and its examine presentation.
/// </summary>
[Prototype]
public sealed partial class ContrabandTierPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Numeric tier displayed to players.
    /// </summary>
    [DataField(required: true)]
    public int Level;

    /// <summary>
    /// Localized danger descriptor inserted before the tier number.
    /// </summary>
    [DataField(required: true)]
    public LocId Descriptor;

    /// <summary>
    /// Markup color used for the contraband classification sentence.
    /// </summary>
    [DataField(required: true)]
    public Color Color;
}
