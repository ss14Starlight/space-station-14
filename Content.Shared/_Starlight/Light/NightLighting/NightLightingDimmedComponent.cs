using Content.Shared.Light.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Light.NightLighting;

/// <summary>
/// Added to a <see cref="PoweredLightComponent"/> entity while its APC uses night lighting.
/// Holds a copy of the APC's night profile so the light can be predicted.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NightLightingDimmedComponent : Component
{
    /// <summary>
    /// Determines multiplier which will be applied to light energy.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float EnergyMultiplier = 1f;

    /// <summary>
    /// Determines multiplier which will be applied to light radius.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RadiusMultiplier = 1f;

    /// <summary>
    /// Determines multiplier which will be applied to light power consumtion.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PowerMultiplier = 1f;

    /// <summary>
    /// Determines tint which will be applied to light. For examlpe to make while lamp more yellowish.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color Tint = Color.White;
}
