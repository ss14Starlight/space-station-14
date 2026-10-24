using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Objectives.ObjectivePicker;
using Content.Server.Mind;
using Content.Server._Starlight.Objectives.Components;
using Content.Server.Objectives;
using Content.Shared.Access.Components;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared._Starlight.Objectives.ObjectivePicker;
using Content.Shared.Objectives.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.Tests._Starlight.Objectives;

public sealed partial class ObjectivePickerTest : GameTest
{
    private readonly EntProtoId _limited = "SLPickerTestLimited";
    private readonly EntProtoId _plain = "SLPickerTestPlain";
    private readonly EntProtoId _blacklist = "SLPickerTestBlacklist";
    private readonly EntProtoId _survive = "SLPickerTestSurvive";
    private readonly EntProtoId _free = "SLPickerTestFree";
    private static readonly ProtoId<DepartmentPrototype> _department = "Cargo";

    [TestPrototypes]
    private const string Prototypes = """
        -   type: entity
            id: SLPickerTestPlain
            components:
            -   type: Objective
                difficulty: 3
                unique: false
                issuer: objective-issuer-syndicate
                icon:
                    sprite: error.rsi
                    state: error
            -   type: FreeObjective

        -   type: entity
            parent: SLPickerTestPlain
            id: SLPickerTestLimited
            components:
            -   type: ObjectiveLimit
                limit: 1

        -   type: entity
            parent: SLPickerTestPlain
            id: SLPickerTestBlacklist
            components:
            -   type: ObjectiveBlacklistRequirement
                blacklist:
                    components: [SurviveCondition]

        -   type: entity
            parent: SLPickerTestPlain
            id: SLPickerTestSurvive
            components:
            -   type: SurviveCondition

        -   type: entity
            parent: SLPickerTestPlain
            id: SLPickerTestFree
            components:
            -   type: Objective
                difficulty: 0
        """;

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private EntityUid Picker(params EntProtoId[] prototypes)
    {
        var mind = Server.System<MindSystem>().CreateMind(null);
        var offers = SEntMan.AddComponent<PotentialObjectivesComponent>(mind);
        offers.MinimumDifficulty = 3;
        SEntMan.AddComponent<ObjectivePickerConfigurationComponent>(mind);
        foreach (var prototype in prototypes)
        {
            var objective = SEntMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
            var id = SEntMan.GetNetEntity(objective);
            offers.ObjectiveOptions[id] = default;
            offers.Difficulties[id] = SEntMan.GetComponent<ObjectiveComponent>(objective).Difficulty;
        }
        return mind;
    }

    [Test]
    public async Task FirstConfirmedSelectionExhaustsLimitForOtherPickers() =>
        await Server.WaitAssertion(() =>
        {
            var first = Picker(_limited, _plain);
            var second = Picker(_limited, _plain);
            var firstOffers = SEntMan.GetComponent<PotentialObjectivesComponent>(first);
            var secondOffers = SEntMan.GetComponent<PotentialObjectivesComponent>(second);
            var firstLimited = firstOffers.ObjectiveOptions.Keys.First();
            var secondLimited = secondOffers.ObjectiveOptions.Keys.First();
            var secondPlain = secondOffers.ObjectiveOptions.Keys.Last();
            var system = Server.System<AntagRandomObjectivesSystem>();

            system.ApplySelectedObjectives(first, [firstLimited]);
            Assert.That(secondOffers.UnavailableObjectives, Does.Contain(secondLimited));
            system.ApplySelectedObjectives(second, [secondLimited]);
            Assert.That(SEntMan.GetComponent<MindComponent>(second).Objectives, Is.Empty);
            system.ApplySelectedObjectives(second, [secondPlain]);
            EntityUid[] expectedObjectives = [SEntMan.GetEntity(secondPlain)];
            Assert.That(SEntMan.GetComponent<MindComponent>(second).Objectives,
                Is.EqualTo(expectedObjectives));

            // A repeat submission before deferred removal must not re-add consumed objectives.
            system.ApplySelectedObjectives(first, [firstLimited]);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<MindComponent>(first).Objectives, Has.Count.EqualTo(1));
                Assert.That(firstOffers.ObjectiveOptions, Is.Empty);
            });
        });

    [Test]
    public async Task BlacklistsAreSymmetricAndAConflictingBatchIsRejected() =>
        await Server.WaitAssertion(() =>
        {
            var mind = Picker(_blacklist, _survive);
            var offers = SEntMan.GetComponent<PotentialObjectivesComponent>(mind);
            var ids = offers.ObjectiveOptions.Keys.ToArray();
            Server.System<AntagRandomObjectivesSystem>().ApplySelectedObjectives(mind, ids);
            Assert.That(SEntMan.GetComponent<MindComponent>(mind).Objectives, Is.Empty);
            Assert.That(offers.Conflicts[ids[0]], Does.Contain(ids[1]));
            Assert.That(offers.Conflicts[ids[1]], Does.Contain(ids[0]));
        });

    [Test]
    public async Task DepartmentIdGoalCountsNestedCrewCardsButNotOwnOrBlankCards()
        => await Server.WaitAssertion(() =>
        {
            var minds = Server.System<MindSystem>();
            var mind = minds.CreateMind(null, "Thief");
            var owner = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            minds.TransferTo(mind, owner);
            var containers = Server.System<SharedContainerSystem>();
            var inventory = containers.EnsureContainer<Container>(owner, "test-inventory");
            var bag = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var contents = containers.EnsureContainer<Container>(bag, "test-contents");
            Assert.That(containers.Insert(bag, inventory), Is.True);

            EntityUid Card(string name)
            {
                var card = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                SEntMan.AddComponent(card, new IdCardComponent { FullName = name, JobDepartments = new() { _department } });
                return card;
            }

            var cards = new[] { Card("Crew A"), Card("Crew B"), Card("Crew C") };
            var goal = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var condition = SEntMan.AddComponent<DepartmentIdStealConditionComponent>(goal);
            condition.MinCount = condition.MaxCount = 3;
            var assigned = new ObjectiveAssignedEvent(mind, mind.Comp);
            SEntMan.EventBus.RaiseLocalEvent(goal, ref assigned);
            Assert.That(assigned.Cancelled, Is.False);
            Assert.That(condition.Department, Is.EqualTo(_department));
            Assert.That(condition.Count, Is.EqualTo(3));

            Assert.That(containers.Insert(Card("Thief"), contents), Is.True);
            Assert.That(containers.Insert(Card(""), contents), Is.True);
            var objectives = Server.System<ObjectivesSystem>();
            Assert.That(objectives.GetProgress(goal, mind), Is.Zero);
            Assert.That(containers.Insert(cards[0], contents), Is.True);
            Assert.That(objectives.GetProgress(goal, mind), Is.EqualTo(1f / 3).Within(0.001f));
            Assert.That(containers.Insert(cards[1], contents), Is.True);
            Assert.That(containers.Insert(cards[2], contents), Is.True);
            Assert.That(objectives.GetProgress(goal, mind), Is.EqualTo(1));
        });
}
