using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Objectives.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Content.Shared.Mind;
using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Antags.Vampires.Components.Classes;
using Content.Server._Starlight.GameTicking.Rules;
using Content.Server._Starlight.GameTicking.Rules.Components;

namespace Content.IntegrationTests.Tests._Starlight.Antags;
[TestFixture]
public sealed class VampireRuleTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true,
    };

    private const string VampireGameRuleProtoId = "Vampire";
    private const string VampireAntagRoleName = "Vampire";

    [Test]
    public async Task TestVampireRuleAssignsAntagAndObjectives()
    {
        var pair = Pair;

        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var compFact = server.ResolveDependency<IComponentFactory>();
        var ticker = server.System<GameTicker>();
        var mindSys = server.System<MindSystem>();
        var roleSys = server.System<RoleSystem>();
        var ruleSys = server.System<VampireRuleSystem>();

        // Look up the minimum player count and max total objective difficulty for the game rule
        var minPlayers = 1;
        var maxDifficulty = 0f;
        await server.WaitAssertion(() =>
        {
            Assert.That(protoMan.TryIndex<EntityPrototype>(VampireGameRuleProtoId, out var gameRuleEnt),
            $"Failed to lookup vampire game rule entity prototype with ID \"{VampireGameRuleProtoId}\"!");

            Assert.That(gameRuleEnt.TryGetComponent<GameRuleComponent>(out var gameRule, compFact),
            $"Game rule entity {VampireGameRuleProtoId} does not have a GameRuleComponent!");

            Assert.That(gameRuleEnt.TryGetComponent<AntagRandomObjectivesComponent>(out var randomObjectives, compFact),
            $"Game rule entity {VampireGameRuleProtoId} does not have an AntagRandomObjectivesComponent!");

            minPlayers = gameRule.MinPlayers;
            maxDifficulty = randomObjectives.MaxDifficulty;
        });

        // Initially in the lobby
        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
        Assert.That(client.AttachedEntity, Is.Null);
        Assert.That(ticker.PlayerGameStatuses[client.User!.Value], Is.EqualTo(PlayerGameStatus.NotReadyToPlay));

        // Add enough dummy players for the game rule
        var dummies = await pair.Server.AddDummySessions(minPlayers);
        await pair.RunTicksSync(5);

        // Initially, the players have no attached entities
        Assert.That(pair.Player?.AttachedEntity, Is.Null);
        Assert.That(dummies.All(x => x.AttachedEntity == null));

        // Opt-in the player for the vampire role
        await pair.SetAntagPreferences([VampireAntagRoleName]);

        // Add the game rule
        VampireRuleComponent ruleComp = null;
        await server.WaitPost(() =>
        {
            var gameRuleEnt = ticker.AddGameRule(VampireGameRuleProtoId);
            Assert.That(entMan.TryGetComponent(gameRuleEnt, out ruleComp));

            // Ready up
            ticker.ToggleReadyAll(true);
            Assert.That(ticker.PlayerGameStatuses.Values.All(x => x == PlayerGameStatus.ReadyToPlay));

            // Start the round
            ticker.StartRound();
            // Force vampire mode to start (skip the delay)
            ticker.StartGameRule(gameRuleEnt);
        });
        await pair.RunTicksSync(10);

        // Game should have started
        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
        Assert.That(ticker.PlayerGameStatuses.Values.All(x => x == PlayerGameStatus.JoinedGame));
        Assert.That(client.EntMan.EntityExists(client.AttachedEntity));

        // Check the player and dummies are spawned
        var dummyEnts = dummies.Select(x => x.AttachedEntity ?? default).ToArray();
        var player = pair.Player!.AttachedEntity!.Value;
        Assert.That(entMan.EntityExists(player));
        Assert.That(dummyEnts.All(entMan.EntityExists));

        // Make sure the player is a vampire.
        Assert.That(mindSys.GetMind(player), Is.Not.Null, "Player should have a mind");
        var mind = mindSys.GetMind(player)!.Value;
        Assert.That(roleSys.MindIsAntagonist(mind), "Player mind was not marked as antagonist.");
        Assert.That(entMan.HasComponent<VampireComponent>(player), "Player entity did not get VampireComponent.");

        // Check that the player has no other vampire class components and has the correct initial blood amount
        var vampComp = entMan.GetComponent<VampireComponent>(player);
        Assert.That(!entMan.HasComponent<HemomancerComponent>(player)
                    && !entMan.HasComponent<UmbraeComponent>(player)
                    && !entMan.HasComponent<DantalionComponent>(player)
                    && !entMan.HasComponent<GargantuaComponent>(player),
            "Vampire should start without a chosen class");
        Assert.That(vampComp.TotalBlood, Is.EqualTo(0),
            "Vampire should start with 0 blood");

        // Check that the vampire rule system has correctly registered the vampire mind
        Assert.That(ruleComp.VampireMinds.Count, Is.EqualTo(1),
            "Expected exactly 1 vampire to be selected when only 1 player opts in");
        Assert.That(ruleComp.VampireMinds.Contains(mind),
            "The player who opted in should be selected as vampire");

        // Check total objective difficulty
        Assert.That(entMan.TryGetComponent<MindComponent>(mind, out var mindComp));
        var totalDifficulty = mindComp.Objectives.Sum(o => entMan.GetComponent<ObjectiveComponent>(o).Difficulty);
        Assert.That(totalDifficulty, Is.AtMost(maxDifficulty),
            $"MaxDifficulty exceeded! Objectives: {string.Join(", ", mindComp.Objectives.Select(o => FormatObjective(o, entMan)))}");
        Assert.That(mindComp.Objectives, Is.Not.Empty,
            $"No objectives assigned!");
    }

    private static string FormatObjective(Entity<ObjectiveComponent> entity, IEntityManager entMan)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(entity);
        var objective = entMan.GetComponent<ObjectiveComponent>(entity);
        return $"{meta.EntityName} ({objective.Difficulty})";
    }
}
