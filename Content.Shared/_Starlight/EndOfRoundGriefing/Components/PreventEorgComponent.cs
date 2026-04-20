using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.EndOfRoundGriefing.Components;

/// <summary>
/// Marker component to disable antagonism. Intended for use in EOR to prevent EORG.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PreventEorgComponent : Component
{
}
