namespace Content.Server._NullLink.EventBus;

public sealed partial class EventBusSystem : EntitySystem
{
    private const int MaxEventsPerTick = 3;

    [Dependency] private INullLinkEventBusManager _nullLinkEventBusManager = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        for (var i = 0; i < MaxEventsPerTick && _nullLinkEventBusManager.TryDequeue(out var @event); i++)
            RaiseLocalEvent(@event);
    }
}
