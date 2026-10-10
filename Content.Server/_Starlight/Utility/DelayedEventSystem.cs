using System.Collections.Generic;
using Content.Server._Starlight.Utility.Components;
using Content.Server._Starlight.Utility.Events;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Utility;

public sealed partial class DelayedEventSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;

    private readonly List<(EntityUid, string)> _expiredEvents = new();

    /// <summary>
    /// Schedules one delayed event on an entity, replacing any existing delayed event.
    /// </summary>
    public void Schedule(EntityUid uid, string eventId, TimeSpan delay)
    {
        var component = EnsureComp<DelayedEventComponent>(uid);
        component.EventId = eventId;
        component.TriggerTime = _gameTiming.CurTime + delay;
    }

    public void Cancel(EntityUid uid, string eventId)
    {
        if (!TryComp<DelayedEventComponent>(uid, out var component) || component.EventId != eventId)
            return;

        RemComp<DelayedEventComponent>(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _expiredEvents.Clear();
        var query = EntityQueryEnumerator<DelayedEventComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.TriggerTime <= _gameTiming.CurTime)
                _expiredEvents.Add((uid, component.EventId));
        }

        foreach (var (uid, eventId) in _expiredEvents)
        {
            if (!TryComp<DelayedEventComponent>(uid, out var component) || component.EventId != eventId)
                continue;

            RemComp<DelayedEventComponent>(uid);
            var ev = new DelayedEventTriggeredEvent(eventId);
            RaiseLocalEvent(uid, ref ev);
        }
    }
}
