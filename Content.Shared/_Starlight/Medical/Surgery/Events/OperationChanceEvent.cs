using Content.Shared.Starlight.Medical.Surgery.Effects.Step;

namespace Content.Shared.Starlight.Medical.Surgery.Events;

/// <summary>
///    Raised to determine the chance of success for an operation.
/// </summary>
[ByRefEvent]
public record struct OperationChanceEvent(EntityUid Performer, EntityUid Target, EntityUid? Tool, EntityUid Step, SurgeryStepPenaltiesComponent Penalties, float Chance = 1f, string Reason = "", List<string>? Factors = null, bool ForceSuccess = false);
