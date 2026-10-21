using Content.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Objectives.ObjectivePicker;

[RegisterComponent]
public sealed partial class ObjectivePickerConfigurationComponent : Component
{
    public EntityUid? Rule;
    public Dictionary<string, float> Weights = new();
    public Dictionary<string, float> StoryWeights = new();
    public int PreferredOptions;
    public HashSet<NetEntity> DeferredTargets = new();
    public Dictionary<int, EntityUid> TargetTokens = new();
    public bool Finished;
    public float MinimumDifficulty;
    public TimeSpan SelectionDelay;
    public bool MulliganUsed;
    public HashSet<EntityUid> CurrentBatch = new();
    public HashSet<EntityUid> CompletedObjectives = new();
    public bool BatchUnlocked;
}
