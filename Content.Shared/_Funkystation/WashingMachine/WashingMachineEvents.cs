namespace Content.Shared._Funkystation.WashingMachine;

[ByRefEvent]
public record struct WashingMachineIsBeingWashed(EntityUid WashingMachine, HashSet<EntityUid> Items);

[ByRefEvent]
public record struct WashingMachineStartedWashingEvent(HashSet<EntityUid> Items);

[ByRefEvent]
public record struct WashingMachineWashedEvent(EntityUid WashingMachine, HashSet<EntityUid> Items);

[ByRefEvent]
public record struct WashingMachineFinishedWashingEvent(HashSet<EntityUid> Items);
