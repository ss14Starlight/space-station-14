using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Pinpointer.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ItgResourceReclaimerComponent : Component
{
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public ItgResourceReclaimerMode Mode = ItgResourceReclaimerMode.Alive;
}

[Serializable, NetSerializable]
public enum ItgResourceReclaimerMode : byte
{
    Alive,
    Crit,
    Dead
}
