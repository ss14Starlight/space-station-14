using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.CentComm;

[RegisterComponent, NetworkedComponent]
public sealed partial class CentCommManagementConsoleComponent : Component
{

    [DataField]
    public string PrimaryKey = "disk1";

    [DataField]
    public string SecondaryKey = "disk2";
}