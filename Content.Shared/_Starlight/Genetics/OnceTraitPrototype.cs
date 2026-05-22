using Content.Shared._Starlight.Xenobiology;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Genetics;

/// <summary>
/// A trait that acts on an entity once it's been created or once the trait has been added.
/// NOTE: Because this acts only once, this does not need a system to manage it.
/// </summary>
[Prototype]
public sealed partial class OnceTraitPrototype : AbstractTraitPrototype
{
    /// <summary>
    /// The entity effect that is called when the associated entity is created or has its traits updated.
    /// </summary>
    [ViewVariables]
    [DataField(required: true)]
    public ScaledEntityEffect OnAddedEffect = default!;

    /// <summary>
    /// The entity effect that is called when the value associated with this trait is updated.
    /// If no effect is provided, then when the trait value is updated, onRemovedEffect will be applied then onAddedAffect.
    /// </summary>
    [ViewVariables]
    [DataField]
    public ScaledEntityEffect? OnUpdatedEffect = default!;

    /// <summary>
    /// The entity effect that is called when the associated entity has this trait removed.
    /// </summary>
    [ViewVariables]
    [DataField(required: true)]
    public ScaledEntityEffect OnRemovedEffect = default!;
}
