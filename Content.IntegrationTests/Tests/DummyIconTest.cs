#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Prototypes;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests
{
    [TestFixture]
    public sealed class DummyIconTest : GameTest
    {
        [Test]
        public async Task Test()
        {
            var pair = Pair;
            var client = pair.Client;
            var prototypeManager = client.ResolveDependency<IPrototypeManager>();
            var spriteSys = client.System<SpriteSystem>();
            var componentFactory = client.ResolveDependency<IComponentFactory>();
            var entMan = client.ResolveDependency<IEntityManager>();

            await client.WaitAssertion(() =>
            {
                // Starlight - start
                Assert.Multiple(() =>
                {
                    foreach (var proto in prototypeManager.EnumeratePrototypes<EntityPrototype>())
                    {
                        if (proto.HideSpawnMenu || proto.Abstract || pair.IsTestPrototype(proto) ||
                            !proto.Components.ContainsKey("Sprite"))
                            continue;

                        Assert.DoesNotThrow(() =>
                        {
                            var _ = spriteSys.GetPrototypeTextures(proto).ToList();
                        }, "Prototype {0} threw an exception when getting its textures.", proto.ID);
                    }

                    var handStateNames = new[] { "left", "right" };
                    var equipStateNames = new[] { "back", "suitstorage" };
                    var fillLevelPrototypes = prototypeManager.EnumeratePrototypes<EntityPrototype>()
                        .Where(p => !p.Abstract)
                        .Where(p => !pair.IsTestPrototype(p))
                        .Where(p => p.TryGetComponent<SolutionContainerVisualsComponent>(out _, componentFactory))
                        .OrderBy(p => p.ID)
                        .ToList();

                    foreach (var proto in fillLevelPrototypes)
                    {
                        Assert.That(proto.TryGetComponent<SolutionContainerVisualsComponent>(out var visuals, componentFactory));
                        Assert.That(proto.TryGetComponent<SpriteComponent>(out var sprite, componentFactory));
                        if (!proto.HasComponent<AppearanceComponent>(componentFactory))
                            Assert.Fail($"{proto.ID} has SolutionContainerVisualsComponent but no AppearanceComponent.");

                        if (!string.IsNullOrEmpty(visuals.FillBaseName) && visuals.MaxFillLevels > 0)
                        {
                            var entity = entMan.Spawn(proto.ID);
                            if (!spriteSys.LayerMapTryGet(entity, SolutionContainerLayers.Fill, out var fillLayerId, false))
                                Assert.Fail($"{proto.ID} has SolutionContainerVisualsComponent but no fill layer map.");
                            if (!spriteSys.TryGetLayer(entity, fillLayerId, out var fillLayer, false))
                                Assert.Fail($"{proto.ID} somehow lost a layer.");

                            var rsi = fillLayer.ActualRsi;
                            for (var i = 1; i <= visuals.MaxFillLevels; i++)
                            {
                                var state = $"{visuals.FillBaseName}{i}";
                                Assert.That(rsi.TryGetState(state, out _),
                                    $"{proto.ID} has SolutionContainerVisualsComponent with MaxFillLevels = {visuals.MaxFillLevels}, but {rsi.Path} doesn't have state {state}!");
                            }
                        }

                        if (!string.IsNullOrEmpty(visuals.InHandsFillBaseName) && visuals.InHandsMaxFillLevels > 0)
                        {
                            var rsi = sprite.BaseRSI;
                            for (var i = 1; i <= visuals.InHandsMaxFillLevels; i++)
                            {
                                foreach (var handName in handStateNames)
                                {
                                    var state = $"inhand-{handName}{visuals.InHandsFillBaseName}{i}";
                                    Assert.That(rsi.TryGetState(state, out _),
                                        $"{proto.ID} has SolutionContainerVisualsComponent with InHandsMaxFillLevels = {visuals.InHandsMaxFillLevels}, but {rsi.Path} doesn't have state {state}!");
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(visuals.EquippedFillBaseName) && visuals.EquippedMaxFillLevels > 0)
                        {
                            var rsi = sprite.BaseRSI;
                            for (var i = 1; i <= visuals.EquippedMaxFillLevels; i++)
                            {
                                foreach (var equipName in equipStateNames)
                                {
                                    var state = $"equipped-{equipName}{visuals.EquippedFillBaseName}{i}";
                                    Assert.That(rsi.TryGetState(state, out _),
                                        $"{proto.ID} has SolutionContainerVisualsComponent with EquippedMaxFillLevels = {visuals.EquippedMaxFillLevels}, but {rsi.Path} doesn't have state {state}!");
                                }
                            }
                        }
                    }
                });
                // Starlight - end
            });
        }
    }
}
