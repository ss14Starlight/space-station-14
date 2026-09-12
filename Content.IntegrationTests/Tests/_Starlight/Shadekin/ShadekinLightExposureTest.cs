using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Starlight.Shadekin;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Shadekin;

[TestFixture]
[TestOf(typeof(ShadekinSystem))]
public sealed class ShadekinLightExposureTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: SLLightExposureTestLight
  components:
  - type: Transform
  - type: PointLight
    radius: 6
    energy: 2

- type: entity
  id: SLLightExposureTestDummy
  components:
  - type: Transform

- type: entity
  id: SLLightExposureTestBag
  components:
  - type: Transform
  - type: ContainerContainer
";

    [Test]
    public async Task LightExposureFollowsLights()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;

        var shadekin = server.System<ShadekinSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var containerSys = server.System<SharedContainerSystem>();

        var testMap = await pair.CreateTestMap();
        var coords = testMap.GridCoords;

        EntityUid dummy = default;
        EntityUid light = default;
        EntityUid bag = default;

        await server.WaitPost(() =>
        {
            dummy = entMan.SpawnEntity("SLLightExposureTestDummy", coords);
            light = entMan.SpawnEntity("SLLightExposureTestLight", coords.Offset(new Vector2(1, 0)));
            bag = entMan.SpawnEntity("SLLightExposureTestBag", coords.Offset(new Vector2(3, 0)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(shadekin.GetLightExposure(dummy),
                Is.GreaterThan(0f),
                "A light standing right next to us should light us up.");
        });

        await server.WaitPost(() => xformSys.SetWorldPosition(light, new Vector2(100, 100)));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(shadekin.GetLightExposure(dummy),
                Is.EqualTo(0f),
                "A light on the other side of the map shouldn't reach us.");
        });

        await server.WaitPost(() => xformSys.SetCoordinates(light, coords.Offset(new Vector2(1, 0))));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(shadekin.GetLightExposure(dummy),
                Is.GreaterThan(0f),
                "A light that came back into range should light us up again.");
        });

        await server.WaitPost(() =>
        {
            var container = containerSys.EnsureContainer<Container>(bag, "test-container");
            Assert.That(containerSys.Insert(light, container), Is.True);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(shadekin.GetLightExposure(dummy),
                Is.EqualTo(0f),
                "A light inside a container shouldn't light up the room.");
        });
    }
}
