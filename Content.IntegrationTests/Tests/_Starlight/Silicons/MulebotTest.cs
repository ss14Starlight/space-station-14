#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Starlight.Silicons.Bots;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Silicons.Bots;
using Content.Shared.Buckle;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Systems;
using Content.Shared.Emag.Systems;
using Content.Shared.Lock;
using Content.Shared.Movement.Components;
using Content.Shared.Physics;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Wires;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Starlight.Silicons;

[TestFixture]
public sealed class MulebotTest : InteractionTest
{
    private const int Row = 3;

    private EntityUid _mule;
    private MuleSystem _muleSystem = default!;

    [SetUp]
    public async Task SetUpMule()
    {
        _muleSystem = SEntMan.System<MuleSystem>();
        await Server.WaitPost(() => Server.CfgMan.SetCVar(StarlightCCVars.DisablePathfinding, false));
        await AddGravity();
    }

    [TearDown]
    public async Task TearDownMule()
    {
        await Server.WaitPost(() => Server.CfgMan.SetCVar(StarlightCCVars.DisablePathfinding, true));
    }

    [Test]
    public async Task LoadsOnlyCrates()
    {
        await Build(4, new());
        var mule = Mule();

        EntityUid crate = default, closet = default, full = default;
        await Server.WaitPost(() =>
        {
            crate = SEntMan.SpawnAtPosition("CrateGenericSteel", At(1));
            closet = SEntMan.SpawnAtPosition("ClosetEmergency", At(2));
            full = SEntMan.SpawnAtPosition("CrateGenericSteel", At(3));
            var mouse = SEntMan.SpawnAtPosition("MobMouse", At(3));
            SEntMan.System<SharedEntityStorageSystem>().Insert(mouse, full);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(_muleSystem.TryLoad(_mule, closet), Is.False, "Lockers aren't crates");
            Assert.That(_muleSystem.TryLoad(_mule, full), Is.False, "Won't drive off with someone shut in a crate");
            Assert.That(_muleSystem.TryLoad(_mule, crate), "Should take an empty crate");
            Assert.That(_muleSystem.TryGetLoad(_mule, out var load) && load == crate);
            Assert.That(_muleSystem.TryLoad(_mule, full), Is.False, "Only one load at a time");
        });

        await Server.WaitAssertion(() =>
        {
            // Facing along the corridor, it drops the load on the tile ahead.
            Transform.SetLocalRotation(_mule, Direction.East.ToAngle());
            Assert.That(_muleSystem.Unload(_mule));
            Assert.That(_muleSystem.TryGetLoad(_mule, out _), Is.False);
            Assert.That(GridPosition(crate).X - GridPosition(_mule).X, Is.EqualTo(1f).Within(0.1f), "Unloads onto the tile ahead");
        });
    }

    [Test]
    public async Task FacingAWallItUnloadsUnderItselfAndDrivesOff()
    {
        var beacons = await Build(12, new() { [0] = "CargoBay", [10] = "Bar" });
        var mule = Mule();
        var crate = await Load(mule);

        await Server.WaitAssertion(() =>
        {
            Transform.SetLocalRotation(_mule, Direction.North.ToAngle());
            Assert.That(_muleSystem.Unload(_mule));
            Assert.That((GridPosition(crate) - GridPosition(_mule)).Length(), Is.LessThan(0.1f), "Under itself, with a wall ahead");
            Assert.That(SEntMan.GetComponent<PreventCollideComponent>(crate).Uid, Is.EqualTo(_mule));
        });

        // It still gets out over the crate, and stops ignoring it once clear.
        await Server.WaitPost(() => mule.Comp.AutoReturn = false);
        await Send(mule, beacons[0], beacons[10]);
        await RunSeconds(10);

        await Server.WaitAssertion(() =>
        {
            Assert.That(GridPosition(_mule).X, Is.GreaterThan(5f));
            Assert.That(SEntMan.HasComponent<PreventCollideComponent>(crate), Is.False);
        });
    }

    [Test]
    public async Task NoPickupAtHome()
    {
        var beacons = await Build(12, new() { [1] = "CargoBay", [10] = "Bar" });
        var mule = Mule();
        EntityUid crate = default;

        // Sent home from the bar, it shouldn't grab the crate waiting in cargo.
        await Server.WaitPost(() =>
        {
            crate = SEntMan.SpawnAtPosition("CrateGenericSteel", At(0));
            Transform.SetCoordinates(_mule, At(10));
            _muleSystem.SetBeacon(_mule, beacons[1], true);
            _muleSystem.SetBeacon(_mule, beacons[1], false);
            _muleSystem.Command(_mule, MuleCommand.Go);
        });
        await RunSeconds(8);

        await Server.WaitAssertion(() =>
        {
            Assert.That(GridPosition(_mule).X, Is.LessThan(2.5f), "Should have driven home");
            Assert.That(mule.Comp.Mode, Is.EqualTo(MuleMode.Idle));
            Assert.That(_muleSystem.TryGetLoad(_mule, out _), Is.False);
            Assert.That(GridPosition(crate).X, Is.EqualTo(0.5f).Within(0.1f));
        });
    }

    [Test]
    public async Task NewDestinationMidRunTurnsItRound()
    {
        var beacons = await Build(12, new() { [0] = "CargoBay", [6] = "Medbay", [10] = "Bar" });
        var mule = Mule();
        var crate = await Load(mule);
        await Server.WaitPost(() => mule.Comp.AutoReturn = false);
        await Send(mule, beacons[0], beacons[10]);
        await RunSeconds(1);

        await Server.WaitPost(() => _muleSystem.SetBeacon(_mule, beacons[6], false));
        await RunSeconds(8);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_muleSystem.TryGetLoad(_mule, out _), Is.False, "Should have dropped it off");
            Assert.That(GridPosition(_mule).X, Is.EqualTo(6.5f).Within(1f), "At the new beacon, not the old one");
            Assert.That(GridPosition(crate).X, Is.LessThan(8.5f));
        });
    }

    [Test]
    public async Task CalledHomeMidRunItKeepsTheLoad()
    {
        var beacons = await Build(12, new() { [0] = "CargoBay", [10] = "Bar" });
        var mule = Mule();
        var crate = await Load(mule);
        await Send(mule, beacons[0], beacons[10]);
        await RunSeconds(2);

        await Server.WaitPost(() => _muleSystem.Command(_mule, MuleCommand.Home));
        await RunSeconds(1);
        await Server.WaitAssertion(() => Assert.That(_muleSystem.TryGetLoad(_mule, out var load) && load == crate, "Not dropped where it was called back"));

        await RunSeconds(6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(GridPosition(_mule).X, Is.LessThan(1.5f), "Back home");
            Assert.That(mule.Comp.Mode, Is.EqualTo(MuleMode.Idle));
        });
    }

    [Test]
    public async Task CratesCanBeDraggedOntoIt()
    {
        await Build(4, new());
        EntityUid crate = default, closet = default;
        await Server.WaitPost(() =>
        {
            crate = SEntMan.SpawnAtPosition("CrateGenericSteel", At(1));
            closet = SEntMan.SpawnAtPosition("ClosetEmergency", At(2));
        });

        await Server.WaitAssertion(() =>
        {
            var crateDrag = new Content.Shared.DragDrop.CanDragEvent();
            SEntMan.EventBus.RaiseLocalEvent(crate, ref crateDrag);
            Assert.That(crateDrag.Handled, "Players have to be able to start dragging a crate");

            var closetDrag = new Content.Shared.DragDrop.CanDragEvent();
            SEntMan.EventBus.RaiseLocalEvent(closet, ref closetDrag);
            Assert.That(closetDrag.Handled, Is.False, "Lockers stay put");

            var drop = new Content.Shared.DragDrop.CanDropTargetEvent(SPlayer, crate);
            SEntMan.EventBus.RaiseLocalEvent(_mule, ref drop);
            Assert.That(drop.CanDrop, "The MULE takes the drop");

            var dropped = new Content.Shared.DragDrop.DragDropTargetEvent(SPlayer, crate);
            SEntMan.EventBus.RaiseLocalEvent(_mule, ref dropped);
            Assert.That(_muleSystem.TryGetLoad(_mule, out var load) && load == crate, "Dropping it on loads it");
        });
    }

    [Test]
    public async Task OpenCrateIsShutWhenLoaded()
    {
        await Build(4, new());
        var mule = Mule();

        EntityUid crate = default;
        await Server.WaitPost(() =>
        {
            crate = SEntMan.SpawnAtPosition("CrateGenericSteel", At(1));
            SEntMan.System<SharedEntityStorageSystem>().OpenStorage(crate);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(_muleSystem.TryLoad(_mule, crate));
            Assert.That(SEntMan.GetComponent<EntityStorageComponent>(crate).Open, Is.False);
        });
    }

    [Test]
    public async Task DeliversAndComesHome()
    {
        // An open firelock on the way is just floor to it.
        var beacons = await Build(12, new() { [0] = "CargoBay", [10] = "Bar" }, new() { [5] = "FirelockGlass" });
        var mule = Mule();
        var crate = await Load(mule);

        await Send(mule, beacons[0], beacons[10]);
        await RunSeconds(25);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_muleSystem.TryGetLoad(_mule, out _), Is.False);
            Assert.That(GridPosition(crate).X, Is.EqualTo(10.5f).Within(1f), "Should drop the crate at the beacon");
            Assert.That(GridPosition(_mule).X, Is.EqualTo(0.5f).Within(1f), "Should drive back home");
            Assert.That(mule.Comp.Mode, Is.EqualTo(MuleMode.Idle));
        });
    }

    [Test]
    public async Task UsesChargeAsItDrives()
    {
        var beacons = await Build(12, new() { [0] = "CargoBay", [10] = "Bar" });
        var mule = Mule();
        var before = 0f;
        await Server.WaitPost(() => before = Charge());

        await Send(mule, beacons[0], beacons[10]);
        await RunSeconds(10);

        await Server.WaitAssertion(() => Assert.That(Charge(), Is.LessThan(before)));
    }

    [Test]
    public async Task PicksUpACrateWhereItArrivesEmpty()
    {
        var beacons = await Build(12, new() { [0] = "CargoBay", [10] = "Bar" });
        var mule = Mule();
        EntityUid crate = default;
        await Server.WaitPost(() => crate = SEntMan.SpawnAtPosition("CrateGenericSteel", At(10)));

        await Send(mule, beacons[0], beacons[10]);
        await RunSeconds(25);

        await Server.WaitAssertion(() =>
        {
            Assert.That(GridPosition(crate).X, Is.EqualTo(0.5f).Within(1f), "Should bring the crate home and drop it there");
            Assert.That(_muleSystem.TryGetLoad(_mule, out _), Is.False);
        });
    }

    [Test]
    public async Task GoesThroughTheEntranceButNoDeeper()
    {
        var beacons = await Build(16, new() { [0] = "CargoBay", [14] = "Medbay" },
            new() { [5] = "AirlockMedicalLocked", [11] = "AirlockMedicalLocked" });
        var mule = Mule();
        var crate = await Load(mule);
        await Server.WaitPost(() => mule.Comp.AutoReturn = false);

        await Send(mule, beacons[0], beacons[14]);
        await RunSeconds(25);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_muleSystem.TryGetLoad(_mule, out _), Is.False);
            Assert.That(GridPosition(crate).X, Is.GreaterThan(6f).And.LessThan(11f), "Should drop it inside the first door and outside the second");
        });
    }

    [Test]
    public async Task DoesNotDriveThroughADesk()
    {
        // A desk with a cargo windoor on it, which it could open, but the table is still in the way.
        var beacons = await Build(12, new() { [0] = "CargoBay", [9] = "Bar" }, new() { [5] = "WindoorSecureCargoLocked" });
        await Server.WaitPost(() => SEntMan.SpawnAtPosition("Table", At(5)));
        await RunSeconds(2);
        var mule = Mule();
        var crate = await Load(mule);
        await Server.WaitPost(() => mule.Comp.AutoReturn = false);

        await Send(mule, beacons[0], beacons[9]);
        await RunSeconds(15);

        await Server.WaitAssertion(() =>
        {
            Assert.That(mule.Comp.Waypoints, Is.Empty, "Plans no route through the desk");
            Assert.That(_muleSystem.TryGetLoad(_mule, out _), "No way round the desk, so it never gets there");
            Assert.That(GridPosition(_mule).X, Is.LessThan(5f));
        });
    }

    [Test]
    public async Task FirelockDoesNotHideTheDoorOnItsTile()
    {
        // Department doors usually share their tile with an open firelock.
        var beacons = await Build(12, new() { [0] = "CargoBay", [9] = "Security" }, new() { [5] = "AirlockSecurityLocked" });
        await Server.WaitPost(() => SEntMan.SpawnAtPosition("FirelockGlass", At(5)));
        await RunSeconds(2);
        var mule = Mule();
        var crate = await Load(mule);
        await Server.WaitPost(() => mule.Comp.AutoReturn = false);

        await Send(mule, beacons[0], beacons[9]);
        await RunSeconds(15);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_muleSystem.TryGetLoad(_mule, out _), Is.False, "Should still stop at the door and unload");
            Assert.That(GridPosition(crate).X, Is.LessThan(5f));
        });
    }

    [Test]
    public async Task NeverOpensSecurity()
    {
        var beacons = await Build(12, new() { [0] = "CargoBay", [9] = "Security" },
            new() { [5] = "AirlockSecurityLocked" });
        var mule = Mule();
        var crate = await Load(mule);
        await Server.WaitPost(() => mule.Comp.AutoReturn = false);

        await Send(mule, beacons[0], beacons[9]);
        await RunSeconds(20);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_muleSystem.TryGetLoad(_mule, out _), Is.False);
            Assert.That(GridPosition(crate).X, Is.GreaterThan(1f).And.LessThan(5f), "Should drop it outside the door");
        });
    }

    [Test]
    public async Task PanelNeedsCargoAccess()
    {
        await SpawnTarget("MobMulebot");
        var mule = STarget!.Value;

        await Activate();
        await Server.WaitAssertion(() => Assert.That(SEntMan.System<SharedUserInterfaceSystem>().IsUiOpen(mule, MuleUiKey.Key, SPlayer), Is.False, "Locked to cargo"));

        await PlaceInHands("CargoIDCard");
        await Activate();
        await Server.WaitAssertion(() => Assert.That(SEntMan.System<SharedUserInterfaceSystem>().IsUiOpen(mule, MuleUiKey.Key, SPlayer), "Opens for cargo"));
    }

    [Test]
    public async Task StopForgetsWhereItWasSent()
    {
        var beacons = await Build(12, new() { [0] = "CargoBay", [10] = "Bar" });
        var mule = Mule();

        await Send(mule, beacons[0], beacons[10]);
        await RunSeconds(1);
        await Server.WaitPost(() => _muleSystem.Command(_mule, MuleCommand.Stop));

        await Server.WaitAssertion(() =>
        {
            Assert.That(mule.Comp.Mode, Is.EqualTo(MuleMode.Idle));
            Assert.That(mule.Comp.Destination, Is.Null);
        });
    }

    [Test]
    public async Task UnloadsEvenWithReportsOff()
    {
        var beacons = await Build(12, new() { [0] = "CargoBay", [10] = "Bar" });
        var mule = Mule();
        var crate = await Load(mule);
        await Server.WaitPost(() =>
        {
            _muleSystem.Command(_mule, MuleCommand.Report);
            mule.Comp.AutoReturn = false;
        });

        await Send(mule, beacons[0], beacons[10]);
        await RunSeconds(20);

        await Server.WaitAssertion(() =>
        {
            Assert.That(mule.Comp.Report, Is.False);
            Assert.That(GridPosition(crate).X, Is.EqualTo(10.5f).Within(1f));
        });
    }

    [Test]
    public async Task SwitchesOffWhenTheCellRunsFlat()
    {
        var beacons = await Build(12, new() { [0] = "CargoBay", [10] = "Bar" });
        var mule = Mule();

        // Enough for about four tiles.
        await Server.WaitPost(() =>
        {
            SEntMan.System<PowerCellSystem>().TryGetBatteryFromSlot(_mule, out var battery);
            SEntMan.System<SharedBatterySystem>().SetCharge(battery!.Value.AsNullable(), mule.Comp.ChargePerTile * 4.5f);
        });

        await Send(mule, beacons[0], beacons[10]);
        await RunSeconds(15);

        await Server.WaitAssertion(() =>
        {
            Assert.That(mule.Comp.On, Is.False);
            Assert.That(mule.Comp.Mode, Is.EqualTo(MuleMode.Idle));
            Assert.That(GridPosition(_mule).X, Is.LessThan(7f), "Should stop well short of the bar");
        });
    }

    [Test]
    public async Task CellIsBehindThePanel()
    {
        await Build(4, new());
        await Server.WaitAssertion(() =>
        {
            var slots = SEntMan.System<ItemSlotsSystem>();
            var slot = SEntMan.GetComponent<PowerCellSlotComponent>(_mule).CellSlotId;
            Assert.That(slots.TryEject(_mule, slot, SPlayer, out _), Is.False, "Can't take the cell with the panel shut");
            Assert.That(Mule().Comp.On, "Still running");
        });
    }

    [Test]
    public async Task CuttingAPowerWireSwitchesItOff()
    {
        await Build(4, new());
        var mule = Mule();

        await Server.WaitPost(() => _muleSystem.SetWireCut(_mule, MuleWires.Power2, true));
        await Server.WaitAssertion(() =>
        {
            Assert.That(mule.Comp.On, Is.False, "The cut switches it off");
            Assert.That(_muleSystem.HasPower(_mule), Is.False);
        });

        await Server.WaitPost(() => _muleSystem.Command(_mule, MuleCommand.Power));
        await Server.WaitAssertion(() => Assert.That(mule.Comp.On, Is.False, "And it won't switch back on"));
    }

    [Test]
    public async Task WontSwitchOnWithThePanelOpen()
    {
        await Build(4, new());
        var mule = Mule();

        await Server.WaitPost(() =>
        {
            _muleSystem.Command(_mule, MuleCommand.Power);
            SetPanel(true);
            _muleSystem.Command(_mule, MuleCommand.Power);
        });

        await Server.WaitAssertion(() => Assert.That(mule.Comp.On, Is.False));
    }

    [Test]
    public async Task MotorWiresSetItsSpeed()
    {
        await Build(4, new());
        var mule = Mule();

        float Speed() => SEntMan.GetComponent<MovementSpeedModifierComponent>(_mule).CurrentWalkSpeed;

        float normal = 0, fast = 0, stopped = 0;
        await Server.WaitPost(() =>
        {
            normal = Speed();
            _muleSystem.SetWireCut(_mule, MuleWires.Motor1, true);
            fast = Speed();
            _muleSystem.SetWireCut(_mule, MuleWires.Motor2, true);
            stopped = Speed();
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(fast, Is.EqualTo(normal * mule.Comp.FastMotor).Within(0.01f));
            Assert.That(stopped, Is.EqualTo(0f));
        });
    }

    [Test]
    public async Task CutLoadCheckTakesAnythingAndRiders()
    {
        await Build(4, new());
        var mule = Mule();

        EntityUid toolbox = default, passenger = default;
        await Server.WaitPost(() =>
        {
            toolbox = SEntMan.SpawnAtPosition("ToolboxMechanical", At(0));
            passenger = SEntMan.SpawnAtPosition("MobHuman", At(1));
            Transform.SetLocalRotation(_mule, Direction.East.ToAngle());
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(_muleSystem.TryLoad(_mule, toolbox), Is.False, "Only crates while the load check is intact");
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(passenger, passenger, _mule), Is.False, "Nobody rides it yet");

            _muleSystem.SetWireCut(_mule, MuleWires.LoadCheck, true);
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(passenger, passenger, _mule), "Rides it once the load check is cut");
            Assert.That(_muleSystem.TryGetLoad(_mule, out var load) && load == passenger);
            Assert.That(_muleSystem.TryLoad(_mule, toolbox), Is.False, "A rider takes the place of a load");

            _muleSystem.SetWireCut(_mule, MuleWires.LoadCheck, false);
            Assert.That(_muleSystem.TryGetRider(_mule, out _), "Mending the wire doesn't throw them off");

            Assert.That(_muleSystem.Unload(_mule));
            Assert.That(_muleSystem.TryGetRider(_mule, out _), Is.False);
            Assert.That(GridPosition(passenger).X, Is.EqualTo(1.5f).Within(0.1f), "Let off on the tile ahead");
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(passenger, passenger, _mule), Is.False, "Nobody gets back on with the wire mended");

            _muleSystem.SetWireCut(_mule, MuleWires.LoadCheck, true);
            Assert.That(_muleSystem.TryLoad(_mule, toolbox), "Takes any loose object with the load check cut");
        });
    }

    [Test]
    public async Task CutSafetyWireKnocksPeopleOver()
    {
        var beacons = await Build(12, new() { [0] = "CargoBay", [10] = "Bar" });
        var mule = Mule();
        EntityUid bystander = default;
        await Server.WaitPost(() =>
        {
            bystander = SEntMan.SpawnAtPosition("MobHuman", At(5));
            _muleSystem.SetWireCut(_mule, MuleWires.Avoidance, true);
            mule.Comp.AutoReturn = false;
        });

        await Send(mule, beacons[0], beacons[10]);
        await RunSeconds(4);

        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<KnockedDownComponent>(bystander), "Should be knocked flat"));
    }

    [Test]
    public async Task SafeModeSlowsDownForSomeoneLyingThere()
    {
        var beacons = await Build(12, new() { [0] = "CargoBay", [10] = "Bar" });
        var mule = Mule();
        var victim = await LyingDown(5);
        await Server.WaitPost(() => mule.Comp.AutoReturn = false);

        await Send(mule, beacons[0], beacons[10]);
        var slowed = false;
        for (var i = 0; i < 12 && !slowed; i++)
        {
            await RunSeconds(0.5f);
            await Server.WaitPost(() => slowed = mule.Comp.CarefulUntil != null);
        }

        await Server.WaitAssertion(() =>
        {
            Assert.That(slowed, "Should slow right down");
            Assert.That(Blunt(victim), Is.Zero, "And not hurt them");
        });
    }

    [Test]
    public async Task EmaggedRunsPeopleOver()
    {
        var beacons = await Build(12, new() { [0] = "CargoBay", [10] = "Bar" });
        var mule = Mule();
        var victim = await LyingDown(5);

        await Server.WaitPost(() =>
        {
            mule.Comp.AutoReturn = false;
            SetPanel(true);
            var emag = SEntMan.SpawnAtPosition("Emag", At(0));
            SEntMan.System<EmagSystem>().TryEmagEffect(emag, SPlayer, _mule);
            SetPanel(false);
        });

        await Server.WaitAssertion(() => Assert.That(SEntMan.System<EmagSystem>().CheckFlag(_mule, EmagType.Interaction)));

        await Send(mule, beacons[0], beacons[10]);
        await RunSeconds(8);

        await Server.WaitAssertion(() =>
        {
            // 5-15 blunt, six times over.
            Assert.That(Blunt(victim), Is.InRange(30f, 90f), "Should have driven over them once");
        });
    }

    [Test]
    public async Task StandingStillItDoesNotKeepRunningSomeoneOver()
    {
        var beacons = await Build(12, new() { [0] = "CargoBay", [10] = "Bar" });
        var mule = Mule();
        var victim = await LyingDown(3);
        await Server.WaitPost(() => _muleSystem.SetWireCut(_mule, MuleWires.Avoidance, true));
        await Send(mule, beacons[0], beacons[10]);

        // Stop it the moment it reaches them.
        var first = 0f;
        for (var i = 0; i < 60 && first <= 0f; i++)
        {
            await RunTicks(3);
            await Server.WaitPost(() => first = Blunt(victim));
        }

        await Server.WaitPost(() =>
        {
            _muleSystem.SetWireCut(_mule, MuleWires.Motor1, true);
            _muleSystem.SetWireCut(_mule, MuleWires.Motor2, true);
        });
        await Server.WaitAssertion(() => Assert.That(first, Is.GreaterThan(0f), "Should have driven over them"));

        await RunSeconds(5);
        await Server.WaitAssertion(() => Assert.That(Blunt(victim), Is.LessThanOrEqualTo(first), "Once per tile it drives onto"));
    }

    [Test]
    public async Task SwitchedOnOverSomeoneItLeavesThemAlone()
    {
        var beacons = await Build(12, new() { [0] = "CargoBay", [10] = "Bar" });
        var mule = Mule();
        var victim = await LyingDown(0);
        await Server.WaitPost(() =>
        {
            _muleSystem.SetWireCut(_mule, MuleWires.Avoidance, true);
            _muleSystem.SetWireCut(_mule, MuleWires.Motor1, true);
            _muleSystem.SetWireCut(_mule, MuleWires.Motor2, true);
        });

        await Send(mule, beacons[0], beacons[10]);
        await RunSeconds(3);
        await Server.WaitAssertion(() => Assert.That(Blunt(victim), Is.EqualTo(0f), "It never drove onto their tile"));
    }

    [Test]
    public async Task PanelIsLockedUntilUnlocked()
    {
        await Build(4, new());
        var panel = SEntMan.GetComponent<WiresPanelComponent>(_mule);
        var wires = SEntMan.System<SharedWiresSystem>();

        await Server.WaitAssertion(() => Assert.That(wires.CanTogglePanel((_mule, panel), SPlayer), Is.False, "Locked without an ID"));

        await PlaceInHands("CargoIDCard");
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<LockSystem>().TryUnlock(_mule, SPlayer, skipDoAfter: true));
            Assert.That(wires.CanTogglePanel((_mule, panel), SPlayer), "Opens once cargo unlocks it");
        });
    }

    [Test]
    public async Task EmagUnlocksThePanelThenHacksIt()
    {
        await Build(4, new());
        var panel = SEntMan.GetComponent<WiresPanelComponent>(_mule);
        var wires = SEntMan.System<SharedWiresSystem>();
        var emags = SEntMan.System<EmagSystem>();

        await Server.WaitAssertion(() =>
        {
            var emag = SEntMan.SpawnAtPosition("Emag", At(0));
            Assert.That(emags.TryEmagEffect(emag, SPlayer, _mule));
            Assert.That(emags.CheckFlag(_mule, EmagType.Interaction), Is.False, "The first swipe only breaks the panel lock");
            Assert.That(wires.CanTogglePanel((_mule, panel), SPlayer));

            wires.TogglePanel(_mule, panel, true);
            Assert.That(emags.TryEmagEffect(emag, SPlayer, _mule));
            Assert.That(emags.CheckFlag(_mule, EmagType.Interaction), "The second, with the panel open, emags it");
        });
    }

    [Test]
    public async Task CutBeaconWireMeansNoRoute()
    {
        var beacons = await Build(12, new() { [0] = "CargoBay", [10] = "Bar" });
        var mule = Mule();
        await Server.WaitPost(() => _muleSystem.SetWireCut(_mule, MuleWires.Beacon, true));

        await Send(mule, beacons[0], beacons[10]);
        await RunSeconds(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(mule.Comp.Waypoints, Is.Empty);
            Assert.That(GridPosition(_mule).X, Is.LessThan(1.5f), "Shouldn't go anywhere");
        });
    }

    private float Blunt(EntityUid uid)
    {
        return SEntMan.System<DamageableSystem>().GetAllDamage(uid).DamageDict.GetValueOrDefault("Blunt").Float();
    }

    private async Task<EntityUid> LyingDown(int x)
    {
        EntityUid victim = default;
        await Server.WaitPost(() =>
        {
            victim = SEntMan.SpawnAtPosition("MobHuman", At(x));
            // No air here; keep the vacuum from adding its own blunt damage.
            SEntMan.EnsureComponent<Content.Server.Atmos.Components.PressureImmunityComponent>(victim);
            SEntMan.System<SharedStunSystem>().TryKnockdown(victim, TimeSpan.FromMinutes(1));
        });
        await RunTicks(5);
        await Server.WaitAssertion(() => Assert.That(SEntMan.System<StandingStateSystem>().IsDown(victim)));
        return victim;
    }

    [Test]
    public async Task WontSwitchOnWithoutACell()
    {
        await Build(4, new());
        var mule = Mule();

        await Server.WaitPost(() =>
        {
            _muleSystem.Command(_mule, MuleCommand.Power);
            var slot = SEntMan.GetComponent<PowerCellSlotComponent>(_mule);
            SEntMan.System<SharedContainerSystem>().EmptyContainer(
                SEntMan.System<SharedContainerSystem>().GetContainer(_mule, slot.CellSlotId));
        });
        await Server.WaitAssertion(() => Assert.That(mule.Comp.On, Is.False, "Switched off first"));

        await Server.WaitPost(() => _muleSystem.Command(_mule, MuleCommand.Power));
        await Server.WaitAssertion(() => Assert.That(mule.Comp.On, Is.False));
    }

    /// <summary>
    /// Builds a walled corridor along <see cref="Row"/> with the MULE at its start, and returns the beacons by tile.
    /// </summary>
    private async Task<Dictionary<int, EntityUid>> Build(int length, Dictionary<int, string> beacons, Dictionary<int, string>? doors = null)
    {
        for (var x = -1; x <= length + 1; x++)
        {
            for (var y = Row - 1; y <= Row + 1; y++)
            {
                await SetTile("Plating", SEntMan.GetNetCoordinates(Tile(x, y)), MapData.Grid);
            }
        }

        var made = new Dictionary<int, EntityUid>();
        await Server.WaitPost(() =>
        {
            for (var x = -1; x <= length + 1; x++)
            {
                SEntMan.SpawnAtPosition("WallSolid", Tile(x, Row - 1));
                SEntMan.SpawnAtPosition("WallSolid", Tile(x, Row + 1));
            }

            SEntMan.SpawnAtPosition("WallSolid", Tile(-1, Row));
            SEntMan.SpawnAtPosition("WallSolid", Tile(length + 1, Row));

            foreach (var (x, proto) in doors ?? new())
            {
                var door = SEntMan.SpawnAtPosition(proto, Tile(x, Row));
                SEntMan.System<SharedPowerReceiverSystem>().SetNeedsPower(door, false);
            }

            foreach (var (x, name) in beacons)
            {
                made[x] = SEntMan.SpawnAtPosition("DefaultStationBeacon" + name, Tile(x, Row));
            }

            _mule = SEntMan.SpawnAtPosition("MobMulebot", At(0));
        });

        // It doesn't run anyone over while floating.
        await AddGravity();

        // Let the pathfinder catch up with the new walls and doors.
        await RunSeconds(2);
        return made;
    }

    private async Task<EntityUid> Load(Entity<MuleComponent> mule)
    {
        EntityUid crate = default;
        await Server.WaitPost(() => crate = SEntMan.SpawnAtPosition("CrateGenericSteel", At(1)));
        await Server.WaitAssertion(() => Assert.That(_muleSystem.TryLoad(_mule, crate)));
        return crate;
    }

    private async Task Send(Entity<MuleComponent> mule, EntityUid home, EntityUid destination)
    {
        await Server.WaitPost(() =>
        {
            _muleSystem.SetBeacon(_mule, home, true);
            _muleSystem.SetBeacon(_mule, destination, false);
            _muleSystem.Command(_mule, MuleCommand.Go);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(mule.Comp.On);
            Assert.That(mule.Comp.Mode, Is.EqualTo(MuleMode.Delivering));
        });
    }

    /// <summary>
    /// Unlocks the maintenance panel and opens or shuts it.
    /// </summary>
    private void SetPanel(bool open)
    {
        SEntMan.System<LockSystem>().Unlock(_mule, null);
        SEntMan.System<SharedWiresSystem>().TogglePanel(_mule, SEntMan.GetComponent<WiresPanelComponent>(_mule), open);
    }

    private Entity<MuleComponent> Mule()
    {
        return (_mule, SEntMan.GetComponent<MuleComponent>(_mule));
    }

    private float Charge()
    {
        SEntMan.System<PowerCellSystem>().TryGetBatteryFromSlot(_mule, out var battery);
        return SEntMan.System<SharedBatterySystem>().GetCharge(battery!.Value.AsNullable());
    }

    private EntityCoordinates Tile(int x, int y)
    {
        return new EntityCoordinates(MapData.Grid, new Vector2(x + 0.5f, y + 0.5f));
    }

    private EntityCoordinates At(int x)
    {
        return Tile(x, Row);
    }

    private Vector2 GridPosition(EntityUid uid)
    {
        return Transform.GetMapCoordinates(uid).Position - Transform.GetMapCoordinates(MapData.Grid).Position;
    }
}
