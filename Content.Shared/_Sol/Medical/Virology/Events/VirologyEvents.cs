using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Virology.Events;

/// <summary>
/// Raised on the patient body after a surgery step completes (Sol hook).
/// </summary>
[ByRefEvent]
public record struct SolSurgeryStepCompletedEvent(
    EntityUid User,
    EntityUid Body,
    EntityUid Part,
    List<EntityUid> Tools,
    EntProtoId StepProto,
    EntProtoId SurgeryProto,
    bool IsFinal,
    bool Failed);

/// <summary>
/// Auditable breakdown of surgery infection chance modifiers.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class SurgeryInfectionModifiers
{
    [DataField]
    public float BaseChance;

    [DataField]
    public float OperatorCarrierMultiplier = 1f;

    [DataField]
    public float MaskMultiplier = 1f;

    [DataField]
    public float ToolMultiplier = 1f;

    [DataField]
    public float GloveMultiplier = 1f;

    [DataField]
    public float EnvironmentMultiplier = 1f;

    [DataField]
    public float WoundMultiplier = 1f;

    [DataField]
    public float FailureMultiplier = 1f;

    [DataField]
    public float ImmunityMultiplier = 1f;

    [DataField]
    public float FinalChance;

    [DataField]
    public bool StationEnabled;

    [DataField]
    public string? SelectedPathogenId;

    public void Recalculate()
    {
        FinalChance = Math.Clamp(
            BaseChance
            * OperatorCarrierMultiplier
            * MaskMultiplier
            * ToolMultiplier
            * GloveMultiplier
            * EnvironmentMultiplier
            * WoundMultiplier
            * FailureMultiplier
            * ImmunityMultiplier,
            0f,
            1f);
    }
}

/// <summary>
/// Raised when an entity is exposed to a pathogen dose.
/// </summary>
[ByRefEvent]
public record struct PathogenExposureEvent(
    ProtoId<PathogenPrototype> PathogenId,
    float Dose,
    PathogenTransmission Route,
    EntityUid? Source,
    bool Force = false);

/// <summary>
/// Optional probability override for deterministic tests.
/// </summary>
[ByRefEvent]
public record struct PathogenInfectionRollEvent(float Chance, bool Infected)
{
    public bool Handled;
}

/// <summary>
/// Raised when Medical synthesizes a vaccine for a pathogen / runtime strain.
/// </summary>
[ByRefEvent]
public readonly record struct BioterrorVaccineCreatedEvent(string PathogenId, EntityUid Machine);

/// <summary>
/// Raised when a physical bioterror payload is successfully deployed.
/// </summary>
[ByRefEvent]
public readonly record struct BioterrorPayloadDeployedEvent(
    string StrainId,
    PathogenPayloadKind Kind,
    float Concentration,
    EntityUid User,
    EntityUid? Target);

/// <summary>
/// Raised when a custom strain is synthesized by the clandestine lab.
/// </summary>
[ByRefEvent]
public readonly record struct BioterrorStrainSynthesizedEvent(string StrainId, EntityUid Synthesizer, EntityUid? User);

[Serializable, NetSerializable]
public sealed partial class DiseaseDiagnosisDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class VaccineProductionDoAfterEvent : SimpleDoAfterEvent;
