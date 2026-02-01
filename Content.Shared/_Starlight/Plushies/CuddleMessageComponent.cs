// Component for items that display cuddle messages when used in hand
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Plushies;

/// <summary>
/// Component for items that show a configurable cuddle message when used in hand (Z key).
/// Uses FTL localization for the message.
/// </summary>
/// <example>
[RegisterComponent, NetworkedComponent]
public sealed partial class CuddleMessageComponent : Component
{
    /// <summary>
    /// The FTL localization key for the cuddle message.
    /// The localization should accept a $user parameter.
    /// </summary>
    [DataField(required: true)]
    public LocId LocalizedMessageKey = string.Empty;

    /// <summary>
    /// Whether this item should be prevented from being eaten.
    /// Set to true to block ingestion and prevent the eating doafter.
    /// </summary>
    [DataField]
    public bool PreventEating = true;
}
