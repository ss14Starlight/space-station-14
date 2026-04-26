using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.NanoChat;

[Prototype]
public sealed partial class NanoChatAdvertisementPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The sender name that will appear in the advertisement message.
    /// </summary>
    [DataField(required: true)]
    public string SenderName = "Unknown";

    /// <summary>
    /// The localization key for the advertisement message content.
    /// </summary>
    [DataField(required: true)]
    public string MessageKey = string.Empty;

    [DataField]
    public float Weight = 1.0f;

    /// <summary>
    /// Fixed sender number for the advertisement. (Use 9000-9999)
    /// If not set, will be auto-assigned based on prototype ID hash.
    /// </summary>
    [DataField]
    public uint? SenderNumber;
}
