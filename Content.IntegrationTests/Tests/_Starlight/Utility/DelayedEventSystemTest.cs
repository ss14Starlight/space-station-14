using System;
using System.Collections.Concurrent;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Utility;
using Content.Server._Starlight.Utility.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Starlight.Utility;

[TestFixture, TestOf(typeof(DelayedEventSystem))]
public sealed class DelayedEventSystemTest : GameTest
{
    private const string TestEventId = "delayed-event-system-test";

    public sealed class DelayedEventListenerSystem : EntitySystem
    {
        public ConcurrentQueue<(EntityUid Entity, string EventId)> Triggered { get; } = new();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DelayedEventTriggeredEvent>(OnDelayedEventTriggered);
        }

        private void OnDelayedEventTriggered(EntityUid uid, ref DelayedEventTriggeredEvent args)
            => Triggered.Enqueue((uid, args.EventId));

        public void Clear()
        {
            while (Triggered.TryDequeue(out _))
            {
            }
        }
    }

    [Test]
    public async Task ScheduledEventFiresAndCancellationSuppressesEvent()
    {
        var server = Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var systems = server.ResolveDependency<IEntitySystemManager>();
        var delayedEvents = systems.GetEntitySystem<DelayedEventSystem>();
        var listener = systems.GetEntitySystem<DelayedEventListenerSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid scheduledEntity = default;
        EntityUid cancelledEntity = default;

        await server.WaitAssertion(() =>
        {
            listener.Clear();
            scheduledEntity = entities.SpawnEntity(null, map.MapCoords);
            cancelledEntity = entities.SpawnEntity(null, map.MapCoords);

            delayedEvents.Schedule(scheduledEntity, TestEventId, TimeSpan.FromMilliseconds(100));
            delayedEvents.Schedule(cancelledEntity, TestEventId, TimeSpan.FromMilliseconds(100));
            delayedEvents.Cancel(cancelledEntity, TestEventId);

            Assert.That(listener.Triggered, Is.Empty);
        });

        await PoolManager.WaitUntil(server, () => listener.Triggered.Count > 0, maxTicks: 60);

        await server.WaitAssertion(() =>
        {
            var triggered = listener.Triggered.ToArray();
            Assert.That(triggered, Has.Length.EqualTo(1));
            Assert.That(triggered[0].Entity, Is.EqualTo(scheduledEntity));
            Assert.That(triggered[0].EventId, Is.EqualTo(TestEventId));
        });
    }
}
