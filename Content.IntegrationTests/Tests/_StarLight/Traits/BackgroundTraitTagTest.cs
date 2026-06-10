using Content.Client.Lobby;
using Content.Server.GameTicking;
using Content.Shared._Starlight.Traits;
using Content.Shared._Starlight.Traits.Effects;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Traits;

/// <summary>
/// Regression tests for issue #4647: BackgroundEffect builds its tag id at runtime
/// (background + "TraitBackground"), so a background without a matching TagPrototype
/// slips past the YAML linter and fails to apply on spawn (with an error log / debug
/// assert in TagSystem).
/// </summary>
[TestFixture]
public sealed class BackgroundTraitTagTest
{
    private const string Map = "BackgroundTraitTagTestMap";

    private static readonly ProtoId<TraitPrototype> BackgroundTrait = "Frontier";
    private const string ExpectedTag = "FrontierTraitBackground";
    private static readonly ProtoId<JobPrototype> Passenger = "Assistant";

    [TestPrototypes]
    private static readonly string Prototypes = $@"
- type: gameMap
  id: {Map}
  mapName: {Map}
  mapPath: /Maps/Test/empty.yml
  minPlayers: 0
  stations:
    Empty:
      mapNameTemplate: {Map}
      stationProto: StandardNanotrasenStation
      components:
        - type: StationJobs
          availableJobs:
            Assistant: [ -1, -1 ]
";

    /// <summary>
    /// Every BackgroundEffect on every trait must reference a registered TagPrototype.
    /// This is the invariant that the YAML linter cannot check because the tag id is
    /// constructed in C# at runtime.
    /// </summary>
    [Test]
    public async Task AllBackgroundEffectTagsRegistered()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();

        await pair.Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var trait in protoMan.EnumeratePrototypes<TraitPrototype>())
                {
                    foreach (var effect in trait.Effects)
                    {
                        if (effect is not BackgroundEffect background)
                            continue;

                        var tagId = background.Background + "TraitBackground";
                        Assert.That(protoMan.HasIndex<TagPrototype>(tagId),
                            $"Trait '{trait.ID}' has a BackgroundEffect that requires tag '{tagId}', " +
                            "but no such TagPrototype is registered in tags.yml. " +
                            "The background will fail to apply on spawn (issue #4647).");
                    }
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Public-path check: a character whose profile selects a background trait must
    /// actually spawn with the corresponding tag applied.
    /// </summary>
    [Test]
    public async Task BackgroundTraitAppliesOnSpawn()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            InLobby = true,
        });
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, Map);

        var ticker = pair.Server.System<GameTicker>();
        ticker.SetGamePreset("Extended");

        var cPref = pair.Client.ResolveDependency<IClientPreferencesManager>();
        var cProto = pair.Client.ResolveDependency<IPrototypeManager>();

        await pair.ReallyBeIdle();

        await pair.Client.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.Random()
                .AsEnabled()
                .WithJobPreferences([Passenger])
                .WithTraitPreference(BackgroundTrait, cProto);

            Assert.That(profile.TraitPreferences, Does.Contain(BackgroundTrait),
                $"Profile rejected the '{BackgroundTrait}' trait preference.");

            cPref.CreateCharacter(profile);

            // delete the default character so the new one is the only candidate
            cPref.DeleteCharacter(0);

            cPref.UpdateJobPriorities(new() { { Passenger, JobPriority.High } });
        });

        await pair.ReallyBeIdle();

        ticker.ToggleReadyAll(true);
        await pair.Server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(30);

        Assert.That(ticker.PlayerGameStatuses[pair.Client.User!.Value], Is.EqualTo(PlayerGameStatus.JoinedGame));

        var mob = pair.Player!.AttachedEntity!.Value;
        Assert.That(pair.Server.EntMan.TryGetComponent<TagComponent>(mob, out var tags));
        // check Tags directly instead of TagSystem.HasTag, so that an unregistered tag id
        // reports a clean failure instead of tripping the same debug assert under test
        Assert.That(tags!.Tags, Does.Contain(new ProtoId<TagPrototype>(ExpectedTag)),
            $"Spawned character is missing the '{ExpectedTag}' tag: the background trait effect did not apply.");

        await pair.Server.WaitPost(() => ticker.RestartRound());
        await pair.CleanReturnAsync();
    }
}
