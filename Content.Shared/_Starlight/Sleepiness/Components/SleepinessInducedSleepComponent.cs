using Robust.Shared.GameStates;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Sleepiness.Components;

/// <summary>
/// Marks a sleep started by the sleepiness system until the entity wakes.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SleepinessInducedSleepComponent : Component;
