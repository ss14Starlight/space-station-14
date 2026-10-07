using Content.Server.Power.Components;
using Content.Shared._Starlight.Power.Components;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Power;

[TestFixture, TestOf(typeof(ActivatableUIRequiresPowerComponent))]
public sealed class ActivatableUIRequiresPowerTest
{
    private static readonly EntProtoId ComputerCommsPrototype = "ComputerComms";
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

            var powerCells = server.System<PowerCellSystem>();
            foreach (var prototypeId in new[] { ComputerCommsPrototype.Id, KeycardAuthPrototype.Id })
            {
                var terminal = entMan.SpawnEntity(prototypeId, MapCoordinates.Nullspace);
                var receiver = entMan.GetComponent<ApcPowerReceiverComponent>(terminal);
                Assert.That(powerCells.HasBattery(terminal), Is.False);

                receiver.Powered = true;
                var poweredAttempt = new ActivatableUIOpenAttemptEvent(EntityUid.Invalid, silent: true);
                entMan.EventBus.RaiseLocalEvent(terminal, poweredAttempt);
                Assert.That(poweredAttempt.Cancelled, Is.False);

                receiver.Powered = false;
                var unpoweredAttempt = new ActivatableUIOpenAttemptEvent(EntityUid.Invalid, silent: true);
                entMan.EventBus.RaiseLocalEvent(terminal, unpoweredAttempt);
                Assert.That(unpoweredAttempt.Cancelled, Is.True);

                entMan.DeleteEntity(terminal);
            }
        });

        await pair.CleanReturnAsync();
    }
}
