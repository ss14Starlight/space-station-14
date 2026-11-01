#nullable enable
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Tests.Movement;
using Content.Client._Starlight.Computers.RemoteControl;
using Content.Server._Starlight.Computers.RemoteControl;
using Content.Server.Power.EntitySystems;
using Content.Server.Silicons.Borgs;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.Silicons.Borgs;
using Content.Shared.ActionBlocker;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Input;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._Starlight.RemoteControl;

[TestFixture]
[TestOf(typeof(RemoteControlConsoleSystem))]
public sealed class RemoteControlBorgMovementTest : MovementTest
{
    [TestPrototypes] private const string Prototypes = """
        -   type: entity
            id: RemoteControlBorgMovementTestAI
            parent: MobHuman
            components:
            -   type: StationAIShuntable
            -   type: RemoteControlConsole
            -   type: UserInterface
                interfaces:
                    enum.RemoteControlUIKey.Key:
                        type: RemoteControlConsoleBoundUserInterface
        -   type: entity
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
            var remoteControl = new AIRemoteControlActionEvent { Target = controlledBorg };
            SEntMan.EventBus.RaiseLocalEvent(SPlayer, remoteControl);

            var console = SEntMan.GetComponent<RemoteControlConsoleComponent>(SPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(remoteControl.Handled, Is.True,
                    "The AI remote-control action should take control of the first borg.");
                Assert.That(console.Controller, Is.EqualTo(SPlayer));
                Assert.That(SEntMan.GetComponent<RelayInputMoverComponent>(SPlayer).RelayEntity,
                    Is.EqualTo(controlledBorg));
            });
        });

        await RunTicks(1);

        await Server.WaitAssertion(() =>
            SEntMan.EventBus.RaiseLocalEvent(SPlayer,
                new BoundUIClosedEvent(RemoteControlUIKey.Key, SPlayer, SPlayer)));

        await RunTicks(1);

        await Server.WaitAssertion(() =>
        {
            var console = SEntMan.GetComponent<RemoteControlConsoleComponent>(SPlayer);
            Assert.That(console.Controller, Is.Null, "Remote control should be released before shunting.");
            moverPresentAfterRemoteControl = SEntMan.HasComponent<InputMoverComponent>(controlledBorg);

            var shunt = new AIShuntActionEvent { Target = controlledBorg, IgnoreCameraView = true };
            SEntMan.EventBus.RaiseLocalEvent(SPlayer, shunt);
            Assert.That(shunt.Handled, Is.True, "The AI should be able to shunt into the previously controlled borg.");
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
            moverPresentAfterShunt = SEntMan.HasComponent<InputMoverComponent>(controlledBorg));

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
            var shunt = new AIShuntActionEvent { Target = untouchedBorg, IgnoreCameraView = true };
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
            Assert.That(untouchedBorgMoved, Is.True,
                "The borg that was never remote-controlled should move after shunting.");
            Assert.That(controlledBorgMoved, Is.True,
                $"The previously remote-controlled borg should move after shunting (InputMover after release: {moverPresentAfterRemoteControl}, after shunt: {moverPresentAfterShunt}).");
        });
    }

    [Test]
    public async Task StartingRemoteControlStopsPreviousEyeMovement()
    {
        EntityUid controlledBorg = default;
        EntityUid aiEye = default;

        await Server.WaitAssertion(() =>
        {
            var coordinates = SEntMan.GetCoordinates(PlayerCoords);
            controlledBorg = SpawnBorg(coordinates.Offset(new Vector2(4f, 0f)), out _);
            aiEye = SEntMan.SpawnEntity("StationAiHolo", coordinates.Offset(new Vector2(-2f, 0f)));

            var mindSystem = Server.System<SharedMindSystem>();
            var mindId = mindSystem.CreateMind(ServerSession.UserId);
            mindSystem.TransferTo(mindId, SPlayer);
        });

        await RunTicks(5);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var mover = Server.System<SharedMoverController>();
                var playerMover = SEntMan.EnsureComponent<InputMoverComponent>(SPlayer);
                var eyeMover = SEntMan.EnsureComponent<InputMoverComponent>(aiEye);
                var eyePhysics = SEntMan.GetComponent<PhysicsComponent>(aiEye);

                using (Assert.EnterMultipleScope())
                {
                    mover.SetVelocityDirection((SPlayer, playerMover), Direction.West, 0, true);
                    Assert.That(playerMover.HeldMoveButtons, Is.EqualTo(MoveButtons.Left));

                    mover.SetRelay(SPlayer, aiEye);
                    Assert.That(playerMover.HeldMoveButtons, Is.EqualTo(MoveButtons.None));

                    mover.SetVelocityDirection((aiEye, eyeMover), Direction.East, 0, true);
                    Assert.That(eyeMover.HeldMoveButtons, Is.EqualTo(MoveButtons.Right));
                    var eyeInput = mover.GetVelocityInput(eyeMover);
                    Assert.That(eyeInput.Walking + eyeInput.Sprinting, Is.Not.EqualTo(Vector2.Zero));

                    Server.System<SharedPhysicsSystem>()
                        .SetLinearVelocity(aiEye, new Vector2(2f, 0f), body: eyePhysics);
                    Assert.That(eyePhysics.LinearVelocity, Is.Not.EqualTo(Vector2.Zero));

                    var remoteControl = new AIRemoteControlActionEvent { Target = controlledBorg };
                    SEntMan.EventBus.RaiseLocalEvent(SPlayer, remoteControl);

                    Assert.That(remoteControl.Handled, Is.True,
                        "The AI should be able to remotely control the borg.");
                    Assert.That(eyeMover.HeldMoveButtons, Is.EqualTo(MoveButtons.None),
                        "The AI Eye should stop moving when control switches to the borg.");
                    eyeInput = mover.GetVelocityInput(eyeMover);
                    Assert.That(eyeInput.Walking + eyeInput.Sprinting, Is.EqualTo(Vector2.Zero));
                    Assert.That(eyePhysics.LinearVelocity, Is.EqualTo(Vector2.Zero));
                    Assert.That(SEntMan.GetComponent<RelayInputMoverComponent>(SPlayer).RelayEntity,
                        Is.EqualTo(controlledBorg));
                }
            });
        }
        finally
        {
            await RunTicks(1);
            await Server.WaitPost(() =>
            {
                if (SEntMan.EntityExists(SPlayer))
                    SEntMan.EventBus.RaiseLocalEvent(SPlayer,
                        new BoundUIClosedEvent(RemoteControlUIKey.Key, SPlayer, SPlayer));
            });
            await RunTicks(1);
        }
    }

    [Test]
    public async Task PlayerControlledEntityPopupUsesAiEye()
    {
        var popupCapture = Client.System<RemoteControlPopupTestSystem>();
        await Client.WaitAssertion(popupCapture.Clear);

        EntityUid controlledBorg = default;
        EntityUid aiEye = default;
        EntityUid previousRelayEntity = default;
        var hadPreviousRelay = false;
        NetEntity aiEyeNetEntity = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var coordinates = SEntMan.GetCoordinates(PlayerCoords);
                controlledBorg = SpawnBorg(coordinates.Offset(new Vector2(4f, 0f)), out _);
                aiEye = SEntMan.SpawnEntity("StationAiHolo", coordinates.Offset(new Vector2(-2f, 0f)));
                SEntMan.AddComponent<ActorComponent>(controlledBorg);

                if (SEntMan.TryGetComponent<RelayInputMoverComponent>(SPlayer, out var relay))
                {
                    hadPreviousRelay = true;
                    previousRelayEntity = relay.RelayEntity;
                }

                var console = SEntMan.GetComponent<RemoteControlConsoleComponent>(SPlayer);
                console.Users.Add(SPlayer);
                Server.System<SharedMoverController>().SetRelay(SPlayer, aiEye);
                aiEyeNetEntity = SEntMan.GetNetEntity(aiEye);

                var remoteControl = new AIRemoteControlActionEvent { Target = controlledBorg };
                SEntMan.EventBus.RaiseLocalEvent(SPlayer, remoteControl);

                Assert.Multiple(() =>
                {
                    Assert.That(remoteControl.Handled, Is.True,
                        "The remote-control action should be handled even though the target is directly controlled.");
                    Assert.That(console.Controller, Is.Null,
                        "A directly controlled target should not be remotely controlled.");
                });
            });

            await RunTicks(5);

            await Client.WaitAssertion(() =>
            {
                var popup = popupCapture.LastPopup;
                Assert.That(popup, Is.Not.Null);
                Assert.That(popup!.Uid, Is.EqualTo(aiEyeNetEntity));
            });
        }
        finally
        {
            await RunTicks(1);
            await Server.WaitPost(() =>
            {
                if (SEntMan.EntityExists(SPlayer))
                {
                    SEntMan.EventBus.RaiseLocalEvent(SPlayer,
                        new BoundUIClosedEvent(RemoteControlUIKey.Key, SPlayer, SPlayer));

                    if (hadPreviousRelay && SEntMan.EntityExists(previousRelayEntity))
                        Server.System<SharedMoverController>().SetRelay(SPlayer, previousRelayEntity);
                    else if (SEntMan.TryGetComponent<RelayInputMoverComponent>(SPlayer, out var relay))
                        SEntMan.RemoveComponent(SPlayer, relay);
                }
            });
            await RunTicks(1);
        }
    }

    [Test]
    public async Task ReleasePulledObjectStopsPullingForRemoteControlledBorg()
    {
        EntityUid controlledBorg = default;
        EntityUid pulledEntity = default;
        EntityUid consoleUid = default;

        await Server.WaitAssertion(() =>
        {
            var coordinates = SEntMan.GetCoordinates(PlayerCoords);
            controlledBorg = SpawnBorg(coordinates.Offset(new Vector2(4f, 0f)), out _);
            pulledEntity = SEntMan.SpawnEntity("MobHuman", coordinates.Offset(new Vector2(5f, 0f)));

            var mindSystem = Server.System<SharedMindSystem>();
            var mindId = mindSystem.CreateMind(ServerSession.UserId);
            mindSystem.TransferTo(mindId, SPlayer);
        });

        await RunTicks(5);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var remoteControl = new AIRemoteControlActionEvent { Target = controlledBorg };
                SEntMan.EventBus.RaiseLocalEvent(SPlayer, remoteControl);
                Assert.That(remoteControl.Handled, Is.True,
                    "The AI should be able to remotely control the borg.");

                var controller = SEntMan.GetComponent<RemoteControlControllerComponent>(SPlayer);
                Assert.That(controller.Consoles, Has.Count.EqualTo(1));
                consoleUid = controller.Consoles.Single();

                SEntMan.EventBus.RaiseLocalEvent(consoleUid, new RemoteControlInteractionMessage
                {
                    Actor = SPlayer,
                    Entity = SEntMan.GetNetEntity(consoleUid),
                    UiKey = RemoteControlUIKey.Key,
                    Coordinates = SEntMan.GetNetCoordinates(SEntMan.GetComponent<TransformComponent>(pulledEntity).Coordinates),
                    Target = SEntMan.GetNetEntity(pulledEntity),
                    Action = RemoteControlInteractionAction.TryPull,
                });

                Assert.That(SEntMan.GetComponent<PullerComponent>(controlledBorg).Pulling, Is.EqualTo(pulledEntity),
                    "TryPull should make the remote borg pull the selected object.");
            });

            await RunTicks(1);
            await PressKey(ContentKeyFunctions.ReleasePulledObject);
            await RunTicks(5);

            await Server.WaitAssertion(() =>
            {
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(SEntMan.GetComponent<PullerComponent>(controlledBorg).Pulling, Is.Null,
                        "H should stop the remote borg from pulling.");
                    Assert.That(SEntMan.GetComponent<PullableComponent>(pulledEntity).Puller, Is.Null,
                        "The pulled entity should no longer have a puller.");
                }
            });
        }
        finally
        {
            await RunTicks(1);
            await Server.WaitPost(() =>
            {
                if (SEntMan.EntityExists(SPlayer))
                    SEntMan.EventBus.RaiseLocalEvent(SPlayer,
                        new BoundUIClosedEvent(RemoteControlUIKey.Key, SPlayer, SPlayer));
            });
            await RunTicks(1);
        }
    }

    [Test]
    public async Task IncapacitatedRemoteBorgCannotActOrMoveButControllerCanSpeak()
    {
        EntityUid controlledBorg = default;
        EntityUid pulledEntity = default;
        EntityUid consoleUid = default;

        await Server.WaitAssertion(() =>
        {
            var coordinates = SEntMan.GetCoordinates(PlayerCoords);
            controlledBorg = SpawnBorg(coordinates.Offset(new Vector2(4f, 0f)), out _);
            pulledEntity = SEntMan.SpawnEntity("MobHuman", coordinates.Offset(new Vector2(5f, 0f)));

            var mindSystem = Server.System<SharedMindSystem>();
            var mindId = mindSystem.CreateMind(ServerSession.UserId);
            mindSystem.TransferTo(mindId, SPlayer);
        });

        await RunTicks(5);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var remoteControl = new AIRemoteControlActionEvent { Target = controlledBorg };
                SEntMan.EventBus.RaiseLocalEvent(SPlayer, remoteControl);
                Assert.That(remoteControl.Handled, Is.True,
                    "The AI should be able to remotely control the borg.");

                var controller = SEntMan.GetComponent<RemoteControlControllerComponent>(SPlayer);
                consoleUid = controller.Consoles.Single();
            });

            var startPosition = Vector2.Zero;
            await Server.WaitAssertion(() => startPosition = Transform.GetWorldPosition(controlledBorg));
            await SetMovementKey(DirectionFlag.East, BoundKeyState.Down);
            await RunSeconds(0.5f);

            var movingPosition = Vector2.Zero;
            await Server.WaitAssertion(() =>
            {
                movingPosition = Transform.GetWorldPosition(controlledBorg);
                Assert.That(movingPosition.X, Is.GreaterThan(startPosition.X + 0.1f),
                    "The remote borg should move before becoming incapacitated.");
            });

            await Server.WaitAssertion(() =>
            {
                var uiAttempt = new BoundUserInterfaceMessageAttempt(
                    SPlayer,
                    controlledBorg,
                    BorgUiKey.Key,
                    new OpenBoundInterfaceMessage());
                SEntMan.EventBus.RaiseLocalEvent(controlledBorg, uiAttempt, broadcast: true);
                Assert.That(uiAttempt.Cancelled, Is.False,
                    "The controller should be able to send BUI actions to a conscious borg.");

                Server.System<MobStateSystem>().ChangeMobState(controlledBorg, MobState.Critical);
                var actionBlocker = Server.System<ActionBlockerSystem>();
                var remoteControlSystem = Server.System<RemoteControlConsoleSystem>();
                Assert.That(actionBlocker.CanConsciouslyPerformAction(controlledBorg), Is.False,
                    "A critical borg should be unable to perform actions.");
                Assert.That(actionBlocker.CanSpeak(SPlayer), Is.True,
                    "Incapacitating the remote borg should not prevent its controller from speaking.");
                Assert.That(remoteControlSystem.TryGetControlledEntity(SPlayer, out var stillControlled), Is.True);
                Assert.That(stillControlled, Is.EqualTo(controlledBorg),
                    "Incapacitation should not release the remote control session.");

                uiAttempt = new BoundUserInterfaceMessageAttempt(
                    SPlayer,
                    controlledBorg,
                    BorgUiKey.Key,
                    new OpenBoundInterfaceMessage());
                SEntMan.EventBus.RaiseLocalEvent(controlledBorg, uiAttempt, broadcast: true);
                Assert.That(uiAttempt.Cancelled, Is.True,
                    "The controller should not be able to send BUI actions to an incapacitated borg.");

                SEntMan.EventBus.RaiseLocalEvent(consoleUid, new RemoteControlInteractionMessage
                {
                    Actor = SPlayer,
                    Entity = SEntMan.GetNetEntity(consoleUid),
                    UiKey = RemoteControlUIKey.Key,
                    Coordinates = SEntMan.GetNetCoordinates(
                        SEntMan.GetComponent<TransformComponent>(pulledEntity).Coordinates),
                    Target = SEntMan.GetNetEntity(pulledEntity),
                    Action = RemoteControlInteractionAction.TryPull,
                });

                Assert.That(SEntMan.GetComponent<PullerComponent>(controlledBorg).Pulling, Is.Null,
                    "The remote borg should not be able to start pulling while critical.");
            });

            await RunSeconds(0.5f);

            var borgPosition = Vector2.Zero;
            await Server.WaitAssertion(() =>
            {
                borgPosition = Transform.GetWorldPosition(controlledBorg);
                Assert.That(borgPosition.X, Is.EqualTo(movingPosition.X).Within(0.1f),
                    "Movement already in progress should stop when the borg becomes critical.");
            });

            await SetMovementKey(DirectionFlag.East, BoundKeyState.Up);
            await RunTicks(1);
            await Move(DirectionFlag.East, 1f);
            await Server.WaitAssertion(() =>
                Assert.That(Transform.GetWorldPosition(controlledBorg).X, Is.EqualTo(borgPosition.X).Within(0.1f),
                    "The remote borg should not move while critical."));
        }
        finally
        {
            await SetMovementKey(DirectionFlag.East, BoundKeyState.Up);
            await RunTicks(1);
            await Server.WaitPost(() =>
            {
                if (SEntMan.EntityExists(SPlayer))
                    SEntMan.EventBus.RaiseLocalEvent(SPlayer,
                        new BoundUIClosedEvent(RemoteControlUIKey.Key, SPlayer, SPlayer));
            });
            await RunTicks(1);
        }
    }

    [Test]
    public async Task ToggleKnockdownKeyActsOnRemoteBorg()
    {
        EntityUid controlledBorg = default;

        await Server.WaitAssertion(() =>
        {
            var coordinates = SEntMan.GetCoordinates(PlayerCoords);
            controlledBorg = SpawnBorg(coordinates.Offset(new Vector2(4f, 0f)), out _);

            var mindSystem = Server.System<SharedMindSystem>();
            var mindId = mindSystem.CreateMind(ServerSession.UserId);
            mindSystem.TransferTo(mindId, SPlayer);
        });

        await RunTicks(5);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var remoteControl = new AIRemoteControlActionEvent { Target = controlledBorg };
                SEntMan.EventBus.RaiseLocalEvent(SPlayer, remoteControl);
                Assert.That(remoteControl.Handled, Is.True,
                    "The AI should be able to remotely control the borg.");
            });

            await RunTicks(5);
            await Client.WaitAssertion(() =>
            {
                Assert.That(Client.System<RemoteControlInterface>().ControlledEntity, Is.Not.Null,
                    "The client should be controlling the borg before receiving the knockdown key.");
                Assert.That(CEntMan.GetComponent<StandingStateComponent>(CPlayer).Standing, Is.True,
                    "The controller should be standing before the remote knockdown key.");
            });
            await SetKey(ContentKeyFunctions.ToggleKnockdown, BoundKeyState.Down);
            await Client.WaitAssertion(() =>
            {
                Assert.That(CEntMan.GetComponent<StandingStateComponent>(CPlayer).Standing, Is.True,
                    "The remote knockdown key should not predict the controller going down.");
                Assert.That(CEntMan.HasComponent<KnockedDownComponent>(CPlayer), Is.False);
            });
            await RunTicks(1);
            await SetKey(ContentKeyFunctions.ToggleKnockdown, BoundKeyState.Up);
            await RunTicks(1);

            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<StandingStateComponent>(controlledBorg).Standing, Is.False,
                    "The knockdown key should put the remotely controlled borg down.");
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(controlledBorg), Is.True);
                Assert.That(SEntMan.GetComponent<StandingStateComponent>(SPlayer).Standing, Is.True,
                    "The knockdown key should not affect the controller.");
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(SPlayer), Is.False);
            });

            await RunSeconds(1f);
            await PressKey(ContentKeyFunctions.ToggleKnockdown);
            await RunSeconds(3f);

            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<StandingStateComponent>(controlledBorg).Standing, Is.True,
                    "The knockdown key should also allow the remote borg to stand back up.");
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(controlledBorg), Is.False);
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (SEntMan.EntityExists(SPlayer))
                    SEntMan.EventBus.RaiseLocalEvent(SPlayer,
                        new BoundUIClosedEvent(RemoteControlUIKey.Key, SPlayer, SPlayer));
            });
            await RunTicks(1);
        }
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
