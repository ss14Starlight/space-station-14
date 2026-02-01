using Content.Shared.EntityEffects;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.EntityEffects.Effects;

/// <summary>
/// A type of <see cref="EntityEffectBase{T}"/> for effects that changes a target entity's species.
/// </summary>
/// <inheritdoc cref="EntityEffect"/>
public sealed partial class ChangeSpecies : EntityEffectBase<ChangeSpecies>
{
    /// <summary>
    /// Name of the species to change to
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SpeciesPrototype> Species = default!;
}