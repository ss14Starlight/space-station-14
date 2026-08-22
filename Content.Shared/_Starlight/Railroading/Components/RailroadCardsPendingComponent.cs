using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Railroading.Components;

/// <summary>
/// Present on a player while they have railroading cards waiting to be picked.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RailroadCardsPendingComponent : Component;
