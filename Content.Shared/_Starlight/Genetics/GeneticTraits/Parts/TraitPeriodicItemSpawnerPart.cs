using Content.Shared._Starlight.Genetics.GeneticTraits.Components;
using Content.Shared._Starlight.Genetics.GeneticTraits.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Genetics.GeneticTraits.Parts;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class TraitPeriodicItemSpawnerPart: IGeneticTraitSetup
{
    [ViewVariables, DataField(required: true)]
    public List<ProtoId<SpawnCollectionPrototype>> Collections = default!;

    public void GeneticTraitSetup(EntityManager entityManager, EntityUid entityUid) => entityManager.EnsureComponent<TraitPeriodicItemSpawnerComponent>(entityUid);
}
