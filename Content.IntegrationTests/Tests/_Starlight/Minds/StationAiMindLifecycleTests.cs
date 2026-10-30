using Content.IntegrationTests.Pair;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared.Mind;
using Content.Shared.Maps;
using Content.Shared.Silicons.StationAi;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.IntegrationTests.Tests.Minds;

public sealed partial class MindTests
{
    private const string StationAiTestMapId = "Empty";

    [Test]
    public async Task StationAiRemainsHeldWhileMindVisitsGhost()
    {
        var (pair, holder, ai, mindId, station) = await SetupAiHolder();
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();

        await pair.Server.WaitAssertion(() =>
        {
            var ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, MapCoordinates.Nullspace);
            entMan.System<MindSystem>().Visit(mindId, ghost);
        });
        await pair.RunTicksSync(5);

        Assert.That(entMan.GetComponent<StationAiHolderComponent>(holder).Slot.Item, Is.EqualTo(ai));
        AssertStationAiJobSlots(entMan, station, 0);
    }

    [Test]
    public async Task StationAiEjectsWhenMindLeavesBody()
    {
        var (pair, holder, _, mindId, station) = await SetupAiHolder();
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();

        await pair.Server.WaitAssertion(() =>
        {
            var ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, MapCoordinates.Nullspace);
            entMan.System<MindSystem>().TransferTo(mindId, ghost);
        });
        await pair.RunTicksSync(5);

        Assert.That(entMan.GetComponent<StationAiHolderComponent>(holder).Slot.Item, Is.Null);
        AssertStationAiJobSlots(entMan, station, 1);
    }

    [Test]
    public async Task StationAiEjectsWhenMindMovesToAnotherRole()
    {
        var (pair, holder, ai, mindId, station) = await SetupAiHolder();
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();

        await pair.Server.WaitAssertion(() =>
        {
            var mindSystem = entMan.System<MindSystem>();
            var ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, MapCoordinates.Nullspace);
            mindSystem.Visit(mindId, ghost);
            Assert.That(entMan.GetComponent<StationAiHolderComponent>(holder).Slot.Item, Is.EqualTo(ai));

            var userId = entMan.GetComponent<MindComponent>(mindId).UserId!.Value;
            var newMind = mindSystem.CreateMind(null);
            mindSystem.SetUserId(newMind, userId);
            var role = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            mindSystem.TransferTo(newMind, role);
        });
        await pair.RunTicksSync(5);

        Assert.That(entMan.GetComponent<StationAiHolderComponent>(holder).Slot.Item, Is.Null);
        AssertStationAiJobSlots(entMan, station, 1);
    }

    [Test]
    public async Task StationAiEjectsWhenPlayerDisconnects()
    {
        var (pair, holder, ai, mindId, station) = await SetupAiHolder();
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();

        await pair.Server.WaitAssertion(() =>
        {
            var ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, MapCoordinates.Nullspace);
            entMan.System<MindSystem>().Visit(mindId, ghost);
        });
        await pair.RunTicksSync(5);
        Assert.That(entMan.GetComponent<StationAiHolderComponent>(holder).Slot.Item, Is.EqualTo(ai));

        await Disconnect(pair);

        Assert.That(entMan.GetComponent<StationAiHolderComponent>(holder).Slot.Item, Is.Null);
        AssertStationAiJobSlots(entMan, station, 1);
    }

    private async Task<(TestPair Pair, EntityUid Holder, EntityUid Ai, EntityUid Mind, EntityUid Station)> SetupAiHolder()
    {
        var pair = await SetupPair();
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();
        var (mindId, _) = GetMind(pair);
        EntityUid ai = default;
        EntityUid holder = default;
        EntityUid station = default;

        await pair.Server.WaitAssertion(() =>
        {
            var map = pair.Server.ResolveDependency<IPrototypeManager>().Index<GameMapPrototype>(StationAiTestMapId);
            station = entMan.System<StationSystem>().InitializeNewStation(map.Stations["Empty"], null, "Station AI test");
        });
        await pair.RunTicksSync(1);

        await pair.Server.WaitAssertion(() =>
        {
            var stationJobs = entMan.System<StationJobsSystem>();
            Assert.That(stationJobs.TrySetJobSlot(station, "StationAi", 1, true), Is.True);
            var userId = entMan.GetComponent<MindComponent>(mindId).UserId!.Value;
            Assert.That(stationJobs.TryAssignJob(station, "StationAi", userId), Is.True);

            ai = entMan.SpawnEntity("StationAiBrainConstructed", MapCoordinates.Nullspace);
            entMan.System<MindSystem>().TransferTo(mindId, ai);
            AssertStationAiJobSlots(entMan, station, 0);

            holder = entMan.SpawnEntity("Intellicard", MapCoordinates.Nullspace);
            var aiHolder = entMan.GetComponent<StationAiHolderComponent>(holder);
            var slotContainer = aiHolder.Slot.ContainerSlot;
            if (slotContainer == null)
                throw new InvalidOperationException("Intellicard AI container was not initialized.");

            Assert.That(
                entMan.System<SharedContainerSystem>().Insert(ai, slotContainer),
                Is.True);
        });
        await pair.RunTicksSync(5);

        return (pair, holder, ai, mindId, station);
    }

    private static void AssertStationAiJobSlots(IServerEntityManager entMan, EntityUid station, int expectedSlots) =>
        Assert.That(
            entMan.System<StationJobsSystem>().TryGetJobSlot(station, "StationAi", out var slots)
            && slots == expectedSlots,
            Is.True);
}
