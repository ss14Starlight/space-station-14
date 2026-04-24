using Content.Shared.Shipyard.Components;

namespace Content.Client.Shipyard.Components;

[RegisterComponent]
[ComponentReference(typeof(SharedShipyardConsoleComponent))]
public sealed partial class ShipyardConsoleComponent : SharedShipyardConsoleComponent {}
