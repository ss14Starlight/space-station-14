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
}
