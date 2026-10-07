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
    [DataField, AutoNetworkedField]
    public float EnergyMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float RadiusMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float PowerMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public Color Tint = Color.White;
}
