using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Thaven;
using Content.Shared.Dataset;
using Content.Shared._Starlight.Thaven;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Thaven;

[TestFixture, TestOf(typeof(ThavenMoodPrototype))]
public sealed class ThavenMoodTests : GameTest
{
    [TestPrototypes]
    const string PROTOTYPES = @"
- type: dataset
  id: ThreeValueSet
  values:
    - One
    - Two
    - Three
- type: thavenMood
  id: DuplicateTest
  moodName: DuplicateTest
  moodDesc: DuplicateTest
  allowDuplicateMoodVars: false
  moodVars:
    a: ThreeValueSet
    b: ThreeValueSet
    c: ThreeValueSet
- type: thavenMood
  id: DuplicateOverlapTest
  moodName: DuplicateOverlapTest
  moodDesc: DuplicateOverlapTest
  allowDuplicateMoodVars: false
  moodVars:
    a: ThreeValueSet
    b: ThreeValueSet
    c: ThreeValueSet
    d: ThreeValueSet
    e: ThreeValueSet
";

    const string DATASETPROTO = "ThreeValueSet";

    [Test]
    [Repeat(10)]
    public async Task TestDuplicatePreventionForOverlappingVariableSets()
    {
        var pair = Pair;
        var server = pair.Server;
        await server.WaitIdleAsync();

        var entMan = server.ResolveDependency<IEntityManager>();
        var thavenSystem = entMan.System<ThavenMoodsSystem>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        const string MoodProto = "DuplicateTest";
        const string OverlapMoodProto = "DuplicateOverlapTest";
        var datasetSet = protoMan.Index<DatasetPrototype>(DATASETPROTO).Values.ToHashSet();
        var mood = thavenSystem.RollMood(protoMan.Index<ThavenMoodPrototype>(MoodProto));
        var overlapMood = thavenSystem.RollMood(protoMan.Index<ThavenMoodPrototype>(OverlapMoodProto));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(mood.MoodVars.Values.ToHashSet(), Is.EquivalentTo(datasetSet));
            Assert.That(overlapMood.MoodVars.Values.ToHashSet(), Is.EquivalentTo(datasetSet));
        }
    }
}
