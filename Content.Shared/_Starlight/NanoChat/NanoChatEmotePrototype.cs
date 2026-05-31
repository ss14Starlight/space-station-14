using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.NanoChat;

/// <summary>
/// Prototype definition for NanoChat emote icons.
/// Emotes can be used in chat messages using :emotename: syntax.
/// </summary>
[Prototype]
public sealed partial class NanoChatEmotePrototype : IPrototype
{
    /// <summary>
    /// Unique identifier for this emote (e.g., "godo", "honk")
    /// Used in :emotename: syntax in messages.
    /// </summary>
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// Display name for the emote shown in tooltips.
    /// If not provided, uses the ID.
    /// </summary>
    [DataField]
    public string? DisplayName { get; private set; }

    /// <summary>
    /// Category for organizing emotes in the selector.
    /// Common categories: "Reactions", "Characters", "Plushies", "Custom"
    /// </summary>
    [DataField]
    public string Category { get; private set; } = "Misc";

    /// <summary>
    /// Tags for improved search functionality.
    /// Example: ["happy", "joy"] for a smile emote
    /// </summary>
    [DataField]
    public List<string> SearchTags { get; private set; } = [];

    /// <summary>
    /// The sprite resource for this emote.
    /// Can be a texture path or RSI reference.
    /// </summary>
    [DataField(required: true)]
    public SpriteSpecifier Sprite = default!;

    /// <summary>
    /// Priority for sorting within category (lower = earlier).
    /// Default is 0. Use negative for favorites.
    /// </summary>
    [DataField]
    public int Priority { get; private set; } = 0;
}
