namespace Content.Server._Starlight.Objectives.Components;

/// <summary>
/// Tracks the uplink account that receives this mind's objective rewards.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectiveBonusRecipientComponent : Component
{
    /// <summary>
    /// The initialized uplink store, independent of access device.
    /// </summary>
    public EntityUid? Uplink;

    /// <summary>
    /// Objective bonuses already credited to this mind's uplink balance.
    /// Cleared when the balance is reset so assigned objectives can restore their bonuses.
    /// </summary>
    public HashSet<EntityUid> RewardedObjectives = new();
}
