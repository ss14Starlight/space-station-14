using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sol.Medical.Virology.Components;

/// <summary>
/// A collected sample (swab, blood vial fraction, culture) carrying pathogen identity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PathogenSampleComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<PathogenPrototype>? PathogenId;

    [DataField, AutoNetworkedField]
    public float Dose;

    [DataField, AutoNetworkedField]
    public PathogenStage? DetectedStage;

    [DataField, AutoNetworkedField]
    public bool IsBloodSample;

    [DataField, AutoNetworkedField]
    public bool IsCentrifuged;

    [DataField, AutoNetworkedField]
    public bool Used;

    /// <summary>
    /// When true, diagnoser should report a false-negative (incubation window).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ForceNegative;
}

/// <summary>
/// Marks an item as a disease swab capable of collecting pathogen samples.
/// Coexists with BotanySwab on DiseaseSwab entities.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DiseasePathogenSwabComponent : Component
{
    [DataField]
    public bool Used;
}

/// <summary>
/// Vaccine item that grants immunity for a pathogen identity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PathogenVaccineComponent : Component
{
    [DataField, AutoNetworkedField]
    public string VaccineIdentity = string.Empty;

    [DataField, AutoNetworkedField]
    public ProtoId<PathogenPrototype>? PathogenId;

    [DataField, AutoNetworkedField]
    public float Strength = 0f;

    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromHours(2);

    [DataField, AutoNetworkedField]
    public bool Used;
}
