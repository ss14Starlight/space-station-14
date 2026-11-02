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
}
