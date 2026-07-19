using System.Collections.Generic;
using Content.Server._Sol.Medical.Virology;
using Content.Server.DeviceNetwork;
using Content.Server.Power.Components;
using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Sol.Medical.Virology;

[TestFixture]
[TestOf(typeof(SterilizationAirlockSystem))]
public sealed class SterilizationAirlockControllerTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: SolSterilizerTestDoor
  parent: Airlock
  components:
  - type: ApcPowerReceiver
    needsPower: false

- type: entity
  id: SolSterilizerTestController
  parent: SolSterilizationAirlockController
  components:
  - type: ApcPowerReceiver
    needsPower: false
  - type: SterilizationAirlockController
    fogDuration: 0.2
    fadeDuration: 0.1
    closingTimeout: 2
";

    [Test]
    public async Task CycleSterilizesChamberAndOpensExit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = entMan.System<SharedMapSystem>();
        var doors = entMan.System<SharedDoorSystem>();
        var sterilizer = entMan.System<SterilizationAirlockSystem>();
        var gridPathogen = entMan.System<GridPathogenAtmosphereSystem>();

        await server.WaitAssertion(() =>
        {
            var gridUid = testMap.Grid.Owner;
            var gridComp = testMap.Grid.Comp;
            var tiles = new List<(Vector2i Index, Tile Tile)>
            {
                new(new Vector2i(0, 0), new Tile(1)),
                new(new Vector2i(1, 0), new Tile(1)),
                new(new Vector2i(2, 0), new Tile(1)),
            };
            map.SetTiles(gridUid, gridComp, tiles);

            var doorA = entMan.SpawnEntity("SolSterilizerTestDoor", new EntityCoordinates(gridUid, 0.5f, 0.5f));
            var controller = entMan.SpawnEntity("SolSterilizerTestController", new EntityCoordinates(gridUid, 1.5f, 0.5f));
            var doorB = entMan.SpawnEntity("SolSterilizerTestDoor", new EntityCoordinates(gridUid, 2.5f, 0.5f));
            var tool = entMan.SpawnEntity("Scalpel", new EntityCoordinates(gridUid, 1.5f, 0.5f));

            doors.SetState(doorA, DoorState.Closed);
            doors.SetState(doorB, DoorState.Closed);
            entMan.GetComponent<ApcPowerReceiverComponent>(doorA).Powered = true;
            entMan.GetComponent<ApcPowerReceiverComponent>(doorB).Powered = true;

            var controllerComp = entMan.GetComponent<SterilizationAirlockControllerComponent>(controller);
            controllerComp.DoorA = doorA;
            controllerComp.DoorB = doorB;
            controllerComp.EntranceDoor = doorA;
            controllerComp.ExitDoor = doorB;
            controllerComp.RequiresPower = false;

            var sterility = entMan.EnsureComponent<SurgicalToolSterilityComponent>(tool);
            sterility.State = SurgicalSterilityState.Dirty;
            sterility.Contaminants.Add(new PathogenContaminationEntry
            {
                PathogenId = "SolPathogenFlu",
                Load = 3f,
            });
            var surface = entMan.EnsureComponent<SurfaceContaminationComponent>(tool);
            surface.IsDirty = true;
            surface.Contaminants.Add(new PathogenContaminationEntry
            {
                PathogenId = "SolPathogenFlu",
                Load = 3f,
            });

            var midTile = new Vector2i(1, 0);
            gridPathogen.AddAirborneLoad(controller, "SolPathogenFlu", 5f);
            Assert.That(gridPathogen.GetAirborneLoad(gridUid, midTile), Is.GreaterThan(0f));

            Assert.That(sterilizer.TryBeginCycle((controller, controllerComp)), Is.True);
            Assert.That(controllerComp.Phase, Is.EqualTo(SterilizationControllerPhase.Closing));
            Assert.That(entMan.HasComponent<SterilizationDoorLockComponent>(doorA), Is.True);
            Assert.That(entMan.HasComponent<SterilizationDoorLockComponent>(doorB), Is.True);

            // Both doors closed: bolt, then sterilize under lock.
            sterilizer.Update(0.1f);
            Assert.That(controllerComp.Phase, Is.EqualTo(SterilizationControllerPhase.Fogging));
            Assert.That(entMan.GetComponent<DoorBoltComponent>(doorA).BoltsDown, Is.True);
            Assert.That(entMan.GetComponent<DoorBoltComponent>(doorB).BoltsDown, Is.True);

            controllerComp.Phase = SterilizationControllerPhase.Fading;
            controllerComp.PhaseEndsAt = timing.CurTime;
            sterilizer.Update(0.1f);

            Assert.That(entMan.GetComponent<DoorBoltComponent>(doorA).BoltsDown, Is.True,
                "Entrance remains bolted through and after sterilization");
            Assert.That(entMan.GetComponent<DoorBoltComponent>(doorB).BoltsDown, Is.False,
                "Exit unbolts only after sterilization completes");

            doors.SetState(doorB, DoorState.Open);
            controllerComp = entMan.GetComponent<SterilizationAirlockControllerComponent>(controller);
            if (controllerComp.Phase == SterilizationControllerPhase.OpeningExit)
            {
                controllerComp.PhaseEndsAt = timing.CurTime;
                sterilizer.Update(0.1f);
            }

            controllerComp = entMan.GetComponent<SterilizationAirlockControllerComponent>(controller);
            Assert.That(controllerComp.Phase, Is.EqualTo(SterilizationControllerPhase.Idle));
            Assert.That(gridPathogen.GetAirborneLoad(gridUid, midTile), Is.EqualTo(0f));
            Assert.That(entMan.GetComponent<SurgicalToolSterilityComponent>(tool).State,
                Is.EqualTo(SurgicalSterilityState.Sterile));
            Assert.That(entMan.GetComponent<SurfaceContaminationComponent>(tool).Contaminants, Is.Empty);
            Assert.That(entMan.HasComponent<SterilizationDoorLockComponent>(doorA), Is.False);
            Assert.That(entMan.HasComponent<SterilizationDoorLockComponent>(doorB), Is.False);
            Assert.That(entMan.GetComponent<DoorBoltComponent>(doorA).BoltsDown, Is.True,
                "Opposite door stays bolted while the exit remains open");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RejectsOpenDuringCycleAndAbortsWithoutPower()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = entMan.System<SharedMapSystem>();
        var doors = entMan.System<SharedDoorSystem>();
        var sterilizer = entMan.System<SterilizationAirlockSystem>();

        await server.WaitAssertion(() =>
        {
            var gridUid = testMap.Grid.Owner;
            var gridComp = testMap.Grid.Comp;
            var tiles = new List<(Vector2i Index, Tile Tile)>
            {
                new(new Vector2i(0, 0), new Tile(1)),
                new(new Vector2i(1, 0), new Tile(1)),
                new(new Vector2i(2, 0), new Tile(1)),
            };
            map.SetTiles(gridUid, gridComp, tiles);

            var doorA = entMan.SpawnEntity("SolSterilizerTestDoor", new EntityCoordinates(gridUid, 0.5f, 0.5f));
            var controller = entMan.SpawnEntity("SolSterilizerTestController", new EntityCoordinates(gridUid, 1.5f, 0.5f));
            var doorB = entMan.SpawnEntity("SolSterilizerTestDoor", new EntityCoordinates(gridUid, 2.5f, 0.5f));

            var controllerComp = entMan.GetComponent<SterilizationAirlockControllerComponent>(controller);
            controllerComp.DoorA = doorA;
            controllerComp.DoorB = doorB;
            controllerComp.RequiresPower = false;
            entMan.GetComponent<ApcPowerReceiverComponent>(doorA).Powered = true;
            entMan.GetComponent<ApcPowerReceiverComponent>(doorB).Powered = true;

            var doorOpen = new NetworkPayload
            {
                [DeviceNetworkConstants.LogicState] = SignalState.High,
            };
            var doorOpenSignal = new SignalReceivedEvent(
                SterilizationAirlockSystem.DoorAPort,
                doorA,
                doorOpen);
            entMan.EventBus.RaiseLocalEvent(controller, ref doorOpenSignal);
            Assert.That(entMan.GetComponent<DoorBoltComponent>(doorB).BoltsDown, Is.True);

            var doorClosed = new NetworkPayload
            {
                [DeviceNetworkConstants.LogicState] = SignalState.Low,
            };
            var doorClosedSignal = new SignalReceivedEvent(
                SterilizationAirlockSystem.DoorAPort,
                doorA,
                doorClosed);
            entMan.EventBus.RaiseLocalEvent(controller, ref doorClosedSignal);
            Assert.That(entMan.GetComponent<DoorBoltComponent>(doorB).BoltsDown, Is.False);

            var quarantineOn = new NetworkPayload
            {
                [DeviceNetworkConstants.LogicState] = SignalState.High,
            };
            var lockSignal = new SignalReceivedEvent(
                SterilizationAirlockSystem.QuarantineLockPort,
                Data: quarantineOn);
            entMan.EventBus.RaiseLocalEvent(controller, ref lockSignal);
            Assert.That(controllerComp.QuarantineLocked, Is.True);
            Assert.That(entMan.GetComponent<DoorBoltComponent>(doorA).BoltsDown, Is.True);

            var quarantineOff = new NetworkPayload
            {
                [DeviceNetworkConstants.LogicState] = SignalState.Low,
            };
            var unlockSignal = new SignalReceivedEvent(
                SterilizationAirlockSystem.QuarantineLockPort,
                Data: quarantineOff);
            entMan.EventBus.RaiseLocalEvent(controller, ref unlockSignal);
            Assert.That(controllerComp.QuarantineLocked, Is.False);
            Assert.That(entMan.GetComponent<DoorBoltComponent>(doorA).BoltsDown, Is.False);

            // Closing the inner door (Door B when quarantine is Door A) always starts a cycle,
            // even if EntranceDoor was cleared after a previous transit.
            controllerComp.EntranceDoor = null;
            doors.SetState(doorA, DoorState.Closed);
            doors.SetState(doorB, DoorState.Closed);
            var innerClose = new NetworkPayload
            {
                [DeviceNetworkConstants.LogicState] = SignalState.Low,
            };
            var innerCloseSignal = new SignalReceivedEvent(
                SterilizationAirlockSystem.DoorBPort,
                doorB,
                innerClose);
            entMan.EventBus.RaiseLocalEvent(controller, ref innerCloseSignal);
            Assert.That(controllerComp.EntranceDoor, Is.EqualTo(doorB));
            Assert.That(controllerComp.Phase, Is.EqualTo(SterilizationControllerPhase.Closing));
            Assert.That(controllerComp.ExitDoor, Is.EqualTo(doorA));
            Assert.That(controllerComp.OpenExitAfterSterilization, Is.True);

            // Follow-up cleanse after inbound entry through the outer door must not reopen outer.
            controllerComp.Phase = SterilizationControllerPhase.Idle;
            controllerComp.EntranceDoor = null;
            controllerComp.ExitDoor = null;
            controllerComp.AwaitingInnerResterilize = true;
            entMan.RemoveComponent<SterilizationDoorLockComponent>(doorA);
            entMan.RemoveComponent<SterilizationDoorLockComponent>(doorB);

            var followUpClose = new SignalReceivedEvent(
                SterilizationAirlockSystem.DoorBPort,
                doorB,
                innerClose);
            entMan.EventBus.RaiseLocalEvent(controller, ref followUpClose);
            Assert.That(controllerComp.OpenExitAfterSterilization, Is.False);
            Assert.That(controllerComp.AwaitingInnerResterilize, Is.False);
            Assert.That(controllerComp.Phase, Is.EqualTo(SterilizationControllerPhase.Closing));

            doors.SetState(doorA, DoorState.Closed);
            doors.SetState(doorB, DoorState.Closed);
            sterilizer.Update(0.1f);
            Assert.That(controllerComp.Phase, Is.EqualTo(SterilizationControllerPhase.Fogging));

            controllerComp.Phase = SterilizationControllerPhase.Fading;
            controllerComp.PhaseEndsAt = timing.CurTime;
            sterilizer.Update(0.1f);
            Assert.That(controllerComp.Phase, Is.EqualTo(SterilizationControllerPhase.Idle));
            Assert.That(entMan.GetComponent<DoorComponent>(doorA).State, Is.EqualTo(DoorState.Closed));
            Assert.That(entMan.GetComponent<DoorComponent>(doorB).State, Is.EqualTo(DoorState.Closed));
            // Follow-up cleanse must not reopen the outer door, but must release bolts afterward.
            Assert.That(entMan.GetComponent<DoorBoltComponent>(doorA).BoltsDown, Is.False);
            Assert.That(entMan.GetComponent<DoorBoltComponent>(doorB).BoltsDown, Is.False);

            controllerComp.RequiresPower = true;
            Assert.That(entMan.TryGetComponent(controller, out ApcPowerReceiverComponent power), Is.True);
            power!.Powered = false;

            sterilizer.Update(0.1f);
            Assert.That(controllerComp.Phase, Is.EqualTo(SterilizationControllerPhase.Idle));
        });

        await pair.CleanReturnAsync();
    }
}
