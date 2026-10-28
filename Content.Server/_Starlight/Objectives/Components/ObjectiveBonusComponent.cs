using Content.Shared.FixedPoint;

namespace Content.Server._Starlight.Objectives.Components;

/// <summary>
/// Grants extra currency when this objective is added to a mind.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectiveBonusComponent : Component
{
    /// <summary>
    /// Amount of TC to add to the recipient's uplink.
    /// </summary>
    [DataField(required: true)]
    public FixedPoint2 Telecrystals;

    /// <summary>
    /// Minds already rewarded for this objective instance, preventing duplicate payments.
    /// Separate minds can receive a reward even when they share the same objective entity.
    /// </summary>
    public HashSet<EntityUid> RewardedMinds = new();
}
