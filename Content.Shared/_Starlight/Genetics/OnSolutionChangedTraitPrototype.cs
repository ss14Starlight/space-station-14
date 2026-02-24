using Content.Shared._Starlight.Xenobiology;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Genetics;

/// <summary>
/// A trait that acts on an entity when its solution is updated.
/// </summary>
[Prototype]
public sealed partial class OnSolutionChangedTraitPrototype : AbstractTraitPrototype
{
    /// <summary>
    /// What occurs when this entity receives some specific reagent.
    /// Each entry is a reagent reaction, consisting of the requirements and then the response
    /// </summary>
    [DataField]
    public List<ProtoId<ExtractReactionPrototype>> ExtractReactions = new();
    
    /// <summary>
    /// The name of the container that holds the solution.
    /// Needed so that the solution changing can communicate with the container itself.
    /// </summary>
    [DataField("containerName", required: true)]
    public string ContainerName = string.Empty;

    /// <summary>
    /// How many times the solution can be changed before being deleted or exhausted.
    /// If null, there is no limit.
    /// </summary>
    [DataField("useLimit")]
    public int? UseLimit;
}