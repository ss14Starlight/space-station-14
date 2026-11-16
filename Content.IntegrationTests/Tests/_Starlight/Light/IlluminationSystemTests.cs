using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Light;
using Content.Shared._Starlight.Light;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System.IO;
using System.Numerics;

namespace Content.IntegrationTests.Tests._Starlight.Light;

[TestFixture]
[TestOf(typeof(IlluminationSystem))]
public sealed class IlluminationSystemTests : GameTest
{

    [Test]
    public async Task CarriedLightFollowsHolder()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();

        var illumination = server.System<SharedIlluminationSystem>();
        var pointLight = server.System<SharedPointLightSystem>();
        var containers = server.System<SharedContainerSystem>();
        var transform = server.System<SharedTransformSystem>();

        EntityUid holder = default;
        EntityUid light = default;
        var query = new IlluminationQuery(IgnoreAmbient: true);

        await server.WaitAssertion(() =>
        {
            holder = server.EntMan.Spawn(null, new MapCoordinates(100, 100, map.MapId));
            light = server.EntMan.Spawn(null, new MapCoordinates(100, 100, map.MapId));

            var lightComp = pointLight.EnsureLight(light);
            pointLight.SetRadius(light, 5f, lightComp);
            pointLight.SetEnergy(light, 2f, lightComp);
            pointLight.SetEnabled(light, true, lightComp);

            var hand = containers.EnsureContainer<Container>(holder, "hand");
            hand.OccludesLight = false;
            Assert.That(containers.Insert(light, hand, force: true), Is.True);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(illumination.GetLightLevel(new MapCoordinates(102, 100, map.MapId), query), Is.GreaterThan(0f),
                "A light in a hand should shine around its holder.");

            transform.SetMapCoordinates(holder, new MapCoordinates(150, 100, map.MapId));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(illumination.GetLightLevel(new MapCoordinates(102, 100, map.MapId), query), Is.Zero,
                    "The light should have left with its holder.");
                Assert.That(illumination.GetLightLevel(new MapCoordinates(152, 100, map.MapId), query), Is.GreaterThan(0f),
                    "The light should shine where its holder went.");
            }

            var hand = containers.GetContainer(holder, "hand");
            hand.OccludesLight = true;
            Assert.That(illumination.GetLightLevel(new MapCoordinates(152, 100, map.MapId), query), Is.Zero,
                "A light shut in a container that blocks light shouldn't shine.");

            server.EntMan.DeleteEntity(holder);
        });
    }

    [Test]
    public async Task AnchoredLightFollowsUnanchor()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();

        var illumination = server.System<SharedIlluminationSystem>();
        var pointLight = server.System<SharedPointLightSystem>();
        var transform = server.System<SharedTransformSystem>();

        EntityUid light = default;
        var query = new IlluminationQuery(IgnoreAmbient: true);
        var near = map.GridCoords.Offset(new Vector2(1, 0));

        await server.WaitAssertion(() =>
        {
            light = server.EntMan.SpawnAtPosition(null, map.GridCoords);

            var lightComp = pointLight.EnsureLight(light);
            pointLight.SetRadius(light, 5f, lightComp);
            pointLight.SetEnergy(light, 2f, lightComp);
            pointLight.SetEnabled(light, true, lightComp);

            Assert.That(transform.AnchorEntity(light), Is.True);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(illumination.GetLightLevel(transform.ToMapCoordinates(near), query), Is.GreaterThan(0f),
                "An anchored light should shine next to it.");

            transform.Unanchor(light);
            transform.SetMapCoordinates(light, new MapCoordinates(200, 200, map.MapId));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(illumination.GetLightLevel(transform.ToMapCoordinates(near), query), Is.Zero,
                    "An unanchored light shouldn't stay stuck where it was anchored.");
                Assert.That(illumination.GetLightLevel(new MapCoordinates(201, 200, map.MapId), query), Is.GreaterThan(0f),
                    "An unanchored light should shine where it went.");
            }

            server.EntMan.DeleteEntity(light);
        });
    }

    [Test]
    public async Task OccludersBlockShadowCastingLights()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();

        var illumination = server.System<SharedIlluminationSystem>();
        var pointLight = server.System<SharedPointLightSystem>();

        EntityUid light = default;
        EntityUid wall = default;
        var query = new IlluminationQuery(IgnoreAmbient: true);
        var target = new MapCoordinates(302, 300, map.MapId);

        await server.WaitAssertion(() =>
        {
            light = server.EntMan.Spawn(null, new MapCoordinates(300, 300, map.MapId));
            wall = server.EntMan.Spawn(null, new MapCoordinates(301, 300, map.MapId));
            server.EntMan.AddComponent<OccluderComponent>(wall);

            var lightComp = pointLight.EnsureLight(light);
            pointLight.SetRadius(light, 5f, lightComp);
            pointLight.SetEnergy(light, 2f, lightComp);
            pointLight.SetEnabled(light, true, lightComp);
            pointLight.SetCastShadows(light, true, lightComp);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(illumination.GetLightLevel(target, query), Is.Zero, "The wall should shade us.");
                Assert.That(illumination.GetLightLevel(new MapCoordinates(300, 302, map.MapId), query), Is.GreaterThan(0f),
                    "Nothing stands between the light and this side.");
            }

            pointLight.SetCastShadows(light, false);
            Assert.That(illumination.GetLightLevel(target, query), Is.GreaterThan(0f),
                "A light that casts no shadows goes through walls, same as on the client.");

            server.EntMan.DeleteEntity(light);
            server.EntMan.DeleteEntity(wall);
        });
    }

    [Test]
    public async Task AllLightMasksReadable()
    {
        var server = Pair.Server;

        await server.WaitAssertion(() =>
        {
            var resources = server.ResolveDependency<IResourceManager>();
            var protos = server.ResolveDependency<IPrototypeManager>();

            using (Assert.EnterMultipleScope())
            {
                foreach (var mask in protos.EnumeratePrototypes<LightMaskPrototype>())
                {
                    using var stream = new MemoryStream();
                    using (var file = resources.ContentFileRead(mask.MaskPath))
                        file.CopyTo(stream);
                    stream.Position = 0;

                    Assert.That(LightMaskReader.TryRead(stream, out var width, out _, out var values), Is.True,
                        $"Light mask {mask.ID} ({mask.MaskPath}) can't be read on the server.");

                    if (mask.ID != "SLCurve")
                        continue;

                    // Sampled with PIL from cone_curve.png.
                    Assert.That(values[(128 * width) + 256], Is.EqualTo(255), "Middle of the beam.");
                    Assert.That(values[(243 * width) + 256], Is.EqualTo(132), "Right at the bulb.");
                    Assert.That(values[(256 * width) + 384], Is.Zero, "Beside the bulb.");
                }
            }
        });
    }

    [Test]
    public async Task FlashlightMaskShinesSouthUnrotated()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();

        var illumination = server.System<SharedIlluminationSystem>();
        var pointLight = server.System<SharedPointLightSystem>();

        EntityUid light = default;
        var query = new IlluminationQuery(IgnoreAmbient: true);

        await server.WaitAssertion(() =>
        {
            light = server.EntMan.Spawn("FlashlightLantern", new MapCoordinates(500, 500, map.MapId));
            pointLight.SetEnabled(light, true);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(illumination.GetLightLevel(new MapCoordinates(500, 497, map.MapId), query), Is.GreaterThan(0f),
                    "The beam should go south.");
                Assert.That(illumination.GetLightLevel(new MapCoordinates(500, 503, map.MapId), query), Is.Zero,
                    "Nothing should come out the back.");
                Assert.That(illumination.GetLightLevel(new MapCoordinates(503, 500, map.MapId), query), Is.Zero,
                    "Nothing should come out the side.");
            }

            server.EntMan.DeleteEntity(light);
        });
    }

    [Test]
    public async Task LightInsideOccluderStillShinesOut()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();

        var illumination = server.System<SharedIlluminationSystem>();
        var pointLight = server.System<SharedPointLightSystem>();

        EntityUid light = default;
        EntityUid wall = default;
        var query = new IlluminationQuery(IgnoreAmbient: true);

        await server.WaitAssertion(() =>
        {
            wall = server.EntMan.Spawn(null, new MapCoordinates(400, 400, map.MapId));
            server.EntMan.AddComponent<OccluderComponent>(wall);

            // Same offset as the Starlight wall lights, leaving the light just inside the wall.
            light = server.EntMan.Spawn(null, new MapCoordinates(400, 400, map.MapId));
            var lightComp = pointLight.EnsureLight(light);
            lightComp.Offset = new Vector2(0, -0.495f);
            pointLight.SetRadius(light, 10f, lightComp);
            pointLight.SetEnergy(light, 0.8f, lightComp);
            pointLight.SetEnabled(light, true, lightComp);
            pointLight.SetCastShadows(light, true, lightComp);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(illumination.GetLightLevel(new MapCoordinates(400, 397, map.MapId), query), Is.GreaterThan(0f),
                    "The wall the light is mounted in shouldn't shade the room.");
                Assert.That(illumination.GetLightLevel(new MapCoordinates(400, 402, map.MapId), query), Is.Zero,
                    "The light shouldn't leak through its own wall to the other side.");
            }

            server.EntMan.DeleteEntity(light);
            server.EntMan.DeleteEntity(wall);
        });
    }
}
