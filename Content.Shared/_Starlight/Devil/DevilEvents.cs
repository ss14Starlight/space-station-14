using Content.Shared.Actions;

namespace Content.Shared._Starlight.Devil;

public sealed partial class SummonDemonicContractEvent : InstantActionEvent { };

[ByRefEvent]
public record struct DamnationFailEvent();