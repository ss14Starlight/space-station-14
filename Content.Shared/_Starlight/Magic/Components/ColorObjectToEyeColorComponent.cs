using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Put on spells to color objects the color of the casters eyes. Will color the object, its inhands,
/// and if its a point light, its light color.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ColorObjectToEyeColorComponent : Component;
