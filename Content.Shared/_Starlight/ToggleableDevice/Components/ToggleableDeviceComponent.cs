using Content.Shared._Starlight.ToggleableDevice.EntitySystems;
using Content.Shared.Database;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.ToggleableDevice.Components;

[RegisterComponent]
[NetworkedComponent]
[AutoGenerateComponentState]
[Access(typeof(ToggleableDeviceSystem))]
public sealed partial class ToggleableDeviceComponent : Component
{
    /// <summary>
    /// Whether the device is enabled or not.
    /// </summary>
    [DataField, AutoNetworkedField] public bool Enabled;

    /// <summary>
    /// The log type to use for state changes. Not persisted.
    /// </summary>
    public LogType LogType = LogType.Action;
}
