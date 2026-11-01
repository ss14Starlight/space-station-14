using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Power.Components;
using Content.Shared.Coordinates;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Power;

[TestFixture, TestOf(typeof(SharedPowerStateSystem))]
public sealed class PowerStatePrototypeTest : GameTest
{
    #region Starlight
    [TestPrototypes]
    private const string Prototypes = @"
-
    type: entity
    id: PowerStateApcReceiverDummy
    components:
        -
            type: ApcPowerReceiver
        -
            type: ExtensionCableReceiver
        -
            type: Transform
            anchored: true
        -
            type: PowerState
            isWorking: false
            idlePowerDraw: 10
            workingPowerDraw: 50
";
    #endregion

    /// <summary>
    /// Asserts that the <see cref="SharedApcPowerReceiverComponent"/>'s load is the same
    /// as the idle or working power draw from <see cref="PowerStateComponent"/>,
    /// depending on the current power state.
    /// </summary>
    [Test]
    public async Task AssertApcPowerMatchesPowerState()
    {
        var pair = Pair;
        var server = pair.Server;

        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(delegate
            {
                foreach (var prototype in protoMan.EnumeratePrototypes<EntityPrototype>()
                            .Where(p => !p.Abstract)
                            .Where(p => !pair.IsTestPrototype(p)))
                {
                    // Starlight - start
                    if (!prototype.TryComp<PowerStateComponent>(out var powerStateComp, entMan.ComponentFactory))
                        continue;
                    // Starlight - end

                    // LESSON LEARNED:
                    // ENSURE THAT THE COMPONENT YOU ARE TRYING TO GET IS THE SERVER-SIDE VARIANT
                    // Starlight - start
                    if (!prototype.TryComp<ApcPowerReceiverComponent>(out var powerReceiverComp, entMan.ComponentFactory))
                    {
                        Assert.Fail(
                            $"Entity prototype '{prototype.ID}' has a PowerStateComponent but is missing the required ApcPowerReceiverComponent.");
                    }
                    // Starlight - end

                    var expectedLoad = powerStateComp.IsWorking
                        ? powerStateComp.WorkingPowerDraw
                        : powerStateComp.IdlePowerDraw;

                    Assert.That(powerReceiverComp.Load,
                        Is.EqualTo(expectedLoad),
                        $"Entity prototype '{prototype.ID}' has mismatched power draw between PowerStateComponent and SharedApcPowerReceiverComponent.");
                }
            });
        });

        // Starlight - start
        var mapSys = entMan.System<SharedMapSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapId);
            var grid = mapSys.CreateGridEntity(mapId);
            mapSys.SetTile(grid, Vector2i.Zero, new Tile(1));

            var ent = entMan.SpawnEntity("PowerStateApcReceiverDummy", grid.Owner.ToCoordinates());

            var receiver = entMan.GetComponent<ApcPowerReceiverComponent>(ent);
            var powerState = entMan.GetComponent<PowerStateComponent>(ent);
            var system = entMan.System<SharedPowerStateSystem>();
            Entity<PowerStateComponent> entity = (ent, powerState);

            void AssertPowerState(bool isWorking, float expectedLoad) => Assert.Multiple(() =>
            {
                Assert.That(powerState.IsWorking, Is.EqualTo(isWorking));
                Assert.That(receiver.Load, Is.EqualTo(expectedLoad).Within(0.01f));
            });

            AssertPowerState(false, powerState.IdlePowerDraw);

            system.SetWorkingState(entity, false);
            AssertPowerState(false, powerState.IdlePowerDraw);

            system.SetWorkingState(entity, true);
            AssertPowerState(true, powerState.WorkingPowerDraw);

            system.SetWorkingState(entity, true);
            AssertPowerState(true, powerState.WorkingPowerDraw);

            system.SetWorkingState(entity, false);
            AssertPowerState(false, powerState.IdlePowerDraw);
        });
        // Starlight - end
    }
}
