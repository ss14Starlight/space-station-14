using Content.Server.Wires;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.PowerCell;
using Content.Shared.Wires;

namespace Content.IntegrationTests.Tests._Starlight.Construction.Interaction;

[TestFixture]
public sealed class CommunicationsConsoleConstructionTest : InteractionTest
{
    private const string ComputerFrame = "ComputerFrame";

    [Test]
    public async Task RebuildingCommunicationsConsoleDoesNotRegenerateBattery()
    {
        await StartDeconstruction("ComputerComms");
        var console = STarget!.Value;

        await Server.WaitPost(() =>
        {
            var wires = Server.System<WiresSystem>();
            var panel = SEntMan.GetComponent<WiresPanelComponent>(console);
            Assert.That(wires.TogglePanel(console, panel, true), Is.True);

            var powerCells = Server.System<PowerCellSystem>();
            Assert.That(powerCells.TryEjectBatteryFromSlot(console, out var ejectedCell), Is.True);
            if (ejectedCell is { } cell)
                SEntMan.DeleteEntity(cell);

            Assert.That(wires.TogglePanel(console, panel, false), Is.True);
        });
        await RunTicks(1);

        await Interact(Screw, Pry);
        AssertPrototype(ComputerFrame);

        await Interact(Pry, Cut, Screw, Pry);
        AssertPrototype(ComputerFrame);

        await Interact(
            "CommsComputerCircuitboard",
            Screw,
            (Cable, 5),
            (Glass, 2),
            Screw);
        AssertPrototype("ComputerComms");

        var rebuiltConsole = STarget!.Value;
        await Server.WaitPost(() =>
            Assert.That(Server.System<PowerCellSystem>().HasBattery(rebuiltConsole), Is.False));
    }
}
