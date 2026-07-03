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
    /// What occurs when this entity receives some solution recipe. The recipe is specified in the extract reaction
    /// itself.
    /// </summary>
    [ViewVariables]
    [DataField(required: true)]
    public ProtoId<ExtractReactionPrototype> ExtractReaction = new();
}
