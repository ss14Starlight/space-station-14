using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared.Body.Organ;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Starlight.Body;

[TestFixture]
public sealed class SaveLoadReparentTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  name: HumanBodyDummy
  id: HumanBodyDummy
  components:
  - type: SLBody
    prefab: Human
";

    [Test]
    public async Task Test()
    {
        var server = Pair.Server;

        var entities = server.ResolveDependency<IEntityManager>();
        var mapLoader = entities.System<MapLoaderSystem>();
        var bodySystem = entities.System<SLBodySystem>();
        var containerSystem = entities.System<SharedContainerSystem>();
        var mapSys = entities.System<SharedMapSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapId);
            mapSys.CreateGridEntity(mapId);
            var human = entities.SpawnEntity("HumanBodyDummy", new MapCoordinates(0, 0, mapId));
            var partCount = bodySystem.GetBodyParts((human, entities.GetComponent<SLBodyComponent>(human))).Count;
            Assert.That(partCount, Is.GreaterThan(1));
            AssertBody(human);

            var mapPath = new ResPath($"/{nameof(SaveLoadReparentTest)}{nameof(Test)}map.yml");
            Assert.That(mapLoader.TrySaveMap(mapId, mapPath));
            mapSys.DeleteMap(mapId);
            Assert.That(mapLoader.TryLoadMap(mapPath, out var map, out _), Is.True);

            var loaded = entities.AllEntities<SLBodyComponent>()
                .Where(e => entities.GetComponent<MetaDataComponent>(e.Owner).EntityPrototype?.ID == "HumanBodyDummy")
                .ToArray();
            Assert.That(loaded, Has.Length.EqualTo(1));
            Assert.That(bodySystem.GetBodyParts(loaded[0]), Has.Count.EqualTo(partCount));
            AssertBody(loaded[0].Owner);

            entities.DeleteEntity(map!.Value);
        });

        void AssertBody(EntityUid human)
        {
            var body = new Entity<SLBodyComponent>(human, entities.GetComponent<SLBodyComponent>(human));
            foreach (var part in bodySystem.GetBodyParts(body))
            {
                Assert.Multiple(() =>
                {
                    Assert.That(part.Comp.Body, Is.EqualTo(human));
                    if (part.Owner == body.Comp.RootPartEntity)
                    {
                        Assert.That(part.Comp.Parent, Is.Null);
                        return;
                    }

                    Assert.That(part.Comp.Parent, Is.Not.Null);
                    Assert.That(part.Comp.ParentSocket, Is.Not.Null);
                    Assert.That(containerSystem.TryGetContainingContainer((part.Owner, null, null), out var container));
                    Assert.That(container!.Owner, Is.EqualTo(part.Comp.Parent));
                    Assert.That(container.ID, Is.EqualTo(SLBodySystem.BodyContainerId));
                    if (entities.TryGetComponent<OrganComponent>(part, out var organ))
                        Assert.That(organ.Body, Is.EqualTo(human));
                });
            }
        }
    }
}
