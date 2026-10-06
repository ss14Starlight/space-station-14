using Robust.Shared.GameObjects;

namespace Content.Shared._Starlight.Sleepiness.Events;

[ByRefEvent]
public record struct SleepinessWakeAttemptEvent(EntityUid Target, TimeSpan WakePower)
{
    public bool? Result;
}
