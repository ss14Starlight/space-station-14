using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Computers.RemoteControl;
using Content.Server.Power.EntitySystems;
using Content.Server.Silicons.Borgs;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.Borgs;
using Content.Shared.UserInterface;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.RemoteControl;

[TestOf(typeof(RemoteControlConsoleSystem))]
public sealed class RemoteControlConsoleTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
-   type: entity
    id: RemoteControlRangeTestUi
    components:
    -   type: UserInterface
        interfaces:
            enum.BorgSwitchableTypeUiKey.SelectBorgType:
                type: BorgSelectTypeUserInterface
                interactionRange: 2
-   type: entity
    id: RemoteControlForceTestConsole
    parent: RemoteControlConsole
    components:
    -   type: RemoteControlConsole
        canForceRemoteControl: true
";

    [TestCase(false, 1, true)]
    [TestCase(false, 10, false)]
    [TestCase(true, 1, true)]
    [TestCase(true, 10, false)]
    public async Task UiMessagesRespectRemoteRange(bool controlling, int distance, bool expectedOpen)
    {
        await Server.WaitAssertion(() =>
        {
            var mapSystem = Server.System<SharedMapSystem>();
            var ui = Server.System<SharedUserInterfaceSystem>();
            var map = mapSystem.CreateMap(out var mapId);

            try
            {
                var target = SEntMan.SpawnEntity("RemoteControlRangeTestUi", new MapCoordinates(0, 0, mapId));
                var user = SEntMan.SpawnEntity(null, new MapCoordinates(controlling ? 100 : distance, 0, mapId));
                if (controlling)
                {
                    var remote = SEntMan.SpawnEntity(null, new MapCoordinates(distance, 0, mapId));
                    var consoleUid = SEntMan.SpawnEntity(null, new MapCoordinates(100, 0, mapId));
                    var console = SEntMan.AddComponent<RemoteControlConsoleComponent>(consoleUid);
                    console.RemoteBrain = remote;
                    console.Controller = user;
                    console.Users.Add(user);
                }

                var key = BorgSwitchableTypeUiKey.SelectBorgType;
                Assert.DoesNotThrow(() => ui.OpenUi(target, key, user));
                Assert.That(ui.IsUiOpen(target, key, user), Is.EqualTo(expectedOpen));
            }
            finally
            {
                SEntMan.DeleteEntity(map);
            }
        });
    }

    [Test]
    public async Task ReleasingRemoteControlClosesBorgTypeUiAndAllowsReopening()
    {
        await Server.WaitAssertion(() =>
        {
            var mapSystem = Server.System<SharedMapSystem>();
            var ui = Server.System<SharedUserInterfaceSystem>();
            var map = mapSystem.CreateMap(out var mapId);

            try
            {
                var coordinates = new MapCoordinates(0, 0, mapId);
                var remoteEntity = SEntMan.SpawnEntity("RemoteControlRangeTestUi", coordinates);
                var consoleUid = SEntMan.SpawnEntity("RemoteControlConsole", coordinates);
                var console = SEntMan.GetComponent<RemoteControlConsoleComponent>(consoleUid);
                var user = SEntMan.SpawnEntity(null, coordinates);
                console.RemoteBrain = remoteEntity;
                console.Controller = user;
                console.Users.Add(user);

                var selectTypeKey = BorgSwitchableTypeUiKey.SelectBorgType;
                var remoteControlKey = RemoteControlUIKey.Key;
                ui.OpenUi(remoteEntity, selectTypeKey, user);
                Assert.That(ui.IsUiOpen(remoteEntity, selectTypeKey, user), Is.True);

                SEntMan.EventBus.RaiseLocalEvent(consoleUid,
                    new BoundUIClosedEvent(remoteControlKey, consoleUid, user));
                Assert.That(console.Controller, Is.Null);
                Assert.That(ui.IsUiOpen(remoteEntity, selectTypeKey, user), Is.False);

                console.Users.Add(user);
                console.Controller = user;
                ui.OpenUi(remoteEntity, selectTypeKey, user);
                Assert.That(ui.IsUiOpen(remoteEntity, selectTypeKey, user), Is.True);
            }
            finally
            {
                SEntMan.DeleteEntity(map);
            }
        });
    }

    [Test]
    public async Task BorgTypeChangeRefreshesAllConnectedConsoleStates()
    {
        EntityUid? map = null;
        EntityUid? chassis = null;
        EntityUid? brain = null;
        EntityUid controller = default;
        EntityUid observer = default;
        EntityUid consoleUid = default;
        EntityUid observerConsoleUid = default;
        NetEntity selectTypeAction = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var mapSystem = Server.System<SharedMapSystem>();
                var containerSystem = Server.System<SharedContainerSystem>();
                var ui = Server.System<SharedUserInterfaceSystem>();
                map = mapSystem.CreateMap(out var mapId);
                var coordinates = new MapCoordinates(0, 0, mapId);
                controller = SEntMan.SpawnEntity(null, coordinates);
                observer = SEntMan.SpawnEntity(null, coordinates);
                chassis = SEntMan.SpawnEntity("BorgChassisSelectable", coordinates);
                brain = SEntMan.SpawnEntity("RemoteControlBrain", coordinates);
                var chassisUid = chassis.Value;
                var brainUid = brain.Value;
                var borg = SEntMan.GetComponent<BorgChassisComponent>(chassisUid);
                Assert.That(containerSystem.Insert(brainUid, borg.BrainContainer), Is.True);

                consoleUid = SEntMan.SpawnEntity("RemoteControlConsole", coordinates);
                observerConsoleUid = SEntMan.SpawnEntity("RemoteControlConsole", coordinates);
                var console = SEntMan.GetComponent<RemoteControlConsoleComponent>(consoleUid);
                var observerConsole = SEntMan.GetComponent<RemoteControlConsoleComponent>(observerConsoleUid);
                console.RemoteBrain = brainUid;
                console.Controller = controller;
                observerConsole.RemoteBrain = brainUid;
                observerConsole.Controller = controller;

                SEntMan.EventBus.RaiseLocalEvent(consoleUid,
                    new BoundUIOpenedEvent(RemoteControlUIKey.Key, consoleUid, controller));
                SEntMan.EventBus.RaiseLocalEvent(consoleUid,
                    new BoundUIOpenedEvent(RemoteControlUIKey.Key, consoleUid, observer));
                SEntMan.EventBus.RaiseLocalEvent(observerConsoleUid,
                    new BoundUIOpenedEvent(RemoteControlUIKey.Key, observerConsoleUid, observer));
                observerConsole.Controller = null;

                var switchable = SEntMan.GetComponent<BorgSwitchableTypeComponent>(chassisUid);
                Assert.That(switchable.SelectTypeAction, Is.Not.Null);
                selectTypeAction = SEntMan.GetNetEntity(switchable.SelectTypeAction!.Value);

                Assert.That(ui.TryGetUiState<RemoteControlConsoleBuiState>(
                    consoleUid, RemoteControlUIKey.Key, out var initialState), Is.True);
                Assert.That(initialState!.Actions, Does.Contain(selectTypeAction));
                Assert.That(ui.TryGetUiState<RemoteControlConsoleBuiState>(
                    observerConsoleUid, RemoteControlUIKey.Key, out var observerInitialState), Is.True);
                Assert.That(observerInitialState!.Actions, Is.EqualTo(initialState.Actions));

                var borgTypes = Server.System<BorgSwitchableTypeSystem>();
                Assert.That(borgTypes.TrySelectBorgType(
                    (chassisUid, switchable), new ProtoId<BorgTypePrototype>("generic")), Is.True);
            });

            await Server.WaitRunTicks(1);
            await Server.WaitAssertion(() =>
            {
                var ui = Server.System<SharedUserInterfaceSystem>();
                Assert.That(ui.TryGetUiState<RemoteControlConsoleBuiState>(
                    consoleUid, RemoteControlUIKey.Key, out var state), Is.True);
                Assert.That(ui.TryGetUiState<RemoteControlConsoleBuiState>(
                    observerConsoleUid, RemoteControlUIKey.Key, out var observerState), Is.True);
                Assert.That(state!.Actions, Does.Not.Contain(selectTypeAction));
                Assert.That(observerState!.Actions, Is.EqualTo(state.Actions));
                Assert.That(SEntMan.GetComponent<RemoteControlConsoleComponent>(consoleUid).Users,
                    Does.Contain(controller));
                Assert.That(SEntMan.GetComponent<RemoteControlConsoleComponent>(consoleUid).Users,
                    Does.Contain(observer));
                Assert.That(SEntMan.GetComponent<RemoteControlConsoleComponent>(observerConsoleUid).Users,
                    Does.Contain(observer));
            });
        }
        finally
        {
            if (map is { } mapUid)
            {
                await Server.WaitPost(() =>
                {
                    if (chassis is { } chassisUid
                        && brain is { } brainUid
                        && SEntMan.TryGetComponent<BorgChassisComponent>(chassisUid, out var borg))
                        Server.System<SharedContainerSystem>().Remove(brainUid, borg.BrainContainer);

                    SEntMan.DeleteEntity(mapUid);
                });
            }
        }
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    public async Task PlayerControlledEntityCanOnlyBeRemoteControlledWhenForced(bool canForce, bool expectedControl)
    {
        await Server.WaitAssertion(() =>
        {
            var mapSystem = Server.System<SharedMapSystem>();
            var playerManager = Server.ResolveDependency<IPlayerManager>();
            var session = playerManager.Sessions.Single();
            var previousAttachedEntity = session.AttachedEntity;
            var map = mapSystem.CreateMap(out var mapId);

            try
            {
                var coordinates = new MapCoordinates(0, 0, mapId);
                var remoteEntity = SEntMan.SpawnEntity("RemoteControlRangeTestUi", coordinates);
                Assert.That(playerManager.SetAttachedEntity(session, remoteEntity), Is.True);

                var consoleUid = SEntMan.SpawnEntity(
                    canForce ? "RemoteControlForceTestConsole" : "RemoteControlConsole",
                    coordinates);
                var console = SEntMan.GetComponent<RemoteControlConsoleComponent>(consoleUid);
                Assert.That(console.CanForceRemoteControl, Is.EqualTo(canForce));
                console.RemoteBrain = remoteEntity;

                var controller = SEntMan.SpawnEntity(null, coordinates);
                SEntMan.EventBus.RaiseLocalEvent(consoleUid,
                    new BoundUIOpenedEvent(RemoteControlUIKey.Key, consoleUid, controller));

                Assert.That(console.Controller, Is.EqualTo(expectedControl ? controller : (EntityUid?) null));
            }
            finally
            {
                playerManager.SetAttachedEntity(session, previousAttachedEntity);
                SEntMan.DeleteEntity(map);
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    public async Task PlayerTakingOverRemoteEntityEndsControlUnlessForced(bool canForce, bool expectedToRemainInControl)
    {
        await Server.WaitAssertion(() =>
        {
            var mapSystem = Server.System<SharedMapSystem>();
            var playerManager = Server.ResolveDependency<IPlayerManager>();
            var session = playerManager.Sessions.Single();
            var previousAttachedEntity = session.AttachedEntity;
            var map = mapSystem.CreateMap(out var mapId);

            try
            {
                var coordinates = new MapCoordinates(0, 0, mapId);
                var remoteEntity = SEntMan.SpawnEntity("RemoteControlRangeTestUi", coordinates);
                var consoleUid = SEntMan.SpawnEntity(
                    canForce ? "RemoteControlForceTestConsole" : "RemoteControlConsole",
                    coordinates);
                var console = SEntMan.GetComponent<RemoteControlConsoleComponent>(consoleUid);
                Assert.That(console.CanForceRemoteControl, Is.EqualTo(canForce));
                console.RemoteBrain = remoteEntity;

                var controller = SEntMan.SpawnEntity(null, coordinates);
                console.Controller = controller;
                console.Users.Add(controller);

                Assert.That(playerManager.SetAttachedEntity(session, remoteEntity), Is.True);
                Assert.That(console.Controller,
                    Is.EqualTo(expectedToRemainInControl ? controller : (EntityUid?) null));
            }
            finally
            {
                playerManager.SetAttachedEntity(session, previousAttachedEntity);
                SEntMan.DeleteEntity(map);
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    public async Task RemoteControlTakeoverRequiresForce(bool canForce, bool expectedToTakeOver)
    {
        await Server.WaitAssertion(() =>
        {
            var mapSystem = Server.System<SharedMapSystem>();
            var ui = Server.System<SharedUserInterfaceSystem>();
            var map = mapSystem.CreateMap(out var mapId);

            try
            {
                var coordinates = new MapCoordinates(0, 0, mapId);
                var remoteEntity = SEntMan.SpawnEntity("RemoteControlRangeTestUi", coordinates);
                var currentConsoleUid = SEntMan.SpawnEntity("RemoteControlConsole", coordinates);
                var currentConsole = SEntMan.GetComponent<RemoteControlConsoleComponent>(currentConsoleUid);
                var currentController = SEntMan.SpawnEntity(null, coordinates);
                currentConsole.RemoteBrain = remoteEntity;
                currentConsole.Controller = currentController;
                currentConsole.Users.Add(currentController);

                var takeoverConsoleUid = SEntMan.SpawnEntity(
                    canForce ? "RemoteControlForceTestConsole" : "RemoteControlConsole",
                    coordinates);
                var takeoverConsole = SEntMan.GetComponent<RemoteControlConsoleComponent>(takeoverConsoleUid);
                Assert.That(takeoverConsole.CanForceRemoteControl, Is.EqualTo(canForce));
                takeoverConsole.RemoteBrain = remoteEntity;
                var takeoverController = SEntMan.SpawnEntity(null, coordinates);

                SEntMan.EventBus.RaiseLocalEvent(takeoverConsoleUid,
                    new BoundUIOpenedEvent(RemoteControlUIKey.Key, takeoverConsoleUid, takeoverController));

                Assert.That(currentConsole.Controller,
                    Is.EqualTo(expectedToTakeOver ? (EntityUid?) null : currentController));
                Assert.That(takeoverConsole.Controller,
                    Is.EqualTo(expectedToTakeOver ? takeoverController : (EntityUid?) null));
            }
            finally
            {
                SEntMan.DeleteEntity(map);
            }
        });
    }

    [Test]
    public async Task ForcedRemoteControlTakeoverPreservesBorgActivationAndOldUiClose()
    {
        await Server.WaitAssertion(() =>
        {
            var mapSystem = Server.System<SharedMapSystem>();
            var containerSystem = Server.System<SharedContainerSystem>();
            var map = mapSystem.CreateMap(out var mapId);

            try
            {
                var coordinates = new MapCoordinates(0, 0, mapId);
                var chassisUid = SEntMan.SpawnEntity("BorgChassisGeneric", coordinates);
                var chassis = SEntMan.GetComponent<BorgChassisComponent>(chassisUid);
                var brain = SEntMan.SpawnEntity("RemoteControlBrain", coordinates);
                Assert.That(containerSystem.Insert(brain, chassis.BrainContainer), Is.True);
                Server.System<BorgSystem>().SetActive((chassisUid, chassis), true);

                var currentConsoleUid = SEntMan.SpawnEntity("RemoteControlConsole", coordinates);
                var currentConsole = SEntMan.GetComponent<RemoteControlConsoleComponent>(currentConsoleUid);
                var currentController = SEntMan.SpawnEntity(null, coordinates);
                currentConsole.RemoteBrain = brain;
                currentConsole.Controller = currentController;
                currentConsole.Users.Add(currentController);
                currentConsole.BorgActivatedByRemote = true;

                var takeoverConsoleUid = SEntMan.SpawnEntity("RemoteControlForceTestConsole", coordinates);
                var takeoverConsole = SEntMan.GetComponent<RemoteControlConsoleComponent>(takeoverConsoleUid);
                var takeoverController = SEntMan.SpawnEntity(null, coordinates);
                takeoverConsole.RemoteBrain = brain;

                SEntMan.EventBus.RaiseLocalEvent(takeoverConsoleUid,
                    new BoundUIOpenedEvent(RemoteControlUIKey.Key, takeoverConsoleUid, takeoverController));

                Assert.That(currentConsole.Controller, Is.Null);
                Assert.That(currentConsole.BorgActivatedByRemote, Is.False);
                Assert.That(takeoverConsole.Controller, Is.EqualTo(takeoverController));
                Assert.That(takeoverConsole.BorgActivatedByRemote, Is.True);
                Assert.That(chassis.Active, Is.True);

                SEntMan.EventBus.RaiseLocalEvent(currentConsoleUid,
                    new BoundUIClosedEvent(RemoteControlUIKey.Key, currentConsoleUid, currentController));

                Assert.That(takeoverConsole.Controller, Is.EqualTo(takeoverController));
                Assert.That(chassis.Active, Is.True);
            }
            finally
            {
                SEntMan.DeleteEntity(map);
            }
        });
    }

    [Test]
    public async Task RemoteControlMirrorsSingleUserActivatableUi()
    {
        EntityUid? map = null;
        EntityUid scanner = default;
        EntityUid controller = default;
        Enum uiKey = default!;
        IPlayerManager playerManager = default!;
        EntityUid? previousAttachedEntity = null;

        try
        {
            await Server.WaitAssertion(() =>
            {
                playerManager = Server.ResolveDependency<IPlayerManager>();
                var session = playerManager.Sessions.Single();
                previousAttachedEntity = session.AttachedEntity;

                map = Server.System<SharedMapSystem>().CreateMap(out var mapId);
                var coordinates = new MapCoordinates(0, 0, mapId);

                scanner = SEntMan.SpawnEntity("HandHeldMassScannerBorg", coordinates);
                var activatable = SEntMan.GetComponent<ActivatableUIComponent>(scanner);
                uiKey = activatable.Key!;
                controller = SEntMan.SpawnEntity("MobHuman", coordinates);
                Assert.That(playerManager.SetAttachedEntity(session, controller), Is.True);

                var consoleUid = SEntMan.SpawnEntity("RemoteControlConsole", coordinates);
                var console = SEntMan.GetComponent<RemoteControlConsoleComponent>(consoleUid);
                console.RemoteBrain = scanner;
                console.Controller = controller;

                var remoteControl = Server.System<RemoteControlConsoleSystem>();
                Assert.That(remoteControl.TryGetControllerForRemoteEntity(scanner, out var popupRecipient), Is.True);
                Assert.That(popupRecipient, Is.EqualTo(controller));

                Server.System<ActivatableUISystem>().SetCurrentSingleUser(scanner, scanner, activatable);
                SEntMan.EventBus.RaiseLocalEvent(scanner, new BoundUIOpenedEvent(uiKey, scanner, scanner));

                Assert.That(activatable.CurrentSingleUser, Is.EqualTo(controller));
            });

            await Pair.RunTicksSync(1);

            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<SharedUserInterfaceSystem>().IsUiOpen(scanner, uiKey, controller), Is.True);
                Assert.That(SEntMan.GetComponent<ActivatableUIComponent>(scanner).CurrentSingleUser,
                    Is.EqualTo(controller));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (playerManager is not null)
                {
                    var session = playerManager.Sessions.Single();
                    playerManager.SetAttachedEntity(session, previousAttachedEntity);
                }

                if (map is { } mapUid)
                    SEntMan.DeleteEntity(mapUid);
            });
        }
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task RemovingBrainPreservesConnection(bool controlling, bool previousRelay)
    {
        await Server.WaitAssertion(() =>
        {
            var mapSystem = Server.System<SharedMapSystem>();
            var containerSystem = Server.System<SharedContainerSystem>();
            var mover = Server.System<SharedMoverController>();
            var remoteControl = Server.System<RemoteControlConsoleSystem>();
            var ui = Server.System<SharedUserInterfaceSystem>();
            var map = mapSystem.CreateMap(out var mapId);
            var coordinates = new MapCoordinates(0, 0, mapId);

            try
            {
                var chassis = SEntMan.SpawnEntity("BorgChassisGeneric", coordinates);
                var brain = SEntMan.SpawnEntity("RemoteControlBrain", coordinates);
                var borg = SEntMan.GetComponent<BorgChassisComponent>(chassis);
                Assert.That(containerSystem.Insert(brain, borg.BrainContainer), Is.True);

                var consoleUid = SEntMan.SpawnEntity("RemoteControlConsole", coordinates);
                var console = SEntMan.GetComponent<RemoteControlConsoleComponent>(consoleUid);
                var observerConsoleUid = SEntMan.SpawnEntity("RemoteControlConsole", coordinates);
                var observerConsole = SEntMan.GetComponent<RemoteControlConsoleComponent>(observerConsoleUid);
                var user = SEntMan.SpawnEntity(null, coordinates);
                var originalRelay = SEntMan.SpawnEntity(null, coordinates);
                console.RemoteBrain = brain;
                observerConsole.RemoteBrain = brain;
                console.Users.Add(user);
                observerConsole.Users.Add(user);

                if (controlling)
                {
                    console.Controller = user;
                    console.PreviousRelayEntity = previousRelay ? originalRelay : null;
                    mover.SetRelay(user, chassis);
                }

                Assert.That(containerSystem.Remove(brain, borg.BrainContainer), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(console.RemoteBrain, Is.EqualTo(brain));
                    Assert.That(observerConsole.RemoteBrain, Is.EqualTo(brain));
                    Assert.That(console.Users, Does.Contain(user));
                    Assert.That(observerConsole.Users, Does.Contain(user));
                    Assert.That(console.Controller, Is.EqualTo(controlling ? user : (EntityUid?) null));
                    Assert.That(console.PreviousRelayEntity, Is.Null);
                    Assert.That(remoteControl.TryGetControlledEntity(user, out var remote), Is.EqualTo(controlling));
                    if (controlling)
                        Assert.That(remote, Is.EqualTo(brain));

                    if (previousRelay)
                        Assert.That(SEntMan.GetComponent<RelayInputMoverComponent>(user).RelayEntity, Is.EqualTo(originalRelay));
                    else
                        Assert.That(SEntMan.HasComponent<RelayInputMoverComponent>(user), Is.False);

                    foreach (var uid in new[] { consoleUid, observerConsoleUid })
                    {
                        Assert.That(ui.TryGetUiState<RemoteControlConsoleBuiState>(uid, RemoteControlUIKey.Key, out var state), Is.True);
                        Assert.That(state!.Connected, Is.True);
                        Assert.That(state.RemoteEntity, Is.EqualTo(SEntMan.GetNetEntity(brain)));
                        Assert.That(state.Hands, Is.Empty);
                        Assert.That(state.Inventory, Is.Empty);
                    }
                });
            }
            finally
            {
                SEntMan.DeleteEntity(map);
            }
        });
    }

    [Test]
    public async Task RemotelyControlledBorgReactivatesWhenCellRecharges()
    {
        EntityUid? map = null;
        EntityUid chassisUid = default;
        EntityUid cellUid = default;
        BorgChassisComponent chassis = default!;
        BatteryComponent battery = default!;
        RemoteControlConsoleComponent console = default!;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var mapSystem = Server.System<SharedMapSystem>();
                var containerSystem = Server.System<SharedContainerSystem>();
                var batterySystem = Server.System<BatterySystem>();
                var borgSystem = Server.System<BorgSystem>();
                map = mapSystem.CreateMap(out var mapId);
                var coordinates = new MapCoordinates(0, 0, mapId);

                chassisUid = SEntMan.SpawnEntity("BorgChassisGeneric", coordinates);
                chassis = SEntMan.GetComponent<BorgChassisComponent>(chassisUid);
                var brain = SEntMan.SpawnEntity("RemoteControlBrain", coordinates);
                Assert.That(containerSystem.Insert(brain, chassis.BrainContainer), Is.True);

                var controller = SEntMan.SpawnEntity(null, coordinates);
                var consoleUid = SEntMan.SpawnEntity("RemoteControlConsole", coordinates);
                console = SEntMan.GetComponent<RemoteControlConsoleComponent>(consoleUid);
                console.RemoteBrain = brain;
                console.Controller = controller;

                cellUid = SEntMan.SpawnEntity("PowerCellSmall", coordinates);
                battery = SEntMan.GetComponent<BatteryComponent>(cellUid);
                batterySystem.SetCharge((cellUid, battery), battery.MaxCharge);
                var cellSlot = SEntMan.GetComponent<ItemSlotsComponent>(chassisUid).Slots["cell_slot"];
                Assert.That(containerSystem.Insert(cellUid, cellSlot.ContainerSlot!), Is.True);

                Assert.That(borgSystem.TryActivate((chassisUid, chassis), allowRemoteControl: true), Is.True);
                console.BorgActivatedByRemote = true;
            });

            await Server.WaitAssertion(() =>
            {
                Server.System<BatterySystem>().SetCharge((cellUid, battery), 0f);
                Assert.That(chassis.Active, Is.False, "An empty cell should switch the borg off.");
                Server.System<BatterySystem>().SetCharge((cellUid, battery), 0.1f);
            });

            await Pair.RunTicksSync(1);
            await Server.WaitAssertion(() =>
                Assert.That(chassis.Active, Is.False, "The borg should remain off until the cell can supply its draw rate."));

            await Server.WaitAssertion(() =>
                Server.System<BatterySystem>().SetCharge((cellUid, battery), 1f));
            await Pair.RunTicksSync(1);

            await Server.WaitAssertion(() =>
            {
                Assert.That(chassis.Active, Is.True, "The remote controller should reactivate the borg after it has enough charge.");
                Assert.That(console.BorgActivatedByRemote, Is.True);
            });
        }
        finally
        {
            if (map is { } mapUid)
                await Server.WaitPost(() => SEntMan.DeleteEntity(mapUid));
        }
    }
}
