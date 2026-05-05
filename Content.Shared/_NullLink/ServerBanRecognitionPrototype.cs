using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._NullLink;

[Prototype]
public sealed partial class ServerBanRecognitionPrototype : IPrototype, ISerializationHooks
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Dictionary<string, string[]> Recognition { get; set; } = [];

    void ISerializationHooks.AfterDeserialization()
    {
        foreach (var (key, values) in Recognition)
            if (values == null)
                throw new PrototypeLoadException(
                    $"Prototype '{ID}' of type '{nameof(ServerBanRecognitionPrototype)}' has invalid configuration: " +
                    $"The recognition entry '{key}' has a null array value. " +
                    $"To fix this, either specify an array (e.g., '{key}: []') or remove the '{key}' entry entirely.");
    }
}
