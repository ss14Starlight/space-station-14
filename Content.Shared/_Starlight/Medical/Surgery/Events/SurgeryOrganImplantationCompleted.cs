namespace Content.Shared._Starlight.Medical.Surgery.Events;
// Based on the RMC14.
// https://github.com/RMC-14/RMC-14
[ByRefEvent]
public record struct SurgeryOrganImplantationCompleted(EntityUid Body, EntityUid Part, EntityUid Organ, EntityUid User);
[ByRefEvent]
public record struct SurgeryOrganExtracted(EntityUid Body, EntityUid Part, EntityUid Organ, EntityUid User);
