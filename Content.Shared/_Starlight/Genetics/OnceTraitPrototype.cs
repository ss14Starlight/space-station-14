using Content.Shared._Starlight.Xenobiology;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Genetics;

/// <summary>
/// A trait that acts on an entity once it's been created or once the trait has been added.
/// </summary>
[Prototype]
public sealed partial class OnceTraitPrototype : AbstractTraitPrototype
{
    /// <summary>
    /// The entity effect that is called when the associated entity is created or has its genes updated.
    /// </summary>
    [DataField(required: true)]
    public ScaledEntityEffect OnAddedEffect = default!;

    /// <summary>
    /// The entity effect that is called when the associated entity has its genes updated. Used to make sure modifications done by a trait are consistently removed if new genes remove it.
    /// </summary>
    [DataField(required: true)]
    public ScaledEntityEffect OnRemovedEffect = default!;
}