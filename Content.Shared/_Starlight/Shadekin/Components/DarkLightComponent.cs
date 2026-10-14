using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Shadekin.Components;

/// <summary>
/// DarkLight Ents will be ingored by the "Light Sensetivity Check"
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DarkLightComponent : Component;
