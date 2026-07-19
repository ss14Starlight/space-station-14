using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Virology.Components;

/// <summary>
/// Wall-mounted quarantine alarm that flashes its light layer when activated by device link.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VirologyAlarmLightComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;
}

[Serializable, NetSerializable]
public enum VirologyAlarmLightVisuals : byte
{
    On
}

public enum VirologyAlarmLightVisualLayers : byte
{
    Base,
    LightOff,
    LightOn
}
