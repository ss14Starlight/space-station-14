namespace Content.Shared.Silicons.Borgs.Components;

/// <summary>
/// Marker component added to items provided by <see cref="ItemBorgModuleComponent"/>.
/// Prevents the item from being thrown when the borg falls down (enters crit/dead),
/// while still allowing normal item swapping via hand whitelists.
/// Not added to items with <see cref="BorgHand.ForceRemovable"/> set — those should drop on crit.
/// </summary>
[RegisterComponent]
public sealed partial class BorgModuleItemComponent : Component;
