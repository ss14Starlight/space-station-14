using System.Linq;
using Content.IntegrationTests.Pair;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server.Ghost;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Pinpointer;
using Content.Server.Power.Components;
using Content.Server.Silicons.StationAi;
using Content.Server._Starlight.TextToSpeech;
using Content.Server._Starlight.Station.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared._Starlight.Silicons.Borgs;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Follower;
using Content.Shared.Follower.Components;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
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
    public async Task StationAiRemainsInCoreWhenShuntedIntoBorg()
    {
        var (pair, station, cores) = await SetupAiStation(1);
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();
        var (mindId, _) = GetMind(pair);
        var announcements = entMan.System<StationAiAnnouncementTestSystem>();
        EntityUid ai = default;
        EntityUid borg = default;

        await pair.Server.WaitAssertion(() =>
        {
            var userId = entMan.GetComponent<MindComponent>(mindId).UserId!.Value;
            Assert.That(entMan.System<StationJobsSystem>().TryAssignJob(station, "StationAi", userId), Is.True);

            var containers = entMan.System<SharedContainerSystem>();
            ai = entMan.SpawnEntity("StationAiBrainConstructed", MapCoordinates.Nullspace);
            if (!containers.TryGetContainer(cores[0], StationAiCoreComponent.Container, out var coreContainer))
                throw new InvalidOperationException("Station AI core container was not initialized.");
            Assert.That(containers.Insert(ai, coreContainer), Is.True);
            entMan.System<MindSystem>().TransferTo(mindId, ai);

            announcements.Clear();
            borg = ShuntAiIntoBorg(entMan, ai);
        });
        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<MindComponent>(mindId).OwnedEntity, Is.EqualTo(borg));
            Assert.That(entMan.GetComponent<StationAiHolderComponent>(cores[0]).Slot.Item, Is.EqualTo(ai));
            Assert.That(entMan.GetComponent<StationAIShuntableComponent>(ai).Inhabited, Is.EqualTo(borg));
            Assert.That(announcements.LastMessage, Is.Empty);
            AssertStationAiJobSlots(entMan, station, 0);

            var unshunt = new AIUnShuntActionEvent { Performer = borg };
            entMan.EventBus.RaiseLocalEvent(borg, unshunt);
        });
        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<MindComponent>(mindId).OwnedEntity, Is.EqualTo(ai));
            Assert.That(entMan.GetComponent<StationAiHolderComponent>(cores[0]).Slot.Item, Is.EqualTo(ai));
            Assert.That(entMan.GetComponent<StationAIShuntableComponent>(ai).Inhabited, Is.Null);
            Assert.That(announcements.LastMessage, Is.Empty);
            AssertStationAiJobSlots(entMan, station, 0);
        });
    }

    [Test]
    public async Task StationAiCannotShuntIntoBorgWithMindInBrain()
    {
        var (pair, station, cores) = await SetupAiStation(1);
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();
        var (aiMindId, _) = GetMind(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var userId = entMan.GetComponent<MindComponent>(aiMindId).UserId!.Value;
            Assert.That(entMan.System<StationJobsSystem>().TryAssignJob(station, "StationAi", userId), Is.True);

            var mindSystem = entMan.System<MindSystem>();
            var containers = entMan.System<SharedContainerSystem>();
            var ai = entMan.Spawn("StationAiBrainConstructed", MapCoordinates.Nullspace);
            if (!containers.TryGetContainer(cores[0], StationAiCoreComponent.Container, out var coreContainer))
                throw new InvalidOperationException("Station AI core container was not initialized.");
            Assert.That(containers.Insert(ai, coreContainer), Is.True);
            mindSystem.TransferTo(aiMindId, ai);

            var maps = entMan.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            var borg = entMan.Spawn("BorgChassisGeneric", new MapCoordinates(0, 0, mapId));
            var chassis = entMan.GetComponent<BorgChassisComponent>(borg);
            var brain = entMan.Spawn("AiInterfaceCube", MapCoordinates.Nullspace);
            Assert.That(containers.Insert(brain, chassis.BrainContainer), Is.True);
            var borgMindId = mindSystem.CreateMind(null);
            mindSystem.TransferTo(borgMindId, brain);

            var shunt = new AIShuntActionEvent
            {
                Target = borg,
                Performer = ai,
                IgnoreCameraView = true
            };
            entMan.EventBus.RaiseLocalEvent(ai, shunt);

            Assert.Multiple(() =>
            {
                Assert.That(shunt.Handled, Is.False);
                Assert.That(entMan.GetComponent<MindComponent>(aiMindId).OwnedEntity, Is.EqualTo(ai));
                Assert.That(entMan.GetComponent<MindComponent>(borgMindId).OwnedEntity, Is.EqualTo(brain));
            });

            var chassisShunt = entMan.GetComponent<StationAIShuntComponent>(borg);
            var brainShunt = entMan.GetComponent<StationAIShuntComponent>(brain);
            Assert.Multiple(() =>
            {
                Assert.That(chassisShunt.Return, Is.Null);
                Assert.That(chassisShunt.ReturnAction, Is.Null);
                Assert.That(brainShunt.Return, Is.Null);
                Assert.That(brainShunt.ReturnAction, Is.Null);
                Assert.That(entMan.GetComponent<StationAIShuntableComponent>(ai).Inhabited, Is.Null);
            });
        });
    }

    [Test]
    public async Task StationAiShuntableActionsAreRemovedWhenComponentShutsDown()
    {
        var pair = await SetupPair();
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();

        await pair.Server.WaitAssertion(() =>
        {
            var ai = entMan.Spawn("StationAiBrainConstructed", MapCoordinates.Nullspace);
            var actions = entMan.System<SharedActionsSystem>();
            var entityTargetActions = entMan.GetEntityQuery<EntityTargetActionComponent>();
            var instantActions = entMan.GetEntityQuery<InstantActionComponent>();

            void AssertShuntActions(int expected)
            {
                var activeActions = actions.GetActions(ai).ToArray();
                Assert.That(
                    activeActions.Count(action => entityTargetActions.CompOrNull(action)?.Event is AIShuntActionEvent),
                    Is.EqualTo(expected));
                Assert.That(
                    activeActions.Count(action =>
                        instantActions.CompOrNull(action)?.Event is AIReconnectShuntActionEvent),
                    Is.EqualTo(expected));
            }

            AssertShuntActions(1);
            entMan.RemoveComponent<StationAIShuntableComponent>(ai);
            AssertShuntActions(0);

            entMan.AddComponent<StationAIShuntableComponent>(ai);
            AssertShuntActions(1);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task StationAiCanUnshuntAndReshuntAfterDisconnectWhileShuntedInBorg(bool deleteRemoteEye)
    {
        var (pair, station, cores) = await SetupAiStation(1);
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();
        var (mindId, _) = GetMind(pair);
        EntityUid ai = default;
        EntityUid brain = default;
        EntityUid borg = default;
        EntityUid follower = default;

        await pair.Server.WaitAssertion(() =>
        {
            var userId = entMan.GetComponent<MindComponent>(mindId).UserId!.Value;
            Assert.That(entMan.System<StationJobsSystem>().TryAssignJob(station, "StationAi", userId), Is.True);

            var containers = entMan.System<SharedContainerSystem>();
            ai = entMan.Spawn("StationAiBrainConstructed", MapCoordinates.Nullspace);
            if (!containers.TryGetContainer(cores[0], StationAiCoreComponent.Container, out var coreContainer))
                throw new InvalidOperationException("Station AI core container was not initialized.");
            Assert.That(containers.Insert(ai, coreContainer), Is.True);
            entMan.System<MindSystem>().TransferTo(mindId, ai);

            brain = entMan.Spawn("AiInterfaceCube", MapCoordinates.Nullspace);
            var shunt = new AIShuntActionEvent
            {
                Target = brain,
                Performer = ai,
                IgnoreCameraView = true
            };
            entMan.EventBus.RaiseLocalEvent(ai, shunt);
            Assert.That(entMan.GetComponent<MindComponent>(mindId).OwnedEntity, Is.EqualTo(brain));

            var maps = entMan.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            borg = entMan.Spawn("BorgChassisGeneric", new MapCoordinates(0, 0, mapId));
            var chassis = entMan.GetComponent<BorgChassisComponent>(borg);
            Assert.That(containers.Insert(brain, chassis.BrainContainer), Is.True);
        });
        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            var mind = entMan.GetComponent<MindComponent>(mindId);
            var shuntable = entMan.GetComponent<StationAIShuntableComponent>(ai);
            var borgShunt = entMan.GetComponent<StationAIShuntComponent>(borg);
            var brainShunt = entMan.GetComponent<StationAIShuntComponent>(brain);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(mind.OwnedEntity, Is.EqualTo(borg));
                Assert.That(entMan.GetComponent<StationAiHolderComponent>(cores[0]).Slot.Item, Is.EqualTo(ai));
                Assert.That(shuntable.Inhabited, Is.EqualTo(borg));
                Assert.That(borgShunt.Return, Is.EqualTo(ai));
                Assert.That(borgShunt.ReturnAction, Is.Not.Null);
                Assert.That(brainShunt.Return, Is.EqualTo(ai));
                Assert.That(brainShunt.ReturnAction, Is.Not.Null);
                AssertStationAiJobSlots(entMan, station, 0);
            }
        });

        await DisconnectReconnect(pair);
        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            var mind = entMan.GetComponent<MindComponent>(mindId);
            var shuntable = entMan.GetComponent<StationAIShuntableComponent>(ai);
            var borgShunt = entMan.GetComponent<StationAIShuntComponent>(borg);
            var brainShunt = entMan.GetComponent<StationAIShuntComponent>(brain);
            var actions = entMan.System<SharedActionsSystem>().GetActions(ai).ToArray();
            var entityTargetActions = entMan.GetEntityQuery<EntityTargetActionComponent>();
            var instantActions = entMan.GetEntityQuery<InstantActionComponent>();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(mind.OwnedEntity, Is.EqualTo(borg));
                Assert.That(shuntable.Inhabited, Is.EqualTo(borg));
                Assert.That(borgShunt.Return, Is.EqualTo(ai));
                Assert.That(borgShunt.ReturnAction, Is.Not.Null);
                Assert.That(brainShunt.Return, Is.EqualTo(ai));
                Assert.That(brainShunt.ReturnAction, Is.Not.Null);
                Assert.That(
                    actions.Any(action => entityTargetActions.CompOrNull(action)?.Event is AIShuntActionEvent),
                    Is.True);
                Assert.That(
                    actions.Any(action => instantActions.CompOrNull(action)?.Event is AIReconnectShuntActionEvent),
                    Is.True);
            }

            var core = entMan.GetComponent<StationAiCoreComponent>(cores[0]);
            if (deleteRemoteEye)
            {
                var remoteEye = core.RemoteEntity
                    ?? throw new InvalidOperationException("Station AI core did not have a remote eye.");
                entMan.DeleteEntity(remoteEye);
            }
            else
            {
                // A disconnected core can retain an invalid remote-eye UID.
                core.RemoteEntity = EntityUid.Invalid;
            }

            var followerCoordinates = entMan.System<SharedTransformSystem>().GetMapCoordinates(borg);
            follower = entMan.Spawn(GameTicker.ObserverPrototypeName, followerCoordinates);
            entMan.System<FollowerSystem>().StartFollowingEntity(follower, borg);
            entMan.EventBus.RaiseLocalEvent(borg, new AIUnShuntActionEvent { Performer = borg });
        });
        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(entMan.GetComponent<MindComponent>(mindId).OwnedEntity, Is.EqualTo(ai));
                Assert.That(entMan.GetComponent<StationAIShuntableComponent>(ai).Inhabited, Is.Null);
                Assert.That(entMan.GetComponent<StationAIShuntComponent>(borg).Return, Is.Null);
                Assert.That(entMan.GetComponent<StationAIShuntComponent>(borg).ReturnAction, Is.Null);
                Assert.That(entMan.GetComponent<StationAIShuntComponent>(brain).Return, Is.Null);
                Assert.That(entMan.GetComponent<StationAIShuntComponent>(brain).ReturnAction, Is.Null);
            }

            entMan.EventBus.RaiseLocalEvent(ai, new AIShuntActionEvent
            {
                Target = borg,
                Performer = ai,
                IgnoreCameraView = true
            });
        });
        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(entMan.GetComponent<MindComponent>(mindId).OwnedEntity, Is.EqualTo(borg));
                Assert.That(entMan.GetComponent<StationAIShuntableComponent>(ai).Inhabited, Is.EqualTo(borg));
                Assert.That(entMan.GetComponent<StationAIShuntComponent>(borg).Return, Is.EqualTo(ai));
                Assert.That(entMan.GetComponent<StationAIShuntComponent>(brain).Return, Is.EqualTo(ai));
            }

            entMan.EventBus.RaiseLocalEvent(borg, new AIUnShuntActionEvent { Performer = borg });
        });
        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(entMan.GetComponent<MindComponent>(mindId).OwnedEntity, Is.EqualTo(ai));
                Assert.That(entMan.GetComponent<StationAIShuntableComponent>(ai).Inhabited, Is.Null);
                Assert.That(entMan.GetComponent<StationAIShuntComponent>(borg).Return, Is.Null);
                Assert.That(entMan.GetComponent<StationAIShuntComponent>(brain).Return, Is.Null);
            }

            var remoteEye = entMan.GetComponent<StationAiCoreComponent>(cores[0]).RemoteEntity;
            Assert.That(remoteEye, Is.Not.Null);
            Assert.That(remoteEye!.Value, Is.Not.EqualTo(EntityUid.Invalid));
            Assert.That(entMan.HasComponent<TransformComponent>(remoteEye.Value), Is.True);
            Assert.That(entMan.GetComponent<FollowerComponent>(follower).Following, Is.EqualTo(remoteEye.Value));
        });
    }

    [Test]
    public async Task StationAiEjectsFromCoreWhenMindLeavesShuntedBorg()
    {
        var (pair, station, cores) = await SetupAiStation(1);
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();
        var (mindId, _) = GetMind(pair);
        StationAIShuntableComponent shuntable = default!;
        EntityUid ai = default;
        EntityUid borg = default;

        await pair.Server.WaitAssertion(() =>
        {
            var userId = entMan.GetComponent<MindComponent>(mindId).UserId!.Value;
            Assert.That(entMan.System<StationJobsSystem>().TryAssignJob(station, "StationAi", userId), Is.True);

            var containers = entMan.System<SharedContainerSystem>();
            ai = entMan.SpawnEntity("StationAiBrainConstructed", MapCoordinates.Nullspace);
            if (!containers.TryGetContainer(cores[0], StationAiCoreComponent.Container, out var coreContainer))
                throw new InvalidOperationException("Station AI core container was not initialized.");
            Assert.That(containers.Insert(ai, coreContainer), Is.True);
            entMan.System<MindSystem>().TransferTo(mindId, ai);
            shuntable = entMan.GetComponent<StationAIShuntableComponent>(ai);

            borg = ShuntAiIntoBorg(entMan, ai);
        });
        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<MindComponent>(mindId).OwnedEntity, Is.EqualTo(borg));
            Assert.That(entMan.GetComponent<StationAiHolderComponent>(cores[0]).Slot.Item, Is.EqualTo(ai));

            var ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, MapCoordinates.Nullspace);
            entMan.System<MindSystem>().TransferTo(mindId, ghost);
        });
        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            var brain = entMan.GetComponent<BorgChassisComponent>(borg).BrainContainer.ContainedEntity
                ?? throw new InvalidOperationException("Borg chassis brain was not initialized.");
            var borgShunt = entMan.GetComponent<StationAIShuntComponent>(borg);
            var brainShunt = entMan.GetComponent<StationAIShuntComponent>(brain);
            var stationJobs = entMan.System<StationJobsSystem>();
            var userId = entMan.GetComponent<MindComponent>(mindId).UserId!.Value;

            Assert.That(entMan.GetComponent<StationAiHolderComponent>(cores[0]).Slot.Item, Is.Null);
            Assert.That(borgShunt.Return, Is.Null);
            Assert.That(borgShunt.ReturnAction, Is.Null);
            Assert.That(brainShunt.Return, Is.Null);
            Assert.That(brainShunt.ReturnAction, Is.Null);
            Assert.That(shuntable.Inhabited, Is.Null);
            Assert.That(stationJobs.TryGetPlayerJobs(station, userId, out _), Is.False);
            AssertStationAiJobSlots(entMan, station, 1);

            Assert.That(stationJobs.TryAssignJob(station, "StationAi", userId), Is.True);
            var nextAi = entMan.SpawnEntity("StationAiBrainConstructed", MapCoordinates.Nullspace);
            var containers = entMan.System<SharedContainerSystem>();
            if (!containers.TryGetContainer(cores[0], StationAiCoreComponent.Container, out var coreContainer))
                throw new InvalidOperationException("Station AI core container was not initialized.");
            Assert.That(containers.Insert(nextAi, coreContainer), Is.True);
            entMan.System<MindSystem>().TransferTo(mindId, nextAi);

            var shunt = new AIShuntActionEvent
            {
                Target = borg,
                Performer = nextAi,
                IgnoreCameraView = true
            };
            entMan.EventBus.RaiseLocalEvent(nextAi, shunt);
            Assert.That(shunt.Handled, Is.True);
        });
        await pair.RunTicksSync(5);

        Assert.That(entMan.GetComponent<MindComponent>(mindId).OwnedEntity, Is.EqualTo(borg));
    }

    [Test]
    public async Task StationAiJobSlotIsReleasedWhenAiGhostsWithoutCoreReturn()
    {
        var (pair, station, cores) = await SetupAiStation(1);
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();
        var (mindId, _) = GetMind(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var userId = entMan.GetComponent<MindComponent>(mindId).UserId!.Value;
            var stationJobs = entMan.System<StationJobsSystem>();
            Assert.That(stationJobs.TryAssignJob(station, "StationAi", userId), Is.True);

            var containers = entMan.System<SharedContainerSystem>();
            var ai = entMan.SpawnEntity("StationAiBrainConstructed", MapCoordinates.Nullspace);
            if (!containers.TryGetContainer(cores[0], StationAiCoreComponent.Container, out var coreContainer))
                throw new InvalidOperationException("Station AI core container was not initialized.");
            Assert.That(containers.Insert(ai, coreContainer), Is.True);
            entMan.System<MindSystem>().TransferTo(mindId, ai);

            AssertStationAiJobSlots(entMan, station, 0);

            var mind = entMan.GetComponent<MindComponent>(mindId);
            Assert.That(entMan.System<GhostSystem>().OnGhostAttempt(mindId, true, viaCommand: true, mind: mind), Is.True);

            Assert.That(coreContainer.ContainedEntities, Is.Empty);
            Assert.That(mind.OwnedEntity, Is.Not.Null);
            var ghost = mind.OwnedEntity!.Value;
            Assert.That(entMan.HasComponent<GhostComponent>(ghost), Is.True);
            Assert.That(entMan.GetComponent<GhostComponent>(ghost).CanReturnToBody, Is.False);
            Assert.That(stationJobs.TryGetPlayerJobs(station, userId, out _), Is.False);
            AssertStationAiJobSlots(entMan, station, 1);
        });
    }

    [Test]
    public async Task StationAiPowerLossAlertIsCancelledWhenPowerReturns()
    {
        var (pair, station, cores) = await SetupAiStation(1);
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();
        var (mindId, _) = GetMind(pair);
        var notifications = entMan.System<StationAiChatNotificationTestSystem>();
        EntityUid ai = default;

        await pair.Server.WaitAssertion(() =>
        {
            var userId = entMan.GetComponent<MindComponent>(mindId).UserId!.Value;
            Assert.That(entMan.System<StationJobsSystem>().TryAssignJob(station, "StationAi", userId), Is.True);

            ai = entMan.SpawnEntity("StationAiBrainConstructed", MapCoordinates.Nullspace);
            entMan.AddComponent<TestListenerComponent>(ai);
            var containers = entMan.System<SharedContainerSystem>();
            if (!containers.TryGetContainer(cores[0], StationAiCoreComponent.Container, out var coreContainer))
                throw new InvalidOperationException("Station AI core container was not initialized.");
            Assert.That(containers.Insert(ai, coreContainer), Is.True);
            entMan.System<MindSystem>().TransferTo(mindId, ai);

            notifications.Clear(ai);
            entMan.GetComponent<ApcPowerReceiverComponent>(cores[0]).NeedsPower = false;
        });
        await pair.RunTicksSync(60);

        await pair.Server.WaitAssertion(() =>
        {
            var battery = entMan.GetComponent<ApcPowerReceiverBatteryComponent>(cores[0]);
            Assert.That(battery.Enabled, Is.False);
            entMan.GetComponent<ApcPowerReceiverComponent>(cores[0]).NeedsPower = true;
        });
        await pair.RunTicksSync(60);

        await pair.Server.WaitAssertion(() =>
        {
            var receiver = entMan.GetComponent<ApcPowerReceiverComponent>(cores[0]);
            var battery = entMan.GetComponent<ApcPowerReceiverBatteryComponent>(cores[0]);
            Assert.That(receiver.PowerReceived, Is.LessThan(receiver.Load));
            Assert.That(battery.Enabled, Is.True);
            Assert.That(notifications.Count(ai), Is.Zero);
            receiver.NeedsPower = false;
        });
        await pair.RunTicksSync(200);

        await pair.Server.WaitAssertion(() => Assert.That(notifications.Count(ai), Is.Zero));
    }

    [Test]
    public async Task StationAiGhostingAnnouncesShutdown()
    {
        var (pair, station, cores) = await SetupAiStation(1);
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();
        var (mindId, _) = GetMind(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var userId = entMan.GetComponent<MindComponent>(mindId).UserId!.Value;
            Assert.That(entMan.System<StationJobsSystem>().TryAssignJob(station, "StationAi", userId), Is.True);

            var ai = entMan.SpawnEntity("StationAiBrainConstructed", MapCoordinates.Nullspace);
            var containers = entMan.System<SharedContainerSystem>();
            if (!containers.TryGetContainer(cores[0], StationAiCoreComponent.Container, out var coreContainer))
                throw new InvalidOperationException("Station AI core container was not initialized.");
            Assert.That(containers.Insert(ai, coreContainer), Is.True);
            entMan.System<MindSystem>().TransferTo(mindId, ai);

            var announcements = entMan.System<StationAiAnnouncementTestSystem>();
            var position = entMan.System<NavMapSystem>().GetNearestBeaconString(cores[0]);
            announcements.Clear();
            var mind = entMan.GetComponent<MindComponent>(mindId);
            Assert.That(entMan.System<GhostSystem>().OnGhostAttempt(mindId, true, viaCommand: true, mind: mind),
                Is.True);
            Assert.That(announcements.LastMessage, Does.Contain("has shut down"));
            Assert.That(announcements.LastMessage, Does.Contain(position));
        });
    }

    [Test]
    public async Task StationAiJobSlotRemainsReservedWhileAiCanReturnToCore()
    {
        var (pair, station, cores) = await SetupAiStation(1);
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();
        var (mindId, _) = GetMind(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var userId = entMan.GetComponent<MindComponent>(mindId).UserId!.Value;
            Assert.That(entMan.System<StationJobsSystem>().TryAssignJob(station, "StationAi", userId), Is.True);

            var containers = entMan.System<SharedContainerSystem>();
            var ai = entMan.SpawnEntity("StationAiBrainConstructed", MapCoordinates.Nullspace);
            if (!containers.TryGetContainer(cores[0], StationAiCoreComponent.Container, out var coreContainer))
                throw new InvalidOperationException("Station AI core container was not initialized.");
            Assert.That(containers.Insert(ai, coreContainer), Is.True);
            entMan.System<MindSystem>().TransferTo(mindId, ai);

            var core = entMan.GetComponent<StationAiCoreComponent>(cores[0]);
            entMan.System<StationAiSystem>().KillHeldAi((cores[0], core));

            var mind = entMan.GetComponent<MindComponent>(mindId);
            Assert.That(mind.OwnedEntity, Is.EqualTo(ai));
            Assert.That(mind.VisitingEntity, Is.Not.Null);
            Assert.That(entMan.GetComponent<GhostComponent>(mind.VisitingEntity!.Value).CanReturnToBody, Is.True);
            AssertStationAiJobSlots(entMan, station, 0);
        });
    }

    [Test]
    public async Task StationAiCoreCanKillAiWhileShunted()
    {
        var (pair, station, cores) = await SetupAiStation(1);
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();
        var (mindId, _) = GetMind(pair);
        EntityUid ai = default;
        EntityUid borg = default;

        await pair.Server.WaitAssertion(() =>
        {
            var userId = entMan.GetComponent<MindComponent>(mindId).UserId!.Value;
            Assert.That(entMan.System<StationJobsSystem>().TryAssignJob(station, "StationAi", userId), Is.True);

            var containers = entMan.System<SharedContainerSystem>();
            ai = entMan.SpawnEntity("StationAiBrainConstructed", MapCoordinates.Nullspace);
            if (!containers.TryGetContainer(cores[0], StationAiCoreComponent.Container, out var coreContainer))
                throw new InvalidOperationException("Station AI core container was not initialized.");
            Assert.That(containers.Insert(ai, coreContainer), Is.True);
            entMan.System<MindSystem>().TransferTo(mindId, ai);

            borg = ShuntAiIntoBorg(entMan, ai);
        });
        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<MindComponent>(mindId).OwnedEntity, Is.EqualTo(borg));
            Assert.That(entMan.GetComponent<StationAiHolderComponent>(cores[0]).Slot.Item, Is.EqualTo(ai));

            var core = entMan.GetComponent<StationAiCoreComponent>(cores[0]);
            entMan.System<StationAiSystem>().KillHeldAi((cores[0], core));

            var mind = entMan.GetComponent<MindComponent>(mindId);
            Assert.That(mind.OwnedEntity, Is.EqualTo(ai));
            Assert.That(entMan.GetComponent<StationAIShuntableComponent>(ai).Inhabited, Is.Null);
            Assert.That(entMan.GetComponent<MobStateComponent>(ai).CurrentState, Is.EqualTo(MobState.Dead));
            Assert.That(mind.VisitingEntity, Is.Not.Null);
            Assert.That(entMan.GetComponent<GhostComponent>(mind.VisitingEntity!.Value).CanReturnToBody, Is.True);
            AssertStationAiJobSlots(entMan, station, 0);
        });
    }

    [Test]
    public async Task StationAiJobSlotReturnsWhenDeadAiStartsNewLife()
    {
        var (pair, station, cores) = await SetupAiStation(1);
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();
        var (mindId, _) = GetMind(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var userId = entMan.GetComponent<MindComponent>(mindId).UserId!.Value;
            var stationJobs = entMan.System<StationJobsSystem>();
            Assert.That(stationJobs.TryAssignJob(station, "StationAi", userId), Is.True);

            var containers = entMan.System<SharedContainerSystem>();
            var ai = entMan.SpawnEntity("StationAiBrainConstructed", MapCoordinates.Nullspace);
            if (!containers.TryGetContainer(cores[0], StationAiCoreComponent.Container, out var coreContainer))
                throw new InvalidOperationException("Station AI core container was not initialized.");
            Assert.That(containers.Insert(ai, coreContainer), Is.True);
            var mindSystem = entMan.System<MindSystem>();
            mindSystem.TransferTo(mindId, ai);

            entMan.System<MobStateSystem>().ChangeMobState(ai, MobState.Dead);
            var mind = entMan.GetComponent<MindComponent>(mindId);
            Assert.That(entMan.System<GhostSystem>().OnGhostAttempt(mindId, true, viaCommand: true, mind: mind), Is.True);
            Assert.That(mind.VisitingEntity, Is.Not.Null);
            AssertStationAiJobSlots(entMan, station, 0);

            mindSystem.WipeMind(mindId, mind);

            Assert.That(coreContainer.ContainedEntities, Is.Empty);
            Assert.That(stationJobs.TryGetPlayerJobs(station, userId, out _), Is.False);
            AssertStationAiJobSlots(entMan, station, 1);
        });
    }

    [Test]
    public async Task StationAiRefreshRestoresMissingSlotWhenCoreIsEmptyAndUnreserved()
    {
        var (pair, station, _) = await SetupAiStation(1);
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();

        await pair.Server.WaitAssertion(() =>
        {
            var stationJobs = entMan.System<StationJobsSystem>();
            Assert.That(stationJobs.TrySetJobSlot(station, "StationAi", 0), Is.True);

            entMan.System<ContainerSpawnJobSlotSystem>().RefreshJobSlots();

            AssertStationAiJobSlots(entMan, station, 1);
        });
    }

    [Test]
    public async Task StationAiRefreshKeepsSlotReservedForIntellicardAi()
    {
        var (pair, _, _, _, station) = await SetupAiHolder();
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();

        await pair.Server.WaitAssertion(() =>
        {
            entMan.System<ContainerSpawnJobSlotSystem>().RefreshJobSlots();

            AssertStationAiJobSlots(entMan, station, 0);
        });
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
    public async Task StationAiRemainsInHolderWhenPlayerDisconnects()
    {
        var (pair, holder, ai, mindId, station) = await SetupAiHolder();
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();
        var userId = entMan.GetComponent<MindComponent>(mindId).UserId!.Value;

        await pair.Server.WaitAssertion(() =>
        {
            var ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, MapCoordinates.Nullspace);
            entMan.System<MindSystem>().Visit(mindId, ghost);
        });
        await pair.RunTicksSync(5);
        Assert.That(entMan.GetComponent<StationAiHolderComponent>(holder).Slot.Item, Is.EqualTo(ai));

        await Disconnect(pair);

        Assert.That(entMan.GetComponent<StationAiHolderComponent>(holder).Slot.Item, Is.EqualTo(ai));
        Assert.That(entMan.System<StationJobsSystem>().TryGetPlayerJobs(station, userId, out _), Is.True);
        AssertStationAiJobSlots(entMan, station, 0);
    }

    [Test]
    public async Task StationAiInBorgRemainsInCoreWhenPlayerDisconnects()
    {
        var (pair, station, cores) = await SetupAiStation(1);
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();
        var (mindId, _) = GetMind(pair);
        NetUserId userId = default;
        EntityUid ai = default;
        EntityUid borg = default;

        await pair.Server.WaitAssertion(() =>
        {
            userId = entMan.GetComponent<MindComponent>(mindId).UserId!.Value;
            Assert.That(entMan.System<StationJobsSystem>().TryAssignJob(station, "StationAi", userId), Is.True);

            var containers = entMan.System<SharedContainerSystem>();
            ai = entMan.SpawnEntity("StationAiBrainConstructed", MapCoordinates.Nullspace);
            if (!containers.TryGetContainer(cores[0], StationAiCoreComponent.Container, out var coreContainer))
                throw new InvalidOperationException("Station AI core container was not initialized.");
            Assert.That(containers.Insert(ai, coreContainer), Is.True);
            entMan.System<MindSystem>().TransferTo(mindId, ai);
            borg = ShuntAiIntoBorg(entMan, ai);
        });
        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<MindComponent>(mindId).OwnedEntity, Is.EqualTo(borg));
            Assert.That(entMan.GetComponent<StationAiHolderComponent>(cores[0]).Slot.Item, Is.EqualTo(ai));
            AssertStationAiJobSlots(entMan, station, 0);
        });

        await Disconnect(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var stationJobs = entMan.System<StationJobsSystem>();
            Assert.That(entMan.GetComponent<StationAiHolderComponent>(cores[0]).Slot.Item, Is.EqualTo(ai));
            Assert.That(stationJobs.TryGetPlayerJobs(station, userId, out _), Is.True);
            AssertStationAiJobSlots(entMan, station, 0);
        });
    }

    [Test]
    public async Task StationAiAvailableSlotsAreCappedByEmptyCores()
    {
        var (pair, station, cores) = await SetupAiStation(2);
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.System<StationSystem>().GetOwningStation(cores[0]), Is.EqualTo(station));
            AssertStationAiJobSlots(entMan, station, 2);

            var containers = entMan.System<SharedContainerSystem>();
            for (var i = 0; i < cores.Length; i++)
            {
                var ai = entMan.SpawnEntity("StationAiBrainConstructed", MapCoordinates.Nullspace);
                if (!containers.TryGetContainer(cores[i], StationAiCoreComponent.Container, out var container))
                    throw new InvalidOperationException("Station AI core container was not initialized.");
                Assert.That(containers.Insert(ai, container), Is.True);
                Assert.That(container.ContainedEntities.Count, Is.EqualTo(1));
                AssertStationAiJobSlots(entMan, station, cores.Length - i - 1);
            }
        });
    }

    private async Task<(TestPair Pair, EntityUid Holder, EntityUid Ai, EntityUid Mind, EntityUid Station)> SetupAiHolder()
    {
        var (pair, station, cores) = await SetupAiStation(1);
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();
        var (mindId, _) = GetMind(pair);
        EntityUid ai = default;
        EntityUid holder = default;

        await pair.Server.WaitAssertion(() =>
        {
            var stationJobs = entMan.System<StationJobsSystem>();
            var containers = entMan.System<SharedContainerSystem>();
            var userId = entMan.GetComponent<MindComponent>(mindId).UserId!.Value;
            Assert.That(stationJobs.TryAssignJob(station, "StationAi", userId), Is.True);

            ai = entMan.SpawnEntity("StationAiBrainConstructed", MapCoordinates.Nullspace);
            if (!containers.TryGetContainer(cores[0], StationAiCoreComponent.Container, out var coreContainer))
                throw new InvalidOperationException("Station AI core container was not initialized.");
            Assert.That(containers.Insert(ai, coreContainer), Is.True);
            entMan.System<MindSystem>().TransferTo(mindId, ai);
            AssertStationAiJobSlots(entMan, station, 0);

            holder = entMan.SpawnEntity("Intellicard", MapCoordinates.Nullspace);
            var aiHolder = entMan.GetComponent<StationAiHolderComponent>(holder);
            var slotContainer = aiHolder.Slot.ContainerSlot
                ?? throw new InvalidOperationException("Intellicard AI container was not initialized.");

            Assert.That(containers.Insert(ai, slotContainer), Is.True);
        });
        await pair.RunTicksSync(5);

        return (pair, holder, ai, mindId, station);
    }

    private async Task<(TestPair Pair, EntityUid Station, EntityUid[] Cores)> SetupAiStation(int coreCount)
    {
        var pair = await SetupPair();
        var entMan = pair.Server.ResolveDependency<IServerEntityManager>();
        EntityUid station = default;
        var cores = new EntityUid[coreCount];

        await pair.Server.WaitAssertion(() =>
        {
            var map = pair.Server.ResolveDependency<IPrototypeManager>().Index<GameMapPrototype>(StationAiTestMapId);
            station = entMan.System<StationSystem>().InitializeNewStation(map.Stations["Empty"], null, "Station AI test");
        });
        await pair.RunTicksSync(1);

        await pair.Server.WaitAssertion(() =>
        {
            for (var i = 0; i < cores.Length; i++)
            {
                cores[i] = entMan.SpawnEntity("PlayerStationAi", MapCoordinates.Nullspace);
                var tracker = entMan.AddComponent<StationTrackerComponent>(cores[i]);
                entMan.System<StationSystem>().SetStation((cores[i], tracker), station);
            }
        });
        await pair.RunTicksSync(1);

        await pair.Server.WaitAssertion(() =>
            Assert.That(entMan.System<StationJobsSystem>().TrySetJobSlot(station, "StationAi", coreCount, true), Is.True));

        return (pair, station, cores);
    }

    private static void AssertStationAiJobSlots(IServerEntityManager entMan, EntityUid station, int expectedSlots)
    {
        Assert.That(
            entMan.System<StationJobsSystem>().TryGetJobSlot(station, "StationAi", out var slots),
            Is.True);
        Assert.That(slots, Is.EqualTo(expectedSlots), "Unexpected number of available Station AI slots.");
    }

    private static EntityUid ShuntAiIntoBorg(IServerEntityManager entMan, EntityUid ai)
    {
        var maps = entMan.System<SharedMapSystem>();
        maps.CreateMap(out var mapId);
        var borg = entMan.SpawnEntity("BorgChassisGeneric", new MapCoordinates(0, 0, mapId));
        var chassis = entMan.GetComponent<BorgChassisComponent>(borg);
        var brain = entMan.SpawnEntity("AiInterfaceCube", MapCoordinates.Nullspace);
        Assert.That(entMan.System<SharedContainerSystem>().Insert(brain, chassis.BrainContainer), Is.True);

        var shunt = new AIShuntActionEvent
        {
            Target = borg,
            Performer = ai,
            IgnoreCameraView = true
        };
        entMan.EventBus.RaiseLocalEvent(ai, shunt);
        return borg;
    }

    public sealed class StationAiChatNotificationTestSystem : TestListenerSystem<ChatNotificationEvent>
    {
    }

    public sealed class StationAiAnnouncementTestSystem : EntitySystem
    {
        public string LastMessage { get; private set; } = string.Empty;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<AnnouncementSpokeEvent>(OnAnnouncement);
        }

        public void Clear() => LastMessage = string.Empty;

        private void OnAnnouncement(AnnouncementSpokeEvent args) => LastMessage = args.Message.Text;
    }
}
