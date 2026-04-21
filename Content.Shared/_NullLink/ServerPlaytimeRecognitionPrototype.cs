using Robust.Shared.Prototypes;

namespace Content.Shared._NullLink;

[Prototype("serverPlaytimeRecognition")]
public sealed partial class ServerPlaytimeRecognitionPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public Dictionary<string, string[]> Recognition { get; set; } = [];
}
