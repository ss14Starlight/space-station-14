using Content.Server.Construction.Components;
using Content.Server.Power.Components;
using Content.Server.Wires;
using Content.Shared._Starlight.Power.Components;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Content.Shared.UserInterface;
using Content.Shared.Wires;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Power;

[TestFixture, TestOf(typeof(ActivatableUIRequiresPowerComponent))]
public sealed class ActivatableUIRequiresPowerTest
{
    private static readonly EntProtoId ComputerCommsPrototype = "ComputerComms";
    private static readonly EntProtoId ComputerCommsFilledPrototype = "ComputerCommsFilled";
    private static readonly EntProtoId KeycardAuthPrototype = "KeycardAuth";

    [Test]
    public async Task ComputerCommsAndKeycardAuthUseCellPowerFallback()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var prototype = protoMan.Index<EntityPrototype>(ComputerCommsPrototype.Id);

            Assert.That(
                prototype.TryComp<ActivatableUIRequiresPowerComponent>(
                    out var powerRequirement,
                    entMan.ComponentFactory),
                Is.True);
            Assert.That(powerRequirement.AllowPowerCellFallback, Is.True);
            Assert.That(
                prototype.TryComp<ActivatableUIRequiresPowerCellComponent>(
                    out _,
                    entMan.ComponentFactory),
                Is.True);

            var keycardAuth = protoMan.Index<EntityPrototype>(KeycardAuthPrototype.Id);
            Assert.That(
                keycardAuth.TryComp<ActivatableUIRequiresPowerComponent>(
                    out var keycardPowerRequirement,
                    entMan.ComponentFactory),
                Is.True);
            Assert.That(keycardPowerRequirement.AllowPowerCellFallback, Is.True);
            Assert.That(
                keycardAuth.TryComp<ActivatableUIRequiresPowerCellComponent>(
                    out _,
                    entMan.ComponentFactory),
                Is.True);
            Assert.That(
                keycardAuth.TryComp<PowerCellFallbackVisualsComponent>(
                    out _,
                    entMan.ComponentFactory),
                Is.True);
            Assert.That(
                keycardAuth.TryComp<ConstructionComponent>(
                    out var keycardAuthConstruction,
                    entMan.ComponentFactory),
                Is.True);
            Assert.That(keycardAuthConstruction.Node, Is.EqualTo("auth"));
            Assert.That(keycardAuthConstruction.DeconstructionNode, Is.Null);

            var powerCells = server.System<PowerCellSystem>();
            var wires = server.System<WiresSystem>();
            foreach (var prototypeId in new[] { ComputerCommsFilledPrototype.Id, KeycardAuthPrototype.Id })
            {
                var terminal = entMan.SpawnEntity(prototypeId, MapCoordinates.Nullspace);
                var receiver = entMan.GetComponent<ApcPowerReceiverComponent>(terminal);
                Assert.That(powerCells.HasBattery(terminal), Is.True);
                Assert.That(powerCells.HasCharge(terminal, 1f), Is.True);

                receiver.Powered = false;
                var cellPoweredAttempt = new ActivatableUIOpenAttemptEvent(EntityUid.Invalid, silent: true);
                entMan.EventBus.RaiseLocalEvent(terminal, cellPoweredAttempt);
                Assert.That(cellPoweredAttempt.Cancelled, Is.False);

                var panel = entMan.GetComponent<WiresPanelComponent>(terminal);
                Assert.That(wires.TogglePanel(terminal, panel, true), Is.True);
                Assert.That(powerCells.TryEjectBatteryFromSlot(terminal, out var ejectedCell), Is.True);
                Assert.That(powerCells.HasBattery(terminal), Is.False);
                Assert.That(wires.TogglePanel(terminal, panel, false), Is.True);

                var unpoweredAttempt = new ActivatableUIOpenAttemptEvent(EntityUid.Invalid, silent: true);
                entMan.EventBus.RaiseLocalEvent(terminal, unpoweredAttempt);
                Assert.That(unpoweredAttempt.Cancelled, Is.True);

                receiver.Powered = true;
                var poweredAttempt = new ActivatableUIOpenAttemptEvent(EntityUid.Invalid, silent: true);
                entMan.EventBus.RaiseLocalEvent(terminal, poweredAttempt);
                Assert.That(poweredAttempt.Cancelled, Is.False);

                entMan.DeleteEntity(terminal);
                if (ejectedCell is { } cell)
                    entMan.DeleteEntity(cell);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TerminalBackupCellsRequireOpenPanelForEjection()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var powerCells = server.System<PowerCellSystem>();
            var wires = server.System<WiresSystem>();

            foreach (var prototypeId in new[] { ComputerCommsFilledPrototype.Id, KeycardAuthPrototype.Id })
            {
                var terminal = entMan.SpawnEntity(prototypeId, MapCoordinates.Nullspace);

                Assert.That(powerCells.HasBattery(terminal), Is.True);
                Assert.That(powerCells.TryEjectBatteryFromSlot(terminal, out _), Is.False);

                var panel = entMan.GetComponent<WiresPanelComponent>(terminal);
                Assert.That(wires.TogglePanel(terminal, panel, true), Is.True);
                Assert.That(powerCells.TryEjectBatteryFromSlot(terminal, out var ejected), Is.True);

                if (ejected is { } cell)
                    entMan.DeleteEntity(cell);
                entMan.DeleteEntity(terminal);
            }
        });

        await pair.CleanReturnAsync();
    }
}
