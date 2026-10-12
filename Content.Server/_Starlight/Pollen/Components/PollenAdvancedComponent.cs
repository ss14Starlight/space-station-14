namespace Content.Server._Starlight.Pollen.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PollenAdvancedComponent : Component
{
    [DataField]
    public float SpawnInterval = 10f;

    [DataField]
    public float SpawnIntervalDeviation = 0.2f;

    [DataField]
    public float PollenLifetime = 60f;

    [DataField]
    public float LifetimeDeviationMultiplier = 2f;

    [AutoPausedField]
    public TimeSpan NextSpawn;
}
