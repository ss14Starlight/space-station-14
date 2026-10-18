#nullable enable
using System.Numerics;
using Content.IntegrationTests.Tests.Movement;
using Content.Server._Starlight.Computers.RemoteControl;
using Content.Server.Power.EntitySystems;
using Content.Server.Silicons.Borgs;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.Silicons.Borgs;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mind;
using Content.Shared.Movement.Components;
using Content.Shared.Power.Components;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.RemoteControl;

[TestFixture]
[TestOf(typeof(RemoteControlConsoleSystem))]
public sealed class RemoteControlBorgMovementTest : MovementTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
            id: RemoteControlBorgMovementTestAI
            parent: MobHuman
            components:
            -   type: StationAIShuntable
            -   type: RemoteControlConsole
            -   type: UserInterface
                interfaces:
                    enum.RemoteControlUIKey.Key:
                        type: RemoteControlConsoleBoundUserInterface
        - type: entity
            id: RemoteControlBorgMovementTestInterface
            parent: AiInterfaceCube
            components:
            -   type: RemoteControlBrain
        """;

    protected override string PlayerPrototype => "RemoteControlBorgMovementTestAI";
    protected override int Tiles => 7;

    [Test]
    public async Task RemoteControlDoesNotPreventShuntedBorgMovement()
    {
        EntityUid controlledBorg = default;
        EntityUid controlledBrain = default;
        EntityUid untouchedBorg = default;
        EntityUid mindId = default;
        var moverPresentAfterRemoteControl = false;
        var moverPresentAfterShunt = false;
        var controlledBorgMoved = false;
        var untouchedBorgMoved = false;

        await Server.WaitAssertion(() =>
        {
            var coordinates = SEntMan.GetCoordinates(PlayerCoords);
            controlledBorg = SpawnBorg(coordinates.Offset(new Vector2(4f, 0f)), out controlledBrain);
            untouchedBorg = SpawnBorg(coordinates.Offset(new Vector2(-4f, 0f)), out _);

            var mindSystem = Server.System<SharedMindSystem>();
            mindId = mindSystem.CreateMind(ServerSession.UserId);
            mindSystem.TransferTo(mindId, SPlayer);
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var remoteControl = new AIRemoteControlActionEvent
            {
                Target = controlledBorg
            };
            SEntMan.EventBus.RaiseLocalEvent(SPlayer, remoteControl);

            var console = SEntMan.GetComponent<RemoteControlConsoleComponent>(SPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(remoteControl.Handled, Is.True, "The AI remote-control action should take control of the first borg.");
                Assert.That(console.Controller, Is.EqualTo(SPlayer));
                Assert.That(SEntMan.GetComponent<RelayInputMoverComponent>(SPlayer).RelayEntity, Is.EqualTo(controlledBorg));
            });
        });

        await RunTicks(1);

        await Server.WaitAssertion(() =>
        {
            SEntMan.EventBus.RaiseLocalEvent(SPlayer,
                new BoundUIClosedEvent(RemoteControlUIKey.Key, SPlayer, SPlayer));
        });

        await RunTicks(1);

        await Server.WaitAssertion(() =>
        {
            var console = SEntMan.GetComponent<RemoteControlConsoleComponent>(SPlayer);
            Assert.That(console.Controller, Is.Null, "Remote control should be released before shunting.");
            moverPresentAfterRemoteControl = SEntMan.HasComponent<InputMoverComponent>(controlledBorg);

            var shunt = new AIShuntActionEvent
            {
                Target = controlledBorg,
                IgnoreCameraView = true
            };
            SEntMan.EventBus.RaiseLocalEvent(SPlayer, shunt);
            Assert.That(shunt.Handled, Is.True, "The AI should be able to shunt into the previously controlled borg.");
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            moverPresentAfterShunt = SEntMan.HasComponent<InputMoverComponent>(controlledBorg);
        });

        var controlledBorgStart = Vector2.Zero;
        await Server.WaitAssertion(() => controlledBorgStart = Transform.GetWorldPosition(controlledBorg));
        await Move(DirectionFlag.West, 1f);
        await Server.WaitAssertion(() =>
            controlledBorgMoved = Transform.GetWorldPosition(controlledBorg).X < controlledBorgStart.X - 0.1f);

        await Server.WaitAssertion(() =>
        {
            var mindSystem = Server.System<SharedMindSystem>();
            // Keep unshunting out of this movement regression while restoring the AI for the control case.
            mindSystem.TransferTo(mindId, SPlayer);
            SEntMan.GetComponent<StationAIShuntableComponent>(SPlayer).Inhabited = null;
            var borgShunt = SEntMan.GetComponent<StationAIShuntComponent>(controlledBorg);
            borgShunt.Return = null;
            borgShunt.ReturnAction = null;
            var brainShunt = SEntMan.GetComponent<StationAIShuntComponent>(controlledBrain);
            brainShunt.Return = null;
            brainShunt.ReturnAction = null;
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var shunt = new AIShuntActionEvent
            {
                Target = untouchedBorg,
                IgnoreCameraView = true
            };
            SEntMan.EventBus.RaiseLocalEvent(SPlayer, shunt);
            Assert.That(shunt.Handled, Is.True, "The AI should be able to shunt into the untouched borg.");
        });

        await RunTicks(5);

        var untouchedBorgStart = Vector2.Zero;
        await Server.WaitAssertion(() => untouchedBorgStart = Transform.GetWorldPosition(untouchedBorg));
        await Move(DirectionFlag.East, 1f);
        await Server.WaitAssertion(() =>
            untouchedBorgMoved = Transform.GetWorldPosition(untouchedBorg).X > untouchedBorgStart.X + 0.1f);

        Assert.Multiple(() =>
        {
            Assert.That(untouchedBorgMoved, Is.True, "The borg that was never remote-controlled should move after shunting.");
            Assert.That(controlledBorgMoved, Is.True,
                $"The previously remote-controlled borg should move after shunting (InputMover after release: {moverPresentAfterRemoteControl}, after shunt: {moverPresentAfterShunt}).");
        });
    }

    private EntityUid SpawnBorg(EntityCoordinates coordinates, out EntityUid brain)
    {
        var containerSystem = Server.System<SharedContainerSystem>();
        var batterySystem = Server.System<BatterySystem>();
        var borgSystem = Server.System<BorgSystem>();

        var chassis = SEntMan.SpawnEntity("BorgChassisGeneric", coordinates);
        var chassisComponent = SEntMan.GetComponent<BorgChassisComponent>(chassis);
        brain = SEntMan.SpawnEntity("RemoteControlBorgMovementTestInterface", coordinates);
        Assert.That(containerSystem.Insert(brain, chassisComponent.BrainContainer), Is.True);

        var cell = SEntMan.SpawnEntity("PowerCellSmall", coordinates);
        var battery = SEntMan.GetComponent<BatteryComponent>(cell);
        batterySystem.SetCharge((cell, battery), battery.MaxCharge);
        var cellSlot = SEntMan.GetComponent<ItemSlotsComponent>(chassis).Slots["cell_slot"];
        Assert.That(containerSystem.Insert(cell, cellSlot.ContainerSlot!), Is.True);
        Assert.That(borgSystem.TryActivate((chassis, chassisComponent), allowRemoteControl: true), Is.True);

        return chassis;
    }
}
