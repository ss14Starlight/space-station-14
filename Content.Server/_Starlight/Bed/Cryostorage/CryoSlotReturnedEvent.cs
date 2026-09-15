using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Bed.Cryostorage;

// raised when someone enters cryo and the job slot they held opens back up.
// carries the cryo'd body (items still on it) and the freed job
[ByRefEvent]
public readonly record struct CryoSlotReturnedEvent(EntityUid Body, ProtoId<JobPrototype> Job);
