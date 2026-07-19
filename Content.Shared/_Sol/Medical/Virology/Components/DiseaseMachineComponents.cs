using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sol.Medical.Virology.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class DiseaseDiagnoserComponent : Component
{
    [DataField]
    public EntProtoId ReportPrototype = "DiagnosisReportPaper";

    [DataField]
    public TimeSpan AnalysisDelay = TimeSpan.FromSeconds(3);

    public bool Processing;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class VaccinatorComponent : Component
{
    [DataField]
    public EntProtoId VaccinePrototype = "Vaccine";

    [DataField]
    public TimeSpan ProductionDelay = TimeSpan.FromSeconds(5);

    public bool Processing;
}

/// <summary>
/// Admin-only analyzer that exposes full virology debug state.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DebugHealthAnalyzerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool ShowPathogens = true;

    [DataField, AutoNetworkedField]
    public bool ShowImmunity = true;

    [DataField, AutoNetworkedField]
    public bool ShowContamination = true;

    [DataField, AutoNetworkedField]
    public bool ShowSurgeryModifiers = true;
}

/// <summary>
/// Tile/entity airborne contaminant cloud (not an Atmos gas).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AirborneContaminantComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<PathogenContaminationEntry> Contaminants = new();

    [DataField, AutoNetworkedField]
    public float DiffusionRate = 0.05f;

    [DataField, AutoNetworkedField]
    public float DecayMultiplier = 1f;
}
