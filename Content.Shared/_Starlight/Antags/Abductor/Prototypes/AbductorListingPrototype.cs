using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Antags.Abductor.Prototypes;

[Prototype("abductorListing")]
[DataDefinition]
public sealed partial class AbductorListingPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public EntProtoId ProductEntity;

    [DataField(required: true)]
    public int Cost;
}
