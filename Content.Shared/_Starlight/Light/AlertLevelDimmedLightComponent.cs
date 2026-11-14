using Content.Shared.Light.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Light;

/// <summary>
/// Added to a <see cref="PoweredLightComponent"/> entity when an alert level affects its brightness.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AlertLevelDimmedLightComponent : Component
{
    [DataField, AutoNetworkedField] public float LightEnergyMultiplier = 1.0f;
}
