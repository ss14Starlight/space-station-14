using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Utility;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Shuttles.Components;
using Content.Shared.Antag;
using Content.Shared.CCVar;
using Content.Shared._Starlight.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
public sealed class AllGamePresetsStartTest : AntagTest
{
    /// <summary>
    /// A list of blacklisted <see cref="GamePresetPrototype"/> for this test. Some down streams might make changes which nuke upstream game modes they don't use.
    /// This prevents them from being tested. If you use this to silence valid test fails and your game fails to start. Skill issue. Do 100 push-ups.
    /// </summary>
    private static readonly HashSet<string> IgnoredPresets = ["TerrorSpiders"]; // Is a string to prevent YAML Linter from freaking if this is empty. - Starlight, Terror Spiders need their own test

    private static readonly HashSet<string> IgnoredAntagSpecifiers = ["Brighteye"]; // Starlight, we're ignoring Brighteye as it needs its own test

    private static string[] _gamePresets = GameDataScrounger.PrototypesOfKind<GamePresetPrototype>().Where(p => !IgnoredPresets.Contains(p)).ToArray();

    // Tests that all game modes can start given ideal circumstances.
    [Test]
    [TestOf(typeof(GameTicker)), TestOf(typeof(AntagSelectionSystem)), TestOf(typeof(AntagSelectionComponent))]
    [TestCaseSource(nameof(_gamePresets))]
    [Description("Ensures all Game Presets are able to start and assign all antags correctly without spawning anyone in nullspace.")]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GameTickerIgnoredPresets), GameTicker.DummyGameRule)]
    public async Task TestAllGamemodesCanStart(string presetId)
    {
        Server.CfgMan.SetCVar(StarlightCCVars.DisableLoadMapRule, false); // Starlight
        Server.CfgMan.SetCVar(CCVars.GameRoleTimers, false); // Starlight
        // Initially in the lobby
        await Server.WaitPost(() =>
        {
            Assert.That(STicker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
            Assert.That(Pair.Player!.AttachedEntity, Is.Null); // Starlight, use pair.player instead of client since our roles are *still* tied to player
            Assert.That(STicker.PlayerGameStatuses[Pair.Player!.UserId], Is.EqualTo(PlayerGameStatus.NotReadyToPlay)); // Starlight, client -> pair.player
        });

        var preset = SProtoMan.Index<GamePresetPrototype>(presetId);

        // Spawn the minimum number of players.
        var players = new List<ICommonSession>();
        players.Add(Pair.Player!); // Starlight, client -> pair.player
        var min = 0;
        await Server.WaitPost(() =>
        {
            min = STicker.GetMinimumPlayerCount(preset);
        });

        // We should already have one client connected, and we need to check the min

        // If we have antags, make sure that those with the correct preferences can spawn with them!
        List<(AntagSpecifierPrototype, int)> rules = [];

        var antags = 0;
        await Server.WaitPost(() =>
        {
            foreach (var ruleId in preset.Rules)
            {
                if (STicker.IsIgnored(ruleId))
                    continue;

                if (!SProtoMan.Resolve(ruleId, out var rule ))
                    continue; // Bruh moment

                // Ignore non-antag game-rules.
                if (!rule.TryGetComponent<AntagSelectionComponent>(out var antag, SEntMan.ComponentFactory))
                    continue;

                #region Starlight
                // Also ignore ones that don't actually select, aka, LoneOp being a round start in Aller at Once
                if (antag.SelectionTime == AntagSelectionTime.Never)
                    continue;
                #endregion

                var runningCount = 0;

                foreach (var selector in antag.Antags)
                {
                    // Throw on invalid prototypes, skip roundstart ghost roles.
                    if (!SProtoMan.Resolve(selector.Proto, out var definition) || definition.PrefRoles.Count == 0 || !definition.PickPlayer || IgnoredAntagSpecifiers.Contains(definition.ID)) // Starlight, Brighteyes need their own test, so we exclude them
                        continue;

                    var count = AntagSys.GetTargetAntagCount(selector, min, ref runningCount);
                    antags += count;
                    rules.Add((definition, count));
                }
            }
        });

        // No preset should ever try to spawn more antags roundstart than it can spawn players.
        Assert.That(antags <= min, Is.True);
        if (min > 1)
        {
            var dummies = await Server.AddDummySessions(min - 1);
            // Put our client at the front of the list.
            players = players.Union(dummies).ToList();
        }

        await Pair.RunUntilSynced();

        // This also ensures that admin commands work properly :P
        await Server.WaitPost(() =>
        {
            STicker.ToggleReadyAll(true);
        });

        var i = 0;
        foreach (var (antag, amount) in rules)
        {
            for (var count = 0; count < amount; count++)
            {
                await Pair.SetAntagPreference(antag.PrefRoles.FirstOrDefault(), true, players[i++].UserId);
                Assert.That(i < min, $"Tried to assign more antags than there were players");
            }
        }

        await Pair.RunUntilSynced();
        await Pair.WaitCommand($"setgamepreset {presetId}");
        await Pair.WaitCommand("startround");
        await Pair.RunUntilSynced();

        // Game should have started
        await Server.WaitPost(() =>
        {
            Assert.That(STicker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(STicker.PlayerGameStatuses.Values.All(x => x == PlayerGameStatus.JoinedGame));
            Assert.That(STicker.PlayerGameStatuses, Has.Count.EqualTo(players.Count));
        });
        Assert.That(CEntMan.EntityExists(Pair.Player!.AttachedEntity)); // Starlight, client -> pair.player

        var player = Pair.Player!.AttachedEntity!.Value;
        Assert.That(SEntMan.EntityExists(player));

        // Start all game presets so antags spawn!
        await Server.WaitPost(() =>
        {
            #region Starlight
            // STicker.StartGamePresetRules();
            // Force every rule to start, including rules currently waiting on a delay.
            // Repeat because starting one rule can add additional rules.
            for (var pass = 0; pass < 5; pass++)
            {
                var madeProgress = false;

                foreach (var rule in STicker.GetAddedGameRules().ToArray())
                {
                    madeProgress |= STicker.StartGameRule(rule);
                }

                if (!madeProgress)
                    break;
            }
            #endregion
        });
        await Pair.RunUntilSynced();

        await Server.WaitPost(() =>
        {
            #region Starlight
            // var j = 0;
            // foreach (var (antag, amount) in rules)
            var expectedCounts = rules
                .GroupBy(entry => entry.Item1)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(entry => entry.Item2));

            var actualCounts = expectedCounts.Keys
                .ToDictionary(antag => antag, _ => 0);

            foreach (var session in players)
            {
                foreach (var antagId in AntagSys.GetPreSelectedAntagSpecifiers(session))
                {
                    if (!SProtoMan.Resolve(antagId, out var actualAntag))
                    {
                        Assert.Fail($"Could not resolve preselected antag {antagId}");
                        continue;
                    }

                    // Ignore antags created by rules outside the direct preset list.
                    if (!actualCounts.ContainsKey(actualAntag))
                        continue;

                    actualCounts[actualAntag]++;
                    SAssertAntagInitialized(actualAntag, session);
                }
            }

            Assert.Multiple(() =>
            {
                foreach (var (antag, expected) in expectedCounts)
                {
                    Assert.That(
                        actualCounts[antag],
                        Is.EqualTo(expected),
                        $"Expected {expected} player(s) to become {antag.ID}, " +
                        $"but {actualCounts[antag]} were preselected.");
                }
            });
            #endregion
        });

        // Maps now exist
        Assert.That(SEntMan.Count<MapComponent>(), Is.GreaterThan(0));
        Assert.That(SEntMan.Count<MapGridComponent>(), Is.GreaterThan(0));
        Assert.That(SEntMan.Count<StationCentcommComponent>(), Is.EqualTo(1));

        // Clear game preset and return to lobby
        await Pair.WaitCommand("golobby");
        STicker.SetGamePreset((GamePresetPrototype) null);
        await Pair.RunUntilSynced();
        Server.CfgMan.SetCVar(StarlightCCVars.DisableLoadMapRule, true); // Starlight
        Server.CfgMan.SetCVar(CCVars.GameRoleTimers, true); // Starlight
    }
}
