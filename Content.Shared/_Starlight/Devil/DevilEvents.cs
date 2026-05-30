using Content.Shared.Actions;

namespace Content.Shared._Starlight.Devil;

public sealed partial class SummonDemonicContractEvent : InstantActionEvent { };
public sealed partial class OpenDamnationsMenuEvent : InstantActionEvent { };

[ByRefEvent]
public record struct DamnationInitFailEvent();

[ByRefEvent]
public record struct DevilSoulsDamnedCountChangedEvent();
