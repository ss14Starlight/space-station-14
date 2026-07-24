using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Alert;

namespace Content.Shared._Starlight.GPS.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class AstroNavComponent : Component
{
    [DataField]
    public ProtoId<AlertPrototype> GPSAlert = "GPSAlert";
}