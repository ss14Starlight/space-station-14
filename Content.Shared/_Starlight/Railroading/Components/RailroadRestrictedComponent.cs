using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Railroading.Components;

/// <summary>
/// Present on a player who let a hand expire and will not be offered cards again this round.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RailroadRestrictedComponent : Component
{
    public override bool SendOnlyToOwner => true;
}
