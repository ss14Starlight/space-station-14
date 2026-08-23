using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Railroading.Components;

/// <summary>
/// Present on a player while they have railroading cards waiting to be picked.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RailroadCardsPendingComponent : Component
{
    /// <summary>
    /// When the hand is discarded if nothing has been picked. Null until the player first opens the window.
    /// </summary>
    [ViewVariables]
    [NonSerialized]
    public TimeSpan? Deadline;
}
