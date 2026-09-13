using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Movement.Pulling.Components;

/// <summary>
/// Marks an entity whose <see cref="Content.Shared.Movement.Pulling.Components.PullerComponent"/> was granted by
/// <see cref="PullTrainComponent"/> so it can be taken away once the car is uncoupled.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PullTrainCarComponent : Component;
