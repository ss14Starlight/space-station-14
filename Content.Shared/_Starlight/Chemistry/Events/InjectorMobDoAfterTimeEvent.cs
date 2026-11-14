using Content.Shared.Chemistry.Prototypes;

namespace Content.Shared._Starlight.Chemistry.Events;

/// <summary>
/// Raised on the target mob when an injector calculates how long its do-after on that mob takes.
/// Handlers may change <see cref="DoAfterTime"/>.
/// </summary>
[ByRefEvent]
public record struct InjectorMobDoAfterTimeEvent(
    EntityUid User,
    EntityUid Injector,
    InjectorModePrototype Mode,
    TimeSpan DoAfterTime);
