using System;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server._Starlight.Utility;
using Content.Server._Starlight.Utility.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Starlight.Utility;

[TestFixture, TestOf(typeof(DelayedEventSystem))]
public sealed class DelayedEventSystemTest : GameTest
{
    private const string TestEventId = "delayed-event-system-test";

    public sealed class DelayedEventListenerSystem : TestListenerSystem<DelayedEventTriggeredEvent>
    {
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
            scheduledEntity = entities.SpawnEntity(null, map.MapCoords);
            cancelledEntity = entities.SpawnEntity(null, map.MapCoords);
            entities.AddComponent<TestListenerComponent>(scheduledEntity);
            entities.AddComponent<TestListenerComponent>(cancelledEntity);

            delayedEvents.Schedule(scheduledEntity, TestEventId, TimeSpan.FromMilliseconds(100));
            delayedEvents.Schedule(cancelledEntity, TestEventId, TimeSpan.FromMilliseconds(100));
            delayedEvents.Cancel(cancelledEntity, TestEventId);
            Assert.That(listener.Count(scheduledEntity), Is.Zero);
            Assert.That(listener.Count(cancelledEntity), Is.Zero);
        });

        await PoolManager.WaitUntil(server, () => listener.Count(scheduledEntity) > 0, maxTicks: 60);

        await server.WaitAssertion(() =>
        {
            Assert.That(listener.Count(scheduledEntity, ev => ev.EventId == TestEventId), Is.EqualTo(1));
            Assert.That(listener.Count(cancelledEntity), Is.Zero);
        });
    }
}
