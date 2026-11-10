using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Shared.Objectives.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Objectives;

public sealed partial class ObjectivePickerTest
{
    [TestPrototypes]
    private const string AchievePrototypes = """
        -   type: entity
            id: SLPickerTestConditionProgress
            components:
            -   type: Objective
                difficulty: 1
                unique: false
                issuer: objective-issuer-syndicate
                icon:
                    sprite: error.rsi
                    state: error
            -   type: PickerProgressTest

        -   type: entity
            parent: [BaseTraitorObjective, SLPickerTestConditionProgress]
            id: SLPickerTestTraitorProgress

        -   type: entity
            parent: SLPickerTestConditionProgress
            id: SLPickerTestThiefProgress
            components:
            -   type: Objective
                antag: Thief

        -   type: entity
            parent: BaseThiefObjective
            id: SLPickerTestThiefAchieve
            components:
            -   type: Objective
                antag: Thief
                difficulty: 0
                icon:
                    sprite: error.rsi
                    state: error
            -   type: AchieveObjectivesCondition

        -   type: entity
            parent: TraitorAchieveObjective
            id: SLPickerTestUnmarkedAchieve
            components:
            -   type: Objective
                antag: null
        """;

    [TestCase("TraitorAchieveObjective", "SLPickerTestTraitorProgress", "SLPickerTestThiefProgress")]
    [TestCase("SLPickerTestThiefAchieve", "SLPickerTestThiefProgress", "SLPickerTestTraitorProgress")]
    public async Task AchieveOnlyCountsItsAntag(string aggregatePrototype, string matchingPrototype, string otherPrototype)
        => await Server.WaitAssertion(() =>
        {
            var minds = Server.System<MindSystem>();
            var mind = minds.CreateMind(null);
            var objectives = Server.System<ObjectivesSystem>();

            EntityUid Assign(string prototype)
            {
                var objective = SEntMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
                minds.AddObjective(mind, mind.Comp, objective);
                return objective;
            }

            var aggregate = Assign(aggregatePrototype);
            // Multiple achieve goals shouldn't interact with each other.
            Assign(aggregatePrototype);
            var first = Assign(matchingPrototype);
            var second = Assign(matchingPrototype);
            var firstProgress = SEntMan.GetComponent<PickerProgressTestComponent>(first);
            var secondProgress = SEntMan.GetComponent<PickerProgressTestComponent>(second);
            firstProgress.Progress = 1;
            secondProgress.Progress = 0.4f;

            var other = Assign(otherPrototype);
            var unmarked = Assign("SLPickerTestConditionProgress");
            var pollen = Assign("PollenCollectionObjective");
            var story = Assign("RetrieveContrabandObjective");
            foreach (var objective in new[] { unmarked, pollen, story })
            {
                Assert.That(SEntMan.GetComponent<ObjectiveComponent>(objective).Antag, Is.Null,
                    "Species and story goals must not inherit a traitor flag.");
                SEntMan.EnsureComponent<PickerProgressTestComponent>(objective).Progress = 0;
            }

            Assert.That(objectives.GetProgress(aggregate, mind), Is.EqualTo(0.4f),
                "An incomplete matching goal must still limit progress.");
            Assert.That(firstProgress.Queries, Is.EqualTo(1));
            Assert.That(secondProgress.Queries, Is.EqualTo(1));

            secondProgress.Progress = 1;
            Assert.That(objectives.GetProgress(aggregate, mind), Is.EqualTo(1),
                "Unfinished goals outside this antagonist must not prevent completion.");
            foreach (var objective in new[] { other, unmarked, pollen, story })
                Assert.That(SEntMan.GetComponent<PickerProgressTestComponent>(objective).Queries, Is.Zero,
                    "Unrelated objective progress must not be queried.");
        });

    [Test]
    public async Task AchieveMatchesGoals()
        => await Server.WaitAssertion(() =>
        {
            var minds = Server.System<MindSystem>();
            var mind = minds.CreateMind(null);
            var objectives = Server.System<ObjectivesSystem>();
            var aggregate = SEntMan.SpawnEntity("TraitorAchieveObjective", MapCoordinates.Nullspace);
            minds.AddObjective(mind, mind.Comp, aggregate);
            var other = SEntMan.SpawnEntity("SLPickerTestThiefProgress", MapCoordinates.Nullspace);
            SEntMan.GetComponent<PickerProgressTestComponent>(other).Progress = 1;
            minds.AddObjective(mind, mind.Comp, other);
            Assert.That(objectives.GetProgress(aggregate, mind), Is.Zero,
                "Having no matching goals must not give free completion.");

            var unmarked = SEntMan.SpawnEntity("SLPickerTestConditionProgress", MapCoordinates.Nullspace);
            SEntMan.GetComponent<PickerProgressTestComponent>(unmarked).Progress = 1;
            minds.AddObjective(mind, mind.Comp, unmarked);
            var unmarkedAggregate = SEntMan.SpawnEntity("SLPickerTestUnmarkedAchieve", MapCoordinates.Nullspace);
            minds.AddObjective(mind, mind.Comp, unmarkedAggregate);
            Assert.That(objectives.GetProgress(unmarkedAggregate, mind), Is.Zero,
                "An aggregate without a flag must not match other unmarked goals.");
        });
}
