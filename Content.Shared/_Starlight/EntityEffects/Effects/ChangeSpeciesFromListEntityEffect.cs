using Content.Shared.EntityEffects;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.EntityEffects.Effects;

/// <summary>
/// A type of <see cref="EntityEffectBase{T}"/> for effects that changes a target entity's species from a table.
/// </summary>
/// <inheritdoc cref="EntityEffect"/>
public sealed partial class ChangeSpeciesFromList : EntityEffectBase<ChangeSpeciesFromList>
{
    /// <summary>
    /// Table from which we're pulling the species name
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<SpeciesPrototype>> SpeciesList = default!;
}
