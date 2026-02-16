using Content.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Power.BluespaceHarvester;

[Prototype]
public sealed partial class BluespaceHarvesterPoolPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
    [DataField(required: true)]
    public string Name { get; private set; } = default!;

    [DataField]
    public bool Enabled { get; private set; } = true;

    [DataField]
    public int Cost { get; private set; }

    [DataField]
    public int Order { get; private set; }

    [DataField(required: true)]
    public ProtoId<WeightedRandomEntityPrototype> LootTable { get; private set; } = default!;
}
