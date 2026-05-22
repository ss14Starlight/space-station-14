using Content.Shared._Starlight.Xenobiology;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Genetics;

/// <summary>
/// A trait that acts on an entity when its solution is updated.
/// </summary>
[Prototype, Serializable]
public sealed partial class OnSolutionChangedTraitPrototype : AbstractTraitPrototype
{
    /// <summary>
    /// What occurs when this entity receives some specific reagent.
    /// Each entry is a reagent reaction, consisting of the requirements and then the response
    /// </summary>
    [DataField]
    public List<ProtoId<ExtractReactionPrototype>> ExtractReactions = new();
}
