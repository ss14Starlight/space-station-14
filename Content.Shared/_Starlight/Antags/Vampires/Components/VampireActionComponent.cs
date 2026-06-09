namespace Content.Shared._Starlight.Antags.Vampires.Components;

using Content.Shared._Starlight.Antags.Vampires.Prototypes;
using Robust.Shared.Prototypes;

/// <summary>
/// Attach to a spawned action entity to define Vampire-specific gating and costs
/// - BloodToUnlock - required TotalBlood to unlock the action
/// - BloodCost - Amount to consume on use
/// - RequiredClass - optional class requirement for the action to be usable
/// - RequiresFullPower - whether the vampire must have achieved full power
/// - AllowNonVampireUsers - lets directly granted admin/debug actions run without adding VampireComponent
/// </summary>
[RegisterComponent]
public sealed partial class VampireActionComponent : Component
{
    [DataField]
    public int BloodToUnlock = 0;

    [DataField]
    public float BloodCost = 0f;

    [DataField]
    public ProtoId<VampireClassPrototype>? RequiredClass = null;

    [DataField]
    public bool RequiresFullPower;

    [DataField]
    public bool AllowNonVampireUsers = true;
}
