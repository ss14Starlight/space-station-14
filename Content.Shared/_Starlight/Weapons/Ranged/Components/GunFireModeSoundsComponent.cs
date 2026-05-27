using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Weapons.Ranged.Components;

/// <summary>
/// Overrides the gunshot sound per fire mode index.
/// Pair with <see cref="BatteryWeaponFireModesComponent"/>; the key in <see cref="Sounds"/>
/// corresponds to the index in <see cref="BatteryWeaponFireModesComponent.FireModes"/>.
/// Modes without an entry keep the gun's base sound.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GunFireModeSoundsComponent : Component
{
    /// <summary>
    /// Maps fire mode index → gunshot sound override.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public Dictionary<int, SoundSpecifier> Sounds = new();
}
