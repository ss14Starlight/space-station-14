// ReSharper disable CheckNamespace
using Robust.Shared.GameStates;

namespace Content.Shared.Pinpointer;

/// <summary>
/// Network this tag so the client can choose the Syndicate palette before the first snapshot, avoiding a blue flash.
/// </summary>
[NetworkedComponent]
public sealed partial class NukeopsStationMapComponent
{
}
