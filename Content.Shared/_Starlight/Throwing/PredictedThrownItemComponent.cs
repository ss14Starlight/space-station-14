using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Throwing;

/// <summary>
/// This is really fucking stupid but because Content.Client.Throwing.ThrownItemVisualizerSystem uses ComponentShutdown,
/// I need a new component to track prediction. Dumb.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PredictedThrownItemComponent : Component
{
}
