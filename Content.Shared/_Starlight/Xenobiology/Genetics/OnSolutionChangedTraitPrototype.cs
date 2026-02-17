using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Xenobiology.Genetics;

/// <summary>
/// A trait that acts on a slime extract when its solution is updated.
/// You can use this to do those extract reactions, or even cause some effect on the solution changing whenever.
/// </summary>
[Prototype]
public sealed partial class OnSolutionChangedTraitPrototype : AbstractXenobiologyTraitPrototype
{
    /// <summary>
    /// What occurs when this extract receives some specific reagent.
    /// Each entry is a reagent reaction, consisting of the requirements and then the response
    /// </summary>
    [DataField]
    public List<ProtoId<ExtractReactionPrototype>> ExtractReactions = new();
}