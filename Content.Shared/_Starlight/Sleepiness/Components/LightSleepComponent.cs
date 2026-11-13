using Robust.Shared.GameStates;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Sleepiness.Components;

/// <summary>
/// Allows an entity to retain vision during voluntary or shallow sleepiness-induced sleep, with drone-like humanoid identification.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LightSleepComponent : Component;
