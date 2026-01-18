using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Starlight.CosmicCult.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CosmicDebuffQueueComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan ActivationTime = default!;

    [DataField]
    public TimeSpan MaxTimeInQueue = TimeSpan.FromSeconds(270);

    [DataField]
    public TimeSpan MinTimeInQueue = TimeSpan.FromSeconds(80);

    [DataField]
    public Enum SelectedDebuff;

    [DataField]
    public int DebuffQuant = 0;

    public Dictionary<Enum, float> DebuffOptions = new Dictionary<Enum, float>()
    {
       {CosmicDebuffOptions.CosmicDebuffMigraine, 8f},
       {CosmicDebuffOptions.CosmicDebuffStutter, 6f},
       {CosmicDebuffOptions.CosmicDebuffVomiting, 6f},
       {CosmicDebuffOptions.CosmicDebuffSleeping, 4f},
       {CosmicDebuffOptions.CosmicDebuffTeleporting, 1f}
    };
}
public enum CosmicDebuffOptions : byte
{
    CosmicDebuffMigraine,
    CosmicDebuffStutter,
    CosmicDebuffVomiting,
    CosmicDebuffSleeping,
    CosmicDebuffTeleporting,
}
