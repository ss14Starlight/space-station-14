using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._NullLink;

[Prototype]
public sealed partial class AdminRankBuilderPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    // First matching entry wins.
    [DataField]
    public List<AdminRankMapping> Ranks = [];
}

[DataDefinition]
public sealed partial class AdminRankMapping
{
    [DataField(required: true)]
    public string Name = "";

    [DataField(required: true)]
    public ulong[] Roles = [];

    [DataField(required: true)]
    public string[] Flags = [];
}
