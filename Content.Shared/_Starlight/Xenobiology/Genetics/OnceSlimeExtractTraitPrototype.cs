using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Xenobiology.Genetics;

/// <summary>
/// A trait that acts on a slime when the slime is created or when its genes are updated.
/// You can use this to add or update components belonging to a slime.
/// Prefer this to using the passive trait for the sake of performance.
/// </summary>
[Prototype]
public sealed partial class OnceSlimeExtractTraitPrototype : AbstractXenobiologyTraitPrototype
{
    /// <summary>
    /// The entity effect that is called when the associated slime extract is created or has its genes updated.
    /// </summary>
    [DataField(required: true)]
    public ScaledEntityEffect OnAddedEffect = default!;

    /// <summary>
    /// The entity effect that is called when the associated slime extract has its genes updated. Used to make sure modifications done by a trait are consistently removed if new genes remove it.
    /// When would an extract have its genes updated? I don't know.
    /// </summary>
    [DataField(required: true)]
    public ScaledEntityEffect OnRemovedEffect = default!;
}