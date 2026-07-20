using Content.Shared._Sol.Medical.Virology;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Virology.Components;

/// <summary>
/// Marks a player as a bioterror cell member.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BioterroristComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool IsHead;
}

/// <summary>
/// Environmental microbe source that can be scraped for trait/chassis material.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EnvironmentalMicrobeSourceComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<PathogenPrototype> ChassisId = "SolPathogenFlu";

    [DataField, AutoNetworkedField]
    public List<WeightedTraitEntry> TraitPool = new();

    [DataField, AutoNetworkedField]
    public float BaseQuality = 0.65f;

    [DataField, AutoNetworkedField]
    public int RemainingSamples = 4;

    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(20);

    [DataField, AutoNetworkedField]
    public TimeSpan NextAvailable;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class WeightedTraitEntry
{
    [DataField(required: true)]
    public ProtoId<PathogenTraitPrototype> Trait = default!;

    [DataField]
    public float Weight = 1f;
}

/// <summary>
/// Sterile scraper used to collect environmental microbial samples.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EnvironmentalScraperComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Sterile = true;

    [DataField, AutoNetworkedField]
    public bool Used;

    [DataField]
    public TimeSpan ScrapeDelay = TimeSpan.FromSeconds(2.5);
}

/// <summary>
/// Raw or analyzed environmental microbial sample.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MicrobialSampleComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<PathogenPrototype>? ChassisId;

    [DataField, AutoNetworkedField]
    public List<ProtoId<PathogenTraitPrototype>> Traits = new();

    [DataField, AutoNetworkedField]
    public float Quality = 0.5f;

    [DataField, AutoNetworkedField]
    public bool Analyzed;

    [DataField, AutoNetworkedField]
    public bool Contaminated;

    [DataField, AutoNetworkedField]
    public string? SourceLabel;
}

/// <summary>
/// Cultured isolate ready for synthesis (chassis or trait culture).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PathogenCultureComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<PathogenPrototype>? ChassisId;

    [DataField, AutoNetworkedField]
    public List<ProtoId<PathogenTraitPrototype>> Traits = new();

    [DataField, AutoNetworkedField]
    public float Viability = 1f;

    [DataField, AutoNetworkedField]
    public bool IsChassisCulture = true;

    /// <summary>
    /// Stack size for gene isolates of the same trait. Substrates stay at 1.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Count = 1;

    [DataField, AutoNetworkedField]
    public TimeSpan SpoilsAt;
}

/// <summary>
/// Physical culture ampoule / payload containing a deployable strain.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PathogenPayloadComponent : Component
{
    [DataField, AutoNetworkedField]
    public string StrainId = string.Empty;

    [DataField, AutoNetworkedField]
    public float Concentration = 5f;

    [DataField, AutoNetworkedField]
    public PathogenPayloadKind Kind = PathogenPayloadKind.Food;

    [DataField, AutoNetworkedField]
    public bool Used;
}

public enum PathogenPayloadKind : byte
{
    Food = 0,
    Surface = 1,
    Aerosol = 2,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ClandestineSampleAnalyzerComponent : Component
{
    [DataField]
    public TimeSpan AnalysisDelay = TimeSpan.FromSeconds(4);

    [DataField, AutoNetworkedField]
    public bool Processing;

    [DataField, AutoNetworkedField]
    public bool HasFinishedSample;

    [DataField, AutoNetworkedField]
    public TimeSpan CycleEndsAt;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ClandestineCultureIncubatorComponent : Component
{
    [DataField]
    public TimeSpan CultureDelay = TimeSpan.FromSeconds(12);

    [DataField]
    public string NutrientReagent = "SolCultureNutrient";

    [DataField]
    public float NutrientBaseCost = 5f;

    [DataField]
    public float NutrientExtraCost = 2f;

    [DataField]
    public int MaxSamples = 6;

    [DataField, AutoNetworkedField]
    public bool CycleInProgress;

    [DataField, AutoNetworkedField]
    public TimeSpan CycleStartedAt;

    [DataField, AutoNetworkedField]
    public TimeSpan CycleEndsAt;

    [DataField, AutoNetworkedField]
    public bool HasFinishedCulture;

    [DataField, AutoNetworkedField]
    public TimeSpan OvergrowAt;

    public static float GetBatchNutrientCost(int sampleCount, float baseCost, float extraCost)
    {
        if (sampleCount <= 0)
            return 0f;

        return baseCost + extraCost * (sampleCount - 1);
    }
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ClandestinePathogenSynthesizerComponent : Component
{
    [DataField]
    public TimeSpan SynthesisBaseDelay = TimeSpan.FromSeconds(12);

    /// <summary>
    /// Extra synthesis time added per gene budget point used.
    /// </summary>
    [DataField]
    public TimeSpan SynthesisDelayPerBudget = TimeSpan.FromSeconds(4);

    [DataField]
    public int MaxTraitBudget = 6;

    [DataField]
    public string StabilizerReagent = "SolCultureStabilizer";

    [DataField]
    public float StabilizerNeeded = 3f;

    [DataField]
    public string AmpoulePrototype = "SolPathogenCultureAmpoule";

    [DataField]
    public int AmpoulesProduced = 2;

    [DataField, AutoNetworkedField]
    public bool CycleInProgress;

    [DataField, AutoNetworkedField]
    public TimeSpan CycleStartedAt;

    [DataField, AutoNetworkedField]
    public TimeSpan CycleEndsAt;

    [DataField, AutoNetworkedField]
    public List<ProtoId<PathogenTraitPrototype>> PendingTraits = new();

    [DataField, AutoNetworkedField]
    public float PendingViability = 1f;

    /// <summary>
    /// Gene isolates currently selected for the next synthesis recipe.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> SelectedGenes = new();

    public TimeSpan GetSynthesisDelay(int budgetUsed)
    {
        var points = Math.Max(0, budgetUsed);
        return SynthesisBaseDelay + SynthesisDelayPerBudget * points;
    }
}

/// <summary>
/// Round-state tracking for bioterror cell objectives / round-end text.
/// </summary>
[RegisterComponent]
public sealed partial class BioterrorCellTrackerComponent : Component
{
    [DataField]
    public bool AnalyzerDeployed;

    [DataField]
    public bool IncubatorDeployed;

    [DataField]
    public bool SynthesizerDeployed;

    [DataField]
    public bool LabEstablishedOffShuttle;

    [DataField]
    public string? SynthesizedStrainId;

    [DataField]
    public float DeployedLoad;

    [DataField]
    public float RequiredDeployedLoad = 8f;

    [DataField]
    public bool Diagnosed;

    [DataField]
    public bool VaccineCreated;

    [DataField]
    public TimeSpan? FirstDeploymentAt;

    [DataField]
    public TimeSpan? VaccineCreatedAt;

    [DataField]
    public TimeSpan DiagnosisDelayTarget = TimeSpan.FromMinutes(10);

    [DataField]
    public EntityUid? SpawnShuttleGrid;
}
