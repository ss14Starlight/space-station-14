using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Mind;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server.Roles;
using Content.Server._Starlight.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Filters;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared._Starlight.Objectives.Targeting;
using Content.Shared._Starlight.Traits.Antags;
using Content.Shared.Objectives.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Objectives;

public sealed class AntagTargetTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private static readonly string[] _sourceArray = new[]
    {
        "MobMonkeyPunpun", "MobKoboldKiki", "MobMonkeyStirStir", "MobCorgiSmart", "MobK9",
        "MobCorgiSmartNoGalcom", // The marker is inherited by descendants.
    };
    private static readonly string[] _sourceArray2 = new[]
    {
        "KillRandomPersonObjective", "KillRandomHeadObjective", "TeachRandomPersonObjective",
        "TeachRandomHeadObjective", "RandomTraitorAliveObjective", "RandomTraitorProgressObjective",
    };
    private static readonly string[] _assertion = new[]
    {
        "KillRandomPersonObjective", "KillRandomHeadObjective", "TeachRandomPersonObjective",
        "TeachRandomHeadObjective", "RandomTraitorAliveObjective", "RandomTraitorProgressObjective",
        "ChangelingKillRandomPersonObjective", "ChangelingTeachRandomPersonObjective",
        "VampireKillRandomPersonObjective",
    };

    [Test]
    public async Task ObjectivePoolsIncludeEligibleAnimalsButExcludeDeadUnoccupiedAndUnmarkedCritters()
        => await Server.WaitAssertion((Action)(() =>
        {
            var minds = Server.System<MindSystem>();
            var targetSys = Server.System<TargetSystem>();
            Entity<MindComponent> Character(string prototype)
            {
                var mind = minds.CreateMind(null);
                minds.TransferTo(mind, SEntMan.Spawn(prototype, MapCoordinates.Nullspace));
                return mind;
            }

            var eligible = Enumerable.Select(_sourceArray, Character).ToArray();
            foreach (var character in eligible)
                Assert.That(SEntMan.HasComponent<AntagTargetComponent>(character.Comp.OwnedEntity), Is.True);

            var human = Character("MobHuman");
            var mouse = Character("MobMouse");
            var mothroach = Character("MobMothroach");
            SEntMan.Spawn("MobCorgiSmart", MapCoordinates.Nullspace); // No mind: not a player target.

            foreach (var source in new MindPool[] { new AliveHumansPool(), new WeightedAlivePool() })
            {
                var pool = new HashSet<Entity<MindComponent>>();
                source.FindMinds(pool, null, SEntMan, targetSys);
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(pool.Select(mind => mind.Owner),
                                    Is.EquivalentTo(eligible.Select(mind => mind.Owner).Append(human.Owner)));
                    Assert.That(pool, Does.Not.Contain(mouse).And.Not.Contain(mothroach));
                }

                pool.Clear();
                source.FindMinds(pool, eligible[0], SEntMan, targetSys);
                Assert.That(pool, Does.Not.Contain(eligible[0]), "The chooser is still excluded.");
            }

            SEntMan.AddComponent<MarkedForDeathComponent>(eligible[0].Comp.OwnedEntity!.Value);
            var highValue = new HashSet<Entity<MindComponent>>();
            new HighValueTargetsPool().FindMinds(highValue, null, SEntMan, targetSys);
            Assert.That(highValue.Select(mind => mind.Owner), Is.EqualTo(new[] { eligible[0].Owner }),
                "Animal eligibility does not itself make a character high value.");

            Server.System<MobStateSystem>().ChangeMobState(eligible[0].Comp.OwnedEntity!.Value, MobState.Dead);
            highValue.Clear();
            new HighValueTargetsPool().FindMinds(highValue, null, SEntMan, targetSys);
            Assert.That(highValue, Is.Empty);
            var living = new HashSet<Entity<MindComponent>>();
            new WeightedAlivePool().FindMinds(living, null, SEntMan, targetSys);
            Assert.That(living, Does.Not.Contain(eligible[0]), "Dead animals cannot become new targets.");
        }));

    [Test]
    public async Task KillHelpAndLessonFiltersAllowEligibleAnimalsAndStillHonorTargetImmunity()
        => await Server.WaitAssertion((Action)(() =>
        {
            var minds = Server.System<MindSystem>();
            var targetSys = Server.System<TargetSystem>();
            Entity<MindComponent> Traitor(string prototype)
            {
                var mind = minds.CreateMind(null);
                minds.TransferTo(mind, SEntMan.Spawn(prototype, MapCoordinates.Nullspace));
                Server.System<RoleSystem>().MindAddRole(mind, "MindRoleTraitor", silent: true);
                return mind;
            }

            var chooser = Traitor("MobHuman");
            var target = Traitor("MobCorgiSmart");
            SEntMan.AddComponent<MarkedForDeathComponent>(target.Comp.OwnedEntity!.Value);
            var goals = Enumerable.Select(_sourceArray2, prototype => SEntMan.Spawn(prototype, MapCoordinates.Nullspace)).ToArray();
            foreach (var objective in goals)
            {
                var pick = SEntMan.GetComponent<PickRandomPersonComponent>(objective);
                Assert.That(targetSys.PickFromPool(pick.Pool, pick.Filters, chooser)?.Owner, Is.EqualTo(target.Owner));
            }

            SEntMan.AddComponent<NoObjectiveTargetComponent>(target.Comp.OwnedEntity!.Value);
            foreach (var objective in goals)
            {
                var pick = SEntMan.GetComponent<PickRandomPersonComponent>(objective);
                Assert.That(targetSys.PickFromPool(pick.Pool, pick.Filters, chooser), Is.Null,
                    "Explicit eligibility must not bypass NoObjectiveTarget or the objective's filters.");
            }
        }));

    [Test]
    public async Task DifferentTargetObjectivesCannotShareTarget()
        => await Server.WaitAssertion(() =>
        {
            var minds = Server.System<MindSystem>();
            var targetSys = Server.System<TargetSystem>();
            var objectiveTargets = Server.System<TargetObjectiveSystem>();
            Entity<MindComponent> Traitor(string prototype)
            {
                var mind = minds.CreateMind(null);
                minds.TransferTo(mind, SEntMan.Spawn(prototype, MapCoordinates.Nullspace));
                Server.System<RoleSystem>().MindAddRole(mind, "MindRoleTraitor", silent: true);
                return mind;
            }

            var chooser = Traitor("MobHuman");
            var target = Traitor("MobHuman");
            SEntMan.AddComponent<MarkedForDeathComponent>(target.Comp.OwnedEntity!.Value);
            var prototypes = _assertion;

            foreach (var assignedProto in prototypes)
            {
                var assigned = SEntMan.Spawn(assignedProto, MapCoordinates.Nullspace);
                objectiveTargets.SetTarget(assigned, target.Owner);
                chooser.Comp.Objectives.Add(assigned);

                foreach (var otherProto in prototypes)
                {
                    var other = SEntMan.Spawn(otherProto, MapCoordinates.Nullspace);
                    var pick = SEntMan.GetComponent<PickRandomPersonComponent>(other);
                    Assert.That(targetSys.PickFromPool(pick.Pool, pick.Filters, chooser), Is.Null,
                        $"{otherProto} must not target someone already targeted by {assignedProto}.");
                    SEntMan.DeleteEntity(other);
                }

                chooser.Comp.Objectives.Remove(assigned);
                SEntMan.DeleteEntity(assigned);
            }
        });
}
