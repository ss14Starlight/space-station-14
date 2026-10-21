using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Mind;
using Content.Server.Objectives.Components;
using Content.Server.Roles;
using Content.Server._Starlight.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Filters;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared._Starlight.Objectives.Targeting;
using Content.Shared._Starlight.Traits.Antags;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Objectives;

public sealed class AntagTargetTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task ObjectivePoolsIncludeEligibleAnimalsButExcludeDeadUnoccupiedAndUnmarkedCritters()
        => await Server.WaitAssertion(() =>
        {
            var minds = Server.System<MindSystem>();
            Entity<MindComponent> Character(string prototype)
            {
                var mind = minds.CreateMind(null);
                minds.TransferTo(mind, SEntMan.SpawnEntity(prototype, MapCoordinates.Nullspace));
                return mind;
            }

            var eligible = new[]
            {
                "MobMonkeyPunpun", "MobKoboldKiki", "MobMonkeyStirStir", "MobCorgiSmart", "MobK9",
                "MobCorgiSmartNoGalcom", // The marker is inherited by descendants.
            }.Select(Character).ToArray();
            foreach (var character in eligible)
                Assert.That(SEntMan.HasComponent<AntagTargetComponent>(character.Comp.OwnedEntity), Is.True);

            var human = Character("MobHuman");
            var mouse = Character("MobMouse");
            var mothroach = Character("MobMothroach");
            SEntMan.SpawnEntity("MobCorgiSmart", MapCoordinates.Nullspace); // No mind: not a player target.

            foreach (IMindPool source in new IMindPool[] { new AliveHumansPool(), new WeightedAlivePool() })
            {
                var pool = new HashSet<Entity<MindComponent>>();
                source.FindMinds(pool, null, SEntMan, minds);
                Assert.That(pool.Select(mind => mind.Owner),
                    Is.EquivalentTo(eligible.Select(mind => mind.Owner).Append(human.Owner)));
                Assert.That(pool, Does.Not.Contain(mouse).And.Not.Contain(mothroach));

                pool.Clear();
                source.FindMinds(pool, eligible[0], SEntMan, minds);
                Assert.That(pool, Does.Not.Contain(eligible[0]), "The chooser is still excluded.");
            }

            SEntMan.AddComponent<MarkedForDeathComponent>(eligible[0].Comp.OwnedEntity!.Value);
            var highValue = new HashSet<Entity<MindComponent>>();
            new HighValueTargetsPool().FindMinds(highValue, null, SEntMan, minds);
            Assert.That(highValue.Select(mind => mind.Owner), Is.EqualTo(new[] { eligible[0].Owner }),
                "Animal eligibility does not itself make a character high value.");

            Server.System<MobStateSystem>().ChangeMobState(eligible[0].Comp.OwnedEntity!.Value, MobState.Dead);
            highValue.Clear();
            new HighValueTargetsPool().FindMinds(highValue, null, SEntMan, minds);
            Assert.That(highValue, Is.Empty);
            var living = new HashSet<Entity<MindComponent>>();
            new WeightedAlivePool().FindMinds(living, null, SEntMan, minds);
            Assert.That(living, Does.Not.Contain(eligible[0]), "Dead animals cannot become new targets.");
        });

    [Test]
    public async Task KillHelpAndLessonFiltersAllowEligibleAnimalsAndStillHonorTargetImmunity()
        => await Server.WaitAssertion(() =>
        {
            var minds = Server.System<MindSystem>();
            Entity<MindComponent> Traitor(string prototype)
            {
                var mind = minds.CreateMind(null);
                minds.TransferTo(mind, SEntMan.SpawnEntity(prototype, MapCoordinates.Nullspace));
                Server.System<RoleSystem>().MindAddRole(mind, "MindRoleTraitor", silent: true);
                return mind;
            }

            var chooser = Traitor("MobHuman");
            var target = Traitor("MobCorgiSmart");
            SEntMan.AddComponent<MarkedForDeathComponent>(target.Comp.OwnedEntity!.Value);
            var goals = new[]
            {
                "KillRandomPersonObjective", "KillRandomHeadObjective", "TeachRandomPersonObjective",
                "TeachRandomHeadObjective", "RandomTraitorAliveObjective", "RandomTraitorProgressObjective",
            }.Select(prototype => SEntMan.SpawnEntity(prototype, MapCoordinates.Nullspace)).ToArray();
            foreach (var objective in goals)
            {
                var pick = SEntMan.GetComponent<PickRandomPersonComponent>(objective);
                Assert.That(minds.PickFromPool(pick.Pool, pick.Filters, chooser)?.Owner, Is.EqualTo(target.Owner));
            }

            SEntMan.AddComponent<NoObjectiveTargetComponent>(target.Comp.OwnedEntity!.Value);
            foreach (var objective in goals)
            {
                var pick = SEntMan.GetComponent<PickRandomPersonComponent>(objective);
                Assert.That(minds.PickFromPool(pick.Pool, pick.Filters, chooser), Is.Null,
                    "Explicit eligibility must not bypass NoObjectiveTarget or the objective's filters.");
            }
        });
}
