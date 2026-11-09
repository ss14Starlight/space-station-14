using Robust.Shared.GameObjects;

namespace Content.Server._Starlight.Utility.Events;

[ByRefEvent]
public readonly record struct DelayedEventTriggeredEvent(string EventId);
