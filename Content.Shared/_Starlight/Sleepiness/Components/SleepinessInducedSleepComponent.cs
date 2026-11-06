using Robust.Shared.GameStates;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Sleepiness.Components;

/// <summary>
/// Marks a sleep started by the sleepiness system until the entity wakes.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SleepinessInducedSleepComponent : Component
{
    /// <summary>
    /// Sleepiness threshold captured when this sleep began, so eligibility remains known after the effect expires.
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan SleepThreshold;
}
