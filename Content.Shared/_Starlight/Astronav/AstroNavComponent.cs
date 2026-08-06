using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Alert;

namespace Content.Shared._Starlight.Astronav;

[RegisterComponent, NetworkedComponent]
public sealed partial class AstroNavComponent : Component
{
    [DataField]
    public ProtoId<AlertPrototype> GPSAlert = "GPSAlert";
    [DataField]
    public float MaxRange = 32f; // Chud range since it requires no power to operate and doesn't take up a slot. Regular mass scanner is 256.
}
