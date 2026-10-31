using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Storage.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests
{
    public sealed class ContainerOcclusionTest : GameTest
    {
        [TestPrototypes]
        private const string Prototypes = @"
- type: entity
  id: ContainerOcclusionA
  components:
  - type: EntityStorage
    occludesLight: true

- type: entity
  id: ContainerOcclusionB
  components:
  - type: EntityStorage
    showContents: true
    occludesLight: false

- type: entity
  id: ContainerOcclusionDummy
  components:
  - type: Sprite
  - type: PointLight
";

        #region Starlight
        [Test]
        public async Task TestContainerOcclusionPropagation()
        {
            var pair = Pair;
            var server = pair.Server;
            var client = pair.Client;

            var clientEntManager = client.ResolveDependency<IEntityManager>();
            var serverEntManager = server.ResolveDependency<IEntityManager>();

            EntityUid occludedDummy = default;
            EntityUid visibleDummy = default;
            EntityUid nestedDummy = default;
            var map = await pair.CreateTestMap();

            await server.WaitPost(() =>
            {
                var occludingPos = new MapCoordinates(Vector2.Zero, map.MapId);
                var transparentPos = new MapCoordinates(new Vector2(1, 0), map.MapId);
                var nestedPos = new MapCoordinates(new Vector2(2, 0), map.MapId);
                var entStorage = serverEntManager.EntitySysManager.GetEntitySystem<EntityStorageSystem>();
                var occludingContainer = serverEntManager.SpawnEntity("ContainerOcclusionA", occludingPos);
                occludedDummy = serverEntManager.SpawnEntity("ContainerOcclusionDummy", occludingPos);
                entStorage.Insert(occludedDummy, occludingContainer);

                var transparentContainer = serverEntManager.SpawnEntity("ContainerOcclusionB", transparentPos);
                visibleDummy = serverEntManager.SpawnEntity("ContainerOcclusionDummy", transparentPos);
                entStorage.Insert(visibleDummy, transparentContainer);

                var outerContainer = serverEntManager.SpawnEntity("ContainerOcclusionA", nestedPos);
                var innerContainer = serverEntManager.SpawnEntity("ContainerOcclusionB", nestedPos);
                nestedDummy = serverEntManager.SpawnEntity("ContainerOcclusionDummy", nestedPos);
                entStorage.Insert(innerContainer, outerContainer);
                entStorage.Insert(nestedDummy, innerContainer);
            });

            await pair.RunTicksSync(5);

            var occludedClient = clientEntManager.GetEntity(serverEntManager.GetNetEntity(occludedDummy));
            var visibleClient = clientEntManager.GetEntity(serverEntManager.GetNetEntity(visibleDummy));
            var nestedClient = clientEntManager.GetEntity(serverEntManager.GetNetEntity(nestedDummy));

            await client.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(clientEntManager.GetComponent<SpriteComponent>(occludedClient).ContainerOccluded);
                    Assert.That(clientEntManager.GetComponent<PointLightComponent>(occludedClient).ContainerOccluded);
                    Assert.That(clientEntManager.GetComponent<SpriteComponent>(visibleClient).ContainerOccluded, Is.False);
                    Assert.That(clientEntManager.GetComponent<PointLightComponent>(visibleClient).ContainerOccluded, Is.False);
                    Assert.That(clientEntManager.GetComponent<SpriteComponent>(nestedClient).ContainerOccluded);
                    Assert.That(clientEntManager.GetComponent<PointLightComponent>(nestedClient).ContainerOccluded);
                });
            });
        }
        #endregion
    }
}
