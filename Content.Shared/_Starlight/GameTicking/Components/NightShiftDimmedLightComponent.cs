using Content.Shared.Light.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.GameTicking.Components;

/// <summary>
/// Added to an entity with <see cref="PoweredLightComponent"/> when the NightShiftRule affects its brightness.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NightShiftDimmedLightComponent : Component
{
    [DataField]
    public float LightEnergyMultiplier;
}
