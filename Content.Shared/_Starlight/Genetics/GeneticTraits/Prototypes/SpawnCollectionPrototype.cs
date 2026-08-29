using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Genetics.GeneticTraits.Prototypes;

[Prototype("spawnCollection")]
public sealed partial class SpawnCollectionPrototype: IPrototype
{
    [ViewVariables, IdDataField]
    public string ID { get; private set; } = default!;

    [ViewVariables, DataField(required: true)]
    public List<SCPEntry> Items = default!;

    [ViewVariables, DataField(required: true)]
    public TimeSpan Cooldown = default!;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class SCPEntry
{
    [DataField]
    public EntProtoId Id;

    [DataField]
    public int Amount;

    public SCPEntry(EntProtoId id, int amount)
    {
        Id = id;
        Amount = amount;
    }
}
