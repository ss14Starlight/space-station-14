#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Pair;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;

namespace Content.IntegrationTests.Tests._Sol.Medical.Virology;

[TestFixture]
[TestOf(typeof(Content.Server._Sol.Medical.Virology.VirologyModeEligibilitySystem))]
public sealed class VirologyModeEligibilityTest : GameTest
{
    private const string EligibleMap = "SolVirologyEligibilityMap";
    private const string NoStationMap = "SolVirologyNoStationMap";

    [TestPrototypes]
    private const string Prototypes = @"
- type: gameMap
  id: SolVirologyEligibilityMap
  mapName: SolVirologyEligibilityMap
  mapPath: /Maps/Test/empty.yml
  minPlayers: 0
  stations:
    Empty:
      mapNameTemplate: SolVirologyEligibilityMap
      stationProto: StandardNanotrasenStation
      components:
        - type: VirologyStation
        - type: StationJobs
          availableJobs:
            Virologist: [ 1, 1 ]
            Assistant: [ -1, -1 ]

- type: gameMap
  id: SolVirologyNoStationMap
  mapName: SolVirologyNoStationMap
  mapPath: /Maps/Test/empty.yml
  minPlayers: 0
  stations:
    Empty:
      mapNameTemplate: SolVirologyNoStationMap
      stationProto: StandardNanotrasenStation
      components:
        - type: StationJobs
          availableJobs:
            Virologist: [ 1, 1 ]
            Assistant: [ -1, -1 ]

- type: entity
  id: SolVirologyEligibilityRule
  parent: BaseGameRule
  categories: [ HideSpawnMenu ]
  components:
  - type: GameRule
    minPlayers: 0
  - type: VirologyModeRule

- type: gamePreset
  id: SolVirologyEligibilityPreset
  name: Sol Virology Eligibility
  description: Test preset for Virology ready-job gating
  showInVote: false
  rules:
  - SolVirologyEligibilityRule
";

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true,
    };

    [Test]
    public async Task HighPriorityReadyVirologistStartsRound()
    {
        var pair = Pair;
        var ticker = pair.Server.System<GameTicker>();
        ConfigureLobby(pair, EligibleMap);

        await pair.SetJobPreferences(["Virologist", "Assistant"]);
        await pair.SetJobPriority("Virologist", JobPriority.High);
        await pair.WaitClientCommand("toggleready True");

        await pair.WaitCommand("setgamepreset SolVirologyEligibilityPreset 9999");
        await pair.Server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(10);

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
        pair.AssertJob("Virologist");

        await Cleanup(pair, ticker);
    }

    [Test]
    public async Task MediumPriorityReadyVirologistIsRejected()
    {
        var pair = Pair;
        var ticker = pair.Server.System<GameTicker>();
        ConfigureLobby(pair, EligibleMap);

        await pair.SetJobPreferences(["Virologist", "Assistant"]);
        await pair.SetJobPriority("Virologist", JobPriority.Medium);
        await pair.WaitClientCommand("toggleready True");

        await pair.WaitCommand("setgamepreset SolVirologyEligibilityPreset 9999");
        await pair.Server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(10);

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));

        await Cleanup(pair, ticker);
    }

    [Test]
    public async Task UnreadyHighPriorityVirologistIsRejected()
    {
        var pair = Pair;
        var ticker = pair.Server.System<GameTicker>();
        ConfigureLobby(pair, EligibleMap);

        await pair.SetJobPreferences(["Virologist", "Assistant"]);
        await pair.SetJobPriority("Virologist", JobPriority.High);
        Assert.That(ticker.PlayerGameStatuses[pair.Client.User!.Value], Is.EqualTo(PlayerGameStatus.NotReadyToPlay));

        await pair.WaitCommand("setgamepreset SolVirologyEligibilityPreset 9999");
        await pair.Server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(10);

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));

        await Cleanup(pair, ticker);
    }

    [Test]
    public async Task ForcedStartStillRequiresReadyVirologist()
    {
        var pair = Pair;
        var ticker = pair.Server.System<GameTicker>();
        ConfigureLobby(pair, EligibleMap);

        await pair.SetJobPreferences(["Assistant"]);
        await pair.SetJobPriority("Assistant", JobPriority.High);
        await pair.WaitClientCommand("toggleready True");

        await pair.WaitCommand("setgamepreset SolVirologyEligibilityPreset 9999");
        await pair.Server.WaitPost(() => ticker.StartRound(force: true));
        await pair.RunTicksSync(10);

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));

        await Cleanup(pair, ticker);
    }

    [Test]
    public async Task MissingVirologyStationRejectsPreset()
    {
        var pair = Pair;
        var ticker = pair.Server.System<GameTicker>();
        ConfigureLobby(pair, NoStationMap);

        await pair.SetJobPreferences(["Virologist", "Assistant"]);
        await pair.SetJobPriority("Virologist", JobPriority.High);
        await pair.WaitClientCommand("toggleready True");

        await pair.WaitCommand("setgamepreset SolVirologyEligibilityPreset 9999");
        await pair.Server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(10);

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));

        await Cleanup(pair, ticker);
    }

    private static void ConfigureLobby(TestPair pair, string map)
    {
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, map);
        pair.Server.CfgMan.SetCVar(CCVars.GameLobbyFallbackEnabled, false);
        pair.Server.CfgMan.SetCVar(CCVars.GridFill, true);
    }

    private static async Task Cleanup(TestPair pair, GameTicker ticker)
    {
        ticker.SetGamePreset((GamePresetPrototype?) null);
        pair.Server.CfgMan.SetCVar(CCVars.GameLobbyFallbackEnabled, true);
        pair.Server.CfgMan.SetCVar(CCVars.GridFill, false);
        if (ticker.RunLevel == GameRunLevel.InRound)
        {
            await pair.Server.WaitPost(() => ticker.RestartRound());
            await pair.RunTicksSync(10);
        }
    }
}
