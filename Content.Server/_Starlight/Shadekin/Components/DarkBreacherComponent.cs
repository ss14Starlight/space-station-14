using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Shadekin;

[RegisterComponent]
public sealed partial class DarkBreacherComponent : Component
{
    [DataField]
    public EntProtoId Portal = "PortalDarkBreacher";

    [DataField]
    public float SpawnDistance = 500f;
}