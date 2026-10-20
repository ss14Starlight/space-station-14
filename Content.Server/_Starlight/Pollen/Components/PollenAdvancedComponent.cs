namespace Content.Server._Starlight.Pollen.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PollenAdvancedComponent : Component
{
    /// <summary>
    /// Base interval between advanced pollen cloud spawns, in seconds.
    /// </summary>
    [DataField]
    public float SpawnInterval = 10f;

    /// <summary>
    /// Fractional random deviation applied to the spawn interval.
    /// A value of 0.2 allows the interval to vary by up to 20% in either direction.
    /// </summary>
    [DataField]
    public float SpawnIntervalDeviation = 0.2f;

    /// <summary>
    /// Base lifetime of spawned pollen clouds, in seconds.
    /// </summary>
    [DataField]
    public float PollenLifetime = 60f;

    /// <summary>
    /// Multiplier applied to the spawn interval's deviation when adjusting cloud lifetime.
    /// Larger values cause greater lifetime variation; the result is clamped to at least 0.1 seconds.
    /// </summary>
    [DataField]
    public float LifetimeDeviationMultiplier = 2f;

    /// <summary>
    /// Absolute time at which the next pollen cloud spawn is scheduled.
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextSpawn;
}
