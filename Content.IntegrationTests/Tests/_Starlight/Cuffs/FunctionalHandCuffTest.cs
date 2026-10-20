using Content.IntegrationTests.Fixtures;
using Content.Server.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Starlight.Cuffs;

[TestFixture, TestOf(typeof(CuffableComponent))]
public sealed class FunctionalHandCuffTest : GameTest
{
    // editorconfig-checker-disable
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: FunctionalHandCuffDummy
  components:
  - type: Cuffable
  - type: Hands
    hands:
      hand_right:
        location: Right
      hand_left:
        location: Left
    sortedHands:
    - hand_right
    - hand_left
  - type: ComplexInteraction

- type: entity
  id: FunctionalHandCuffsDummy
  components:
  - type: Handcuff
";
    // editorconfig-checker-enable

    /// <summary>
    /// Deployed cyberlimb items add functional hands; cuffing both real hands must still fully restrain.
    /// </summary>
    [Test]
    public async Task FunctionalHandsDontNeedCuffs()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var cuffSys = entMan.System<CuffableSystem>();
            var handsSys = entMan.System<SharedHandsSystem>();

            var target = entMan.SpawnEntity("FunctionalHandCuffDummy", map.MapCoords);
            var cuffs = entMan.SpawnEntity("FunctionalHandCuffsDummy", map.MapCoords);
            var secondCuffs = entMan.SpawnEntity("FunctionalHandCuffsDummy", map.MapCoords);
            var cuffable = entMan.GetComponent<CuffableComponent>(target);

            handsSys.AddHand(target, "functional_test", HandLocation.Functional);

            Assert.That(cuffSys.TryAddNewCuffs(target, target, cuffs, cuffable), "First cuffs weren't applied");
            Assert.Multiple(() =>
            {
                Assert.That(cuffable.CanStillInteract, Is.False, "Functional hand kept a cuffed target free");
                Assert.That(cuffSys.IsCuffed((target, cuffable)), Is.True);
                Assert.That(cuffSys.TryAddNewCuffs(target, target, secondCuffs, cuffable), Is.False,
                    "Second cuffs were applied to a functional hand");
            });
        });
    }
}
