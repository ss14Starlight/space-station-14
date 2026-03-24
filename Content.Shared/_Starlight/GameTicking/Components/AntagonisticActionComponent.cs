using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.GameTicking.Components;

/// <summary>
/// Marker component to identify an action as antagonistic. Intended for use in EOR to prevent EORG.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AntagonisticActionComponent : Component
{
}