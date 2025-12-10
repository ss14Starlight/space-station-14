using Content.Shared.Inventory;

namespace Content.Shared.Storage.Components;

/// <summary>
/// Applies an ongoing pickup area around the attached entity.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class MagnetPickupComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("nextScan")]
    [AutoPausedField]
    public TimeSpan NextScan = TimeSpan.Zero;

    /// <summary>
    /// What container slot the magnet needs to be in to work.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("slotFlags")]
    public SlotFlags SlotFlags = SlotFlags.BELT;

    // Starlight Edit Start - Remove slot requirements
    /// <summary>
    /// Controlling whether the magnet needs to be equipped to work,
    /// if false, it will work on the ground and in hands,
    /// if true it wont work at all if not equipped.
    /// This also makes it work in borg modules.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("requiresEquipping")]
    public bool RequiresEquipping = true;
    // Starlight Edit End

    [ViewVariables(VVAccess.ReadWrite), DataField("range")]
    public float Range = 1f;
}
