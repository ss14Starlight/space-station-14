#nullable enable

using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Wires;
using Content.Shared.PowerCell;
using Content.Shared.UserInterface;
using Content.Shared.Wires;

namespace Content.IntegrationTests.Tests._Starlight.Power;

public sealed class ActivatableUIRequiresPowerInteractionTest : InteractionTest
{
    [TestCase("ComputerCommsFilled")]
    [TestCase("KeycardAuth")]
    public async Task EjectingFallbackCellClosesOpenUi(string prototypeId)
    {
        var uiKey = await SpawnAndOpenUi(prototypeId);
        var terminal = STarget!.Value;

        await Server.WaitPost(() =>
        {
            var wires = Server.System<WiresSystem>();
            var powerCells = Server.System<PowerCellSystem>();
            var panel = SEntMan.GetComponent<WiresPanelComponent>(terminal);
            Assert.That(wires.TogglePanel(terminal, panel, true), Is.True);
            Assert.That(powerCells.TryEjectBatteryFromSlot(terminal, out var ejectedCell), Is.True);
            Assert.That(ejectedCell, Is.Not.Null);
            Assert.That(wires.TogglePanel(terminal, panel, false), Is.True);

            SEntMan.DeleteEntity(ejectedCell!.Value);
        });

        await RunTicks(1);
        Assert.That(IsUiOpen(uiKey), Is.False, "UI remained open after its backup cell was ejected.");
    }

    [TestCase("ComputerCommsFilled")]
    [TestCase("KeycardAuth")]
    public async Task EmptyingFallbackCellClosesOpenUi(string prototypeId)
    {
        var uiKey = await SpawnAndOpenUi(prototypeId);
        var terminal = STarget!.Value;

        await Server.WaitPost(() =>
        {
            var powerCells = Server.System<PowerCellSystem>();
            var batteries = Server.System<BatterySystem>();

            if (!powerCells.TryGetBatteryFromSlot(terminal, out var battery))
            {
                Assert.Fail("Expected the terminal to have a backup battery.");
                return;
            }

            batteries.SetCharge(battery.Value.AsNullable(), 0f);
        });

        await RunTicks(1);
        Assert.That(IsUiOpen(uiKey), Is.False, "UI remained open after its backup cell was depleted.");
    }

    private async Task<Enum> SpawnAndOpenUi(string prototypeId)
    {
        await SpawnTarget(prototypeId);
        var terminal = STarget!.Value;
        Enum? uiKey = null;

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.GetComponent<ApcPowerReceiverComponent>(terminal).Powered, Is.False);
            Assert.That(Server.System<PowerCellSystem>().HasCharge(terminal, 1f), Is.True);

            uiKey = SEntMan.GetComponent<ActivatableUIComponent>(terminal).Key;
            Assert.That(uiKey, Is.Not.Null);
        });

        await Activate();
        Assert.That(IsUiOpen(uiKey!), Is.True, $"UI for {prototypeId} did not open with a usable backup cell.");
        return uiKey!;
    }
}
