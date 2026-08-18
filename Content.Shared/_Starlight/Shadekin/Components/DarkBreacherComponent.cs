using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Shadekin.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class DarkBreacherComponent : Component
{
    [DataField]
    public EntProtoId Portal = "PortalDarkBreacher";

    [DataField]
    public float SpawnDistance = 500f;
}
