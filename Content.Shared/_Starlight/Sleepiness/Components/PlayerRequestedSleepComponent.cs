using Robust.Shared.GameStates;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Sleepiness.Components;

/// <summary>
/// Marks sleep initiated by the player's voluntary sleep request until the entity wakes.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlayerRequestedSleepComponent : Component;
