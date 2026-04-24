using Content.Shared.Shipyard.Components;

namespace Content.Server.Shipyard.Components;

[RegisterComponent]
[ComponentReference(typeof(SharedShipyardConsoleComponent))]
public sealed partial class ShipyardConsoleComponent : SharedShipyardConsoleComponent {}
