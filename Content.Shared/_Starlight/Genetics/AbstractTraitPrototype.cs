using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Genetics;

/// <summary>
/// The general trait that all traits inherit from. Makes some things less repetitive.
/// </summary>
[Prototype]
public abstract partial class AbstractTraitPrototype: IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The name of the trait. Shown in the gene analyzer. KEEP IT SHORT AND MEANINGFUL.
    /// </summary>
    [ViewVariables]
    [DataField]
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// The description of the trait. Shown in the gene analyzer.
    /// </summary>
    [ViewVariables]
    [DataField]
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// The minimum value of a trait before it is activated. Can be negative.
    /// If not present, will always be active.
    /// </summary>
    [ViewVariables]
    [DataField]
    public FixedPoint2? Threshold = null;

    /// <summary>
    /// Denotes the trait class of this trait. If a GenesComponent holds a class A, it can apply any trait with class
    /// A. If a trait does not have class A, it may be part of the genome but will not be applied. This is needed since
    /// a trait may be part of a genome WITHOUT being necessarily applied to an entity. For instance, slimes and slime
    /// extracts both have passive traits, but for balance, you want only some passive traits on a slime and other
    /// passive traits on an extract. This can be used for other purposes, like splicing slime genes into a plant.
    /// Note that a trait without a class cannot be applied to any entities, so best be attaching classes!
    /// </summary>
    [ViewVariables]
    [DataField]
    public HashSet<string> Classes = new HashSet<string>();
}
