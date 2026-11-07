using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.DeviceLinking.Components;

[RegisterComponent]
[NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class ToggleableDeviceComponent : Component
{
    [DataField, AutoNetworkedField] public bool Enabled;
}
