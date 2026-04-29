using Robust.Shared.Prototypes;

namespace Content.Shared._NullLink;

[Prototype]
public sealed partial class ServerBanRecognitionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public Dictionary<string, string[]> Recognition { get; set; } = [];
}
