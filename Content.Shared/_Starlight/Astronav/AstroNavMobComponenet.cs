using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Alert;

namespace Content.Shared._Starlight.Astronav;

[RegisterComponent, NetworkedComponent]
public sealed partial class AstroNavMobComponent : Component
{
}

[ByRefEvent]
public sealed partial class GPSAlertEvent : BaseAlertEvent;
