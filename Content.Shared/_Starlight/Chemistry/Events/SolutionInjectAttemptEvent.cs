using Robust.Shared.GameObjects;

namespace Content.Shared._Starlight.Chemistry.Events;

/// <summary>
/// Raised on a target entity when something attempts to inject a solution into them.
/// Can be cancelled to prevent the injection.
/// Similar to FlashAttemptEvent but for chemical injections.
/// </summary>
[ByRefEvent]
public record struct SolutionInjectAttemptEvent(EntityUid Target, EntityUid? Source, EntityUid? Injector, bool Cancelled = false);
