using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Whitelist;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Utility
{
    [TestFixture]
    [TestOf(typeof(EntityWhitelist))]
    public sealed class EntityWhitelistTest : GameTest
    {
        private const string InvalidComponent = "Sprite";
        private const string ValidComponent = "Physics";

        [TestPrototypes] // Starlight, I didn't want to have to modify this entire section, but editor config will yell at me if I don't.
        private const string Prototypes = $"""
        -   type: Tag
            id: WhitelistTestValidTag
        -   type: Tag
            id: WhitelistTestInvalidTag

        -   type: entity
            id: WhitelistDummy
            components:
            -   type: ItemSlots
                slots:
                    slotName:
                        whitelist:
                            prototypes:
                                - ValidPrototypeDummy
                            components:
                                - {ValidComponent}
                            tags:
                                - WhitelistTestValidTag
                            toolQuality:
                                - Slicing

        -   type: entity
            id: InvalidComponentDummy
            components:
            -   type: {InvalidComponent}

        -   type: entity
            id: WhitelistTestInvalidTagDummy
            components:
            -   type: Tag
                tags:
                    - WhitelistTestInvalidTag

        -   type: entity
            id: ValidComponentDummy
            components:
            -   type: {ValidComponent}

        -   type: entity
            id: WhitelistTestValidTagDummy
            components:
            -   type: Tag
                tags:
                    - WhitelistTestValidTag

        -   type: entity
            id: WhitelistTestInvalidToolQualityDummy
            components:
            -   type: Tool
                qualities:
                    - Anchoring

        -   type: entity
            id: WhitelistTestValidToolQualityDummy
            components:
            -   type: Tool
                qualities:
                    - Slicing

        -   type: entity
            id: WhitelistTestAllToolQualitiesDummy
            components:
            -   type: Tool
                qualities:
                    - Cutting
                    - Slicing
        """;

        [Test]
        public async Task Test()
        {
            var pair = Pair;
            var server = pair.Server;

            var testMap = await pair.CreateTestMap();
            var mapCoordinates = testMap.MapCoords;

            var sEntities = server.EntMan;
            var sys = server.System<EntityWhitelistSystem>();

            await server.WaitAssertion(() =>
            {
                var validComponent = sEntities.SpawnEntity("ValidComponentDummy", mapCoordinates);
                var WhitelistTestValidTag = sEntities.SpawnEntity("WhitelistTestValidTagDummy", mapCoordinates);
                var validToolQuality = sEntities.SpawnEntity("WhitelistTestValidToolQualityDummy", mapCoordinates); // Starlight
                var allToolQualities = sEntities.SpawnEntity("WhitelistTestAllToolQualitiesDummy", mapCoordinates); // Starlight

                var invalidComponent = sEntities.SpawnEntity("InvalidComponentDummy", mapCoordinates);
                var WhitelistTestInvalidTag = sEntities.SpawnEntity("WhitelistTestInvalidTagDummy", mapCoordinates);
                var invalidToolQuality = sEntities.SpawnEntity("WhitelistTestInvalidToolQualityDummy", mapCoordinates); // Starlight

                // Test instantiated on its own
                var whitelistInst = new EntityWhitelist
                {
                    Components = new[] { $"{ValidComponent}" },
                    Tags = new() { "WhitelistTestValidTag" },
                    ToolQualities = new() { "Slicing" } // Starlight
                };

                Assert.Multiple(() =>
                {
                    Assert.That(sys.IsValid(whitelistInst, validComponent), Is.True);
                    Assert.That(sys.IsValid(whitelistInst, WhitelistTestValidTag), Is.True);
                    Assert.That(sys.IsValid(whitelistInst, validToolQuality), Is.True); // Starlight

                    Assert.That(sys.IsValid(whitelistInst, invalidComponent), Is.False);
                    Assert.That(sys.IsValid(whitelistInst, WhitelistTestInvalidTag), Is.False);
                    #region Starlight
                    Assert.That(sys.IsValid(whitelistInst, invalidToolQuality), Is.False);
                });

                var requireAllToolQualities = new EntityWhitelist
                {
                    RequireAll = true,
                    ToolQualities = new() { "Cutting", "Slicing" }
                };

                Assert.Multiple(() =>
                {
                    Assert.That(sys.IsValid(requireAllToolQualities, allToolQualities), Is.True);
                    Assert.That(sys.IsValid(requireAllToolQualities, validToolQuality), Is.False);
                    #endregion
                });

                // Test from serialized
                var dummy = sEntities.SpawnEntity("WhitelistDummy", mapCoordinates);
                var whitelistSer = sEntities.GetComponent<ItemSlotsComponent>(dummy).Slots.Values.First().Whitelist;
                Assert.That(whitelistSer, Is.Not.Null);

                Assert.Multiple(() =>
                {
                    Assert.That(whitelistSer.Components, Is.Not.Null);
                    Assert.That(whitelistSer.Tags, Is.Not.Null);
                    Assert.That(whitelistSer.ToolQualities, Is.Not.Null); // Starlight
                });

                Assert.Multiple(() =>
                {
                    Assert.That(sys.IsValid(whitelistSer, validComponent), Is.True);
                    Assert.That(sys.IsValid(whitelistSer, WhitelistTestValidTag), Is.True);
                    Assert.That(sys.IsValid(whitelistSer, validToolQuality), Is.True); // Starlight

                    Assert.That(sys.IsValid(whitelistSer, invalidComponent), Is.False);
                    Assert.That(sys.IsValid(whitelistSer, WhitelistTestInvalidTag), Is.False);
                    Assert.That(sys.IsValid(whitelistSer, invalidToolQuality), Is.False); // Starlight
                });
            });
        }
    }
}
