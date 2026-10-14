
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Weapons.Ranged.Ammo;

/// <summary>
/// Sent when an ammunition type has been selected
/// </summary>
/// <param name="selectedIndex"></param>
[Serializable, NetSerializable]
public sealed class GunSelectableAmmoSelectMessage(int selectedIndex) : BoundUserInterfaceMessage
{
    /// <summary>
    /// The index of the ammo setting to select
    /// </summary>
    public readonly int SelectedIndex = selectedIndex;
}

[Serializable, NetSerializable]
public enum GunSelectableAmmoUiKey : byte
{
    Key,
}
