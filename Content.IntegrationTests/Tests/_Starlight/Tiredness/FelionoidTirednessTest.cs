#nullable enable
using System.Linq;
using Content.Server._Starlight.Tiredness;
using Content.Server.Storage.EntitySystems;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Alert;
using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.StatusEffectNew;
using Content.Shared._Starlight.Sleepiness.Components;
using Content.Shared._Starlight.Tiredness.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Starlight.Tiredness;

[TestFixture]
public sealed class FelionoidTirednessTest : GameTest
{
    [TestPrototypes]
    private const string TestPrototypes = """
    -
        type: entity
        id: TirednessTestEntityStorage
        name: test storage
        components:
            -
                type: EntityStorage
            -
                type: ContainerContainer
                containers:
                    entity_storage: !type:Container
    """;

    public override PoolSettings PoolSettings => new() { Connected = true, DummyTicker = false };

    [Test]
    public async Task SleepActionTracksContainerMembership()
    {
        var pair = Pair;
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        var mapSystem = server.System<SharedMapSystem>();
        var handsSystem = server.System<SharedHandsSystem>();
        var containerSystem = server.System<SharedContainerSystem>();
        var entityStorageSystem = server.System<EntityStorageSystem>();
        var actionsSystem = server.System<SharedActionsSystem>();
        EntityUid holder = default;
        EntityUid felionoid = default;
        EntityUid storage = default;

        try
        {
            await server.WaitPost(() =>
            {
                holder = entityManager.Spawn("MobHuman", map.MapCoords);
                felionoid = entityManager.Spawn("MobFelionoid", map.MapCoords);
            });
            await pair.RunTicksSync(5);

            await server.WaitPost(() =>
            {
                AssertSleepAction(actionsSystem, entityManager, felionoid, expected: false);

                var hands = entityManager.GetComponent<HandsComponent>(holder);
                Assert.That(handsSystem.TryPickup(
                    holder,
                    felionoid,
                    hands.ActiveHandId,
                    checkActionBlocker: false,
                    animate: false), Is.True);
                AssertSleepAction(actionsSystem, entityManager, felionoid, expected: true);

                Assert.That(handsSystem.TryDrop(holder, felionoid, checkActionBlocker: false), Is.True);
                AssertSleepAction(actionsSystem, entityManager, felionoid, expected: false);

                storage = entityManager.Spawn("TirednessTestEntityStorage", map.MapCoords);
            });
            await pair.RunTicksSync(5);

            await server.WaitPost(() =>
            {
                entityStorageSystem.OpenStorage(storage);
                entityStorageSystem.CloseStorage(storage);
                Assert.That(containerSystem.IsEntityInContainer(felionoid), Is.True);
                AssertSleepAction(actionsSystem, entityManager, felionoid, expected: true);

                entityStorageSystem.OpenStorage(storage);
                Assert.That(containerSystem.IsEntityInContainer(felionoid), Is.False);
                AssertSleepAction(actionsSystem, entityManager, felionoid, expected: false);

                var genericContainer = containerSystem.EnsureContainer<Container>(holder, "tiredness-test");
                Assert.That(containerSystem.Insert(felionoid, genericContainer), Is.True);
                AssertSleepAction(actionsSystem, entityManager, felionoid, expected: true);

                var tiredness = entityManager.GetComponent<TirednessComponent>(felionoid);
                var actionId = tiredness.SleepAction!.Value;
                var action = actionsSystem.GetActions(felionoid).Single(action => action.Owner == actionId);
                actionsSystem.PerformAction((felionoid, null), action);

                Assert.That(entityManager.HasComponent<SleepingComponent>(felionoid), Is.True);
                Assert.That(entityManager.HasComponent<PlayerRequestedSleepComponent>(felionoid), Is.True);
            });

            await pair.RunTicksSync(5);
        }
        finally
        {
            await server.WaitPost(() => mapSystem.DeleteMap(map.MapId));
            await pair.RunUntilSynced();
        }
    }

    [Test]
    public async Task TirednessAccumulatesRecoversAndClamps()
    {
        var pair = Pair;
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        EntityUid felionoid = default;

        await server.WaitPost(() => felionoid = entityManager.Spawn("MobFelionoid", MapCoordinates.Nullspace));

        try
        {
            await pair.RunSeconds(2);

            var tirednessBeforeSleep = TimeSpan.Zero;
            await server.WaitPost(() =>
            {
                tirednessBeforeSleep = entityManager.GetComponent<TirednessComponent>(felionoid).CurrentTiredness;
                Assert.That(tirednessBeforeSleep, Is.GreaterThan(TimeSpan.Zero));
                Assert.That(server.System<SleepingSystem>().TrySleeping(felionoid), Is.True);
            });

            await pair.RunSeconds(2);

            await server.WaitPost(() =>
            {
                var tirednessSystem = server.System<TirednessSystem>();
                var tiredness = entityManager.GetComponent<TirednessComponent>(felionoid);
                var alerts = server.System<AlertsSystem>();

                Assert.That(tiredness.CurrentTiredness, Is.LessThan(tirednessBeforeSleep));

                tirednessSystem.SetTiredness((felionoid, tiredness), TimeSpan.FromMinutes(30));
                Assert.That(tiredness.CurrentTiredness, Is.EqualTo(TirednessSystem.MaximumTiredness));
                Assert.That(alerts.IsShowingAlertCategory(felionoid, "Tiredness"), Is.True);

                tirednessSystem.SetTiredness((felionoid, tiredness), TimeSpan.FromMinutes(-1));
                Assert.That(tiredness.CurrentTiredness, Is.EqualTo(TimeSpan.Zero));
                Assert.That(alerts.IsShowingAlertCategory(felionoid, "Tiredness"), Is.False);
            });
        }
        finally
        {
            await server.WaitPost(() => entityManager.DeleteEntity(felionoid));
            await pair.RunUntilSynced();
        }
    }

    [Test]
    public async Task TirednessSleepinessIsCappedAndInductionRespectsConfiguration()
    {
        var pair = Pair;
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var maximumSleepiness = TimeSpan.FromSeconds(20);
        EntityUid felionoid = default;

        await server.WaitPost(() =>
        {
            felionoid = entityManager.Spawn("MobFelionoid", MapCoordinates.Nullspace);
            var tiredness = entityManager.GetComponent<TirednessComponent>(felionoid);
            tiredness.CurrentTiredness = TirednessSystem.MaximumTiredness;
            tiredness.MaximumSleepiness = maximumSleepiness;
            entityManager.Dirty(felionoid, tiredness);
        });

        try
        {
            await pair.RunSeconds(2);

            await server.WaitPost(() =>
            {
                var statusEffects = server.System<StatusEffectsSystem>();
                Assert.That(statusEffects.HasEffectComp<SleepinessStatusEffectComponent>(felionoid), Is.True);
                Assert.That(
                    statusEffects.TrySetStatusEffectDuration(
                        felionoid,
                        "StatusEffectSleepiness",
                        TimeSpan.FromSeconds(19.99)),
                    Is.True);
            });

            await pair.RunSeconds(5);

            await server.WaitPost(() =>
            {
                var statusEffects = server.System<StatusEffectsSystem>();
                var timing = server.ResolveDependency<IGameTiming>();
                Assert.That(statusEffects.TryGetTime(felionoid, "StatusEffectSleepiness", out var sleepiness), Is.True);
                Assert.That(sleepiness.EndEffectTime, Is.Not.Null);
                var remainingSleepiness = sleepiness.EndEffectTime!.Value - timing.CurTime;
                Assert.That(remainingSleepiness, Is.LessThanOrEqualTo(maximumSleepiness));
                Assert.That(entityManager.HasComponent<SleepingComponent>(felionoid), Is.False);
            });
        }
        finally
        {
            await server.WaitPost(() => entityManager.DeleteEntity(felionoid));
            await pair.RunUntilSynced();
        }

        await TestTirednessSleepInduction(canInduceSleep: true, expectedSleeping: true);
        await TestTirednessSleepInduction(canInduceSleep: false, expectedSleeping: false);
    }

    private async Task TestTirednessSleepInduction(bool canInduceSleep, bool expectedSleeping)
    {
        var pair = Pair;
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        EntityUid felionoid = default;

        await server.WaitPost(() =>
        {
            felionoid = entityManager.Spawn("MobFelionoid", MapCoordinates.Nullspace);
            var tiredness = entityManager.GetComponent<TirednessComponent>(felionoid);
            tiredness.CurrentTiredness = TirednessSystem.MaximumTiredness;
            tiredness.MaximumSleepiness = TimeSpan.FromSeconds(40);
            tiredness.CanInduceSleep = canInduceSleep;
            entityManager.Dirty(felionoid, tiredness);
        });

        try
        {
            await pair.RunSeconds(2);

            await server.WaitPost(() =>
            {
                var statusEffects = server.System<StatusEffectsSystem>();
                Assert.That(
                    statusEffects.TrySetStatusEffectDuration(
                        felionoid,
                        "StatusEffectSleepiness",
                        TimeSpan.FromSeconds(29.99)),
                    Is.True);
            });

            await pair.RunSeconds(10);

            await server.WaitPost(() =>
                Assert.That(entityManager.HasComponent<SleepingComponent>(felionoid), Is.EqualTo(expectedSleeping)));
        }
        finally
        {
            await server.WaitPost(() => entityManager.DeleteEntity(felionoid));
            await pair.RunUntilSynced();
        }
    }

    private static void AssertSleepAction(
        SharedActionsSystem actionsSystem,
        IEntityManager entityManager,
        EntityUid felionoid,
        bool expected)
    {
        var tiredness = entityManager.GetComponent<TirednessComponent>(felionoid);
        var hasAction = tiredness.SleepAction is { } actionId &&
                        actionsSystem.GetActions(felionoid).Any(action => action.Owner == actionId);

        Assert.That(hasAction, Is.EqualTo(expected));
    }
}
