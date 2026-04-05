using System.Linq;
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
public sealed class VampireRuleTest
{
    private const string VampireGameRuleProtoId = "Vampire";
    private const string VampireAntagRoleName = "Vampire";

    [Test]
    public async Task TestVampireRuleAssignsAntagAndObjectives()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings()
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true,
            InLobby = true,
        });

        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var compFact = server.ResolveDependency<IComponentFactory>();
        var ticker = server.System<GameTicker>();
        var mindSys = server.System<MindSystem>();
        var roleSys = server.System<RoleSystem>();
        var ruleSys = server.System<VampireRuleSystem>();

        var minPlayers = 1;
        await server.WaitAssertion(() =>
        {
            Assert.That(protoMan.TryIndex<EntityPrototype>(VampireGameRuleProtoId, out var gameRuleEntProto),
                $"Failed to lookup vampire game rule entity prototype with ID \"{VampireGameRuleProtoId}\"!");

            if (gameRuleEntProto.TryGetComponent<GameRuleComponent>(out var gameRule, compFact))
                minPlayers = Math.Max(2, Math.Min(gameRule.MinPlayers, 8)); // Cap at 8 for testing performance
        });

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
        Assert.That(client.AttachedEntity, Is.Null);
        Assert.That(ticker.PlayerGameStatuses[client.User!.Value], Is.EqualTo(PlayerGameStatus.NotReadyToPlay));

        var dummies = await pair.Server.AddDummySessions(minPlayers);
        await pair.RunTicksSync(5);

        Assert.That(pair.Player?.AttachedEntity, Is.Null);
        Assert.That(dummies.All(x => x.AttachedEntity == null));
        await pair.SetAntagPreferences([VampireAntagRoleName]);

        EntityUid gameRuleEnt = default;
        VampireRuleComponent ruleComp = null;
        await server.WaitPost(() =>
        {
            gameRuleEnt = ticker.AddGameRule(VampireGameRuleProtoId);
            Assert.That(entMan.TryGetComponent(gameRuleEnt, out ruleComp));

            ticker.ToggleReadyAll(true);
            Assert.That(ticker.PlayerGameStatuses.Values.All(x => x == PlayerGameStatus.ReadyToPlay));

            ticker.StartRound();
            ticker.StartGameRule(gameRuleEnt);
        });
        await pair.RunTicksSync(10);

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
        Assert.That(ticker.PlayerGameStatuses.Values.All(x => x == PlayerGameStatus.JoinedGame));
        Assert.That(client.EntMan.EntityExists(client.AttachedEntity));

        Assert.That(pair.Player?.AttachedEntity, Is.Not.Null, "Player should have attached entity");
        var player = pair.Player!.AttachedEntity!.Value;
        Assert.That(entMan.EntityExists(player));

        Assert.That(mindSys.GetMind(player), Is.Not.Null, "Player should have a mind");
        var mind = mindSys.GetMind(player)!.Value;
        Assert.That(roleSys.MindIsAntagonist(mind), "Player mind was not marked as antagonist.");
        Assert.That(entMan.HasComponent<VampireComponent>(player), "Player entity did not get VampireComponent.");

        var vampComp = entMan.GetComponent<VampireComponent>(player);
        Assert.That(!entMan.HasComponent<HemomancerComponent>(player)
                    && !entMan.HasComponent<UmbraeComponent>(player)
                    && !entMan.HasComponent<DantalionComponent>(player)
                    && !entMan.HasComponent<GargantuaComponent>(player),
            "Vampire should start without a chosen class");
        Assert.That(vampComp.TotalBlood, Is.EqualTo(0),
            "Vampire should start with 0 blood");

        Assert.That(ruleComp.VampireMinds.Count, Is.EqualTo(1),
            "Expected exactly 1 vampire to be selected when only 1 player opts in");
        Assert.That(ruleComp.VampireMinds.Contains(mind),
            "The player who opted in should be selected as vampire");

        Assert.That(entMan.TryGetComponent<MindComponent>(mind, out var mindComp));
        Assert.That(mindComp.Objectives, Is.Not.Empty, "No objectives assigned to vampire!");
        var totalDifficulty = mindComp.Objectives.Sum(o => entMan.GetComponent<ObjectiveComponent>(o).Difficulty);
        Assert.That(totalDifficulty, Is.GreaterThan(0));

        await pair.CleanReturnAsync();
    }
}
