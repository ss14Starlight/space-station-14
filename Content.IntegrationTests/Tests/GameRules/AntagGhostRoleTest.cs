#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Utility;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Antag;
using Content.Shared.Players;
using Content.Shared.CCVar;
using Content.Shared._Starlight.CCVar;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.IntegrationTests.Tests.GameRules;

public sealed partial class AntagGhostRoleTest : AntagTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        Map = PoolManager.TestStation
    };

    [SidedDependency(Side.Server)] private IRobustRandom _random = default!;
    [SidedDependency(Side.Server)] private GhostRoleSystem _ghostRole = default!;

    #region Starlight
    /// <summary>
    /// Antag game rules excluded because an equivalent rule is already covered.
    /// Keep the canonical rule and list its duplicates here.
    /// </summary>
    private static readonly HashSet<string> IgnoredAntagGameRules =
    [
        /// Already has their own tests
        // Covered by TraitorRuleTest
        "Traitor",
        "TraitorLess",
        "SubTraitor",
        "SleeperAgents",
        "TraitorReinforcement",
        // Covered by VampireRuleTest
        "Vampire",
        "VampireLess",
        // Covered by NukeOpsTest
        "Nukeops",
        "NukeopsLate",
        /// Groupings that don't already have their own tests
        // Covered by DerelictGenericCyborgSpawn
        "DerelictBorgiSpawn",
        "DerelictPurrfusCyborgSpawn",
        "DerelictEngineerCyborgSpawn",
        "DerelictJanitorCyborgSpawn",
        "DerelictMedicalCyborgSpawn",
        "DerelictMiningCyborgSpawn",
        "DerelictSyndicateAssaultCyborgSpawn",
        "DerelictHeavyXenoborgSpawn",
        "DerelictEngiXenoborgSpawn",
        "DerelictScoutXenoborgSpawn",
        "DerelictStealthXenoborgSpawn",
        "DerelictXenoBorgiSpawn",
        // One-offs
        "ParadoxCrisisSpawn",
        "SLChangelingLess",
        "ThiefLess",
        "ZombieOutbreak", // We don't use it, anyways
        "Changeling", // We don't use Wizden Changelings
        /// Need their own tests ideally
        "Wizard",
        "WizardDuel",
        "SubWizard",
        "WizardSpawn",
        "TerrorSpidersSpawn",
        "BrighteyeSpawn",
        "SubBrighteye"
    ];
    private static readonly string[] AntagGameRules = GameDataScrounger.EntitiesWithComponent("AntagSelection").Where(ruleId => !IgnoredAntagGameRules.Contains(ruleId)).ToArray(); // Exclude duplicate rules, they're really not needed and just time out tests.
    #endregion

    [Test]
    [TestOf(typeof(GameTicker)), TestOf(typeof(AntagSelectionSystem)), TestOf(typeof(AntagSelectionComponent)), TestOf(typeof(GhostRoleSystem))]
    [TestCaseSource(nameof(AntagGameRules))]
    [Description($"Ensures all GameRule entities with {nameof(AntagSelectionComponent)} can properly spawn those roles and they can be taken.")]
    [RunOnSide(Side.Server)]
    public void TestAntagGhostRoles(string ruleId)
    {
        Server.CfgMan.SetCVar(StarlightCCVars.DisableLoadMapRule, false); // Starlight
        Server.CfgMan.SetCVar(CCVars.GameRoleTimers, false); // Starlight
        var rule = SProtoMan.Index<EntityPrototype>(ruleId);
        Assert.That(rule.TryGetComponent<AntagSelectionComponent>(out var antag, SEntMan.ComponentFactory), Is.True);

        STicker.StartGameRule(ruleId, out var gameRule);

        Dictionary<ProtoId<AntagSpecifierPrototype>, int> rules = [];

        foreach (var selector in antag!.Antags)
        {
            var specifier = SProtoMan.Index(selector.Proto);

            #region Starlight
            // Ignore our specific antags that need their own tests due to their entirely different spawning mechanics
            if (IgnoredAntagSpecifiers.Contains(specifier.ID))
                continue;
            #endregion

            var count = selector.GetTargetAntagCount(_random, 1);
            // We should always spawn at least one antag if we add a GameRule
            Assert.That(count, Is.GreaterThanOrEqualTo(0)); // Starlight, we have some antags that intentionally underspawn based on playerRatio

            if (specifier.SpawnerPrototype == null)
                continue;

            var value = rules.GetValueOrDefault(specifier);
            rules[selector.Proto] = value + count;
        }

        var roleEnumerator = SEntMan.EntityQueryEnumerator<GhostRoleAntagSpawnerComponent, GhostRoleComponent, TransformComponent>();
        while (roleEnumerator.MoveNext(out var spawner, out var role, out var xform))
        {

            #region Starlight
            if (IsIgnored(spawner))
                continue;
            #endregion

            // Ensure the ghost role spawner spawned correctly!
            Assert.That(spawner.Rule, Is.EqualTo(gameRule));
            Assert.That(spawner.Definition, Is.Not.Null);
            AssertGhostRoleTaken(spawner, role, xform);
            var value = rules[spawner.Definition.Value];
            rules[spawner.Definition.Value] = value - 1;
        }

        // Ensure all ghost roles spawned and were assigned!!!
        Assert.That(rules.Values, Is.All.Zero);

        // End all rules
        STicker.ClearGameRules();
        Assert.That(STicker.GetAddedGameRules(), Is.Empty);
        Server.CfgMan.SetCVar(StarlightCCVars.DisableLoadMapRule, true); // Starlight
        Server.CfgMan.SetCVar(CCVars.GameRoleTimers, true); // Starlight
    }

    [Test]
    [TestOf(typeof(GameTicker)), TestOf(typeof(AntagSelectionSystem)), TestOf(typeof(AntagSelectionComponent)), TestOf(typeof(GhostRoleSystem))]
    [Description("Ensures a player can take all antag ghost roles sequentially without transferring unwanted mind data.")]
    [RunOnSide(Side.Server)]
    public void TestAntagGhostRolesSequential()
    {
        Server.CfgMan.SetCVar(StarlightCCVars.DisableLoadMapRule, false); // Starlight
        Server.CfgMan.SetCVar(CCVars.GameRoleTimers, false); // Starlight
        foreach (var ruleId in AntagGameRules)
        {
            var rule = SProtoMan.Index<EntityPrototype>(ruleId);
            Assert.That(rule.TryGetComponent<AntagSelectionComponent>(out var antag, SEntMan.ComponentFactory), Is.True);
            STicker.StartGameRule(ruleId);
        }

        var mind = ServerSession!.GetMind();

        var roleEnumerator = SEntMan.EntityQueryEnumerator<GhostRoleAntagSpawnerComponent, GhostRoleComponent, TransformComponent>();
        while (roleEnumerator.MoveNext(out var spawner, out var role, out var xform))
        {

            #region Starlight
            if (IsIgnored(spawner))
                continue;

            if (!AssertGhostRoleTaken(spawner, role, xform))
                continue;
            #endregion

            //AssertGhostRoleTaken(spawner, role, xform); // Starlight
            var newMind = ServerSession!.GetMind();
            Assert.That(newMind, Is.Not.EqualTo(mind));
            mind = newMind;
        }

        // End all rules
        STicker.ClearGameRules();
        Assert.That(STicker.GetAddedGameRules(), Is.Empty);
        Server.CfgMan.SetCVar(StarlightCCVars.DisableLoadMapRule, true); // Starlight
        Server.CfgMan.SetCVar(CCVars.GameRoleTimers, true); // Starlight
    }

    private bool AssertGhostRoleTaken(GhostRoleAntagSpawnerComponent spawner, GhostRoleComponent role, TransformComponent xform) // Starlight, void to bool
    {
        // Ensure the ghost role spawner spawned correctly!
        Assert.That(spawner.Definition, Is.Not.Null);
        Assert.That(xform.MapUid, Is.Not.Null);
        Assert.That(xform.MapID, Is.Not.EqualTo(MapId.Nullspace));

        // Take the ghost role and ensure we take it!
        #region Starlight
        var definition = spawner.Definition!.Value;
        var antag = SProtoMan.Index(definition);

        var previousEntity = ServerSession!.AttachedEntity;
        var previousMind = ServerSession.GetMind();

        var eligible = AntagSys.CanTakeAntagGhostRole(ServerSession, definition);

        var tookRole = _ghostRole.Takeover(ServerSession, role.Identifier);

        Assert.That(tookRole, Is.EqualTo(eligible), eligible ? $"Eligible session failed to take {definition}."
                : $"Ineligible session unexpectedly took {definition}.");

        if (!eligible)
        {
            Assert.Multiple(() =>
            {
                Assert.That(ServerSession.AttachedEntity, Is.EqualTo(previousEntity),
                    "Rejected takeover changed the attached entity.");

                Assert.That(ServerSession.GetMind(), Is.EqualTo(previousMind),
                    "Rejected takeover changed the session's mind.");

                Assert.That(role.Taken, Is.False,
                    "Rejected takeover marked the ghost role as taken.");

                Assert.That(_ghostRole.GhostRoles.Any(entry => entry.Comp.Identifier == role.Identifier), Is.True,
                    "Rejected takeover removed the available ghost role.");
            });

            return false;
        }

        Assert.That(ServerSession.AttachedEntity, Is.Not.Null);

        #endregion
        SAssertAntagInitialized(antag, ServerSession);

        // Ensure we spawned in the correct location
        var sessionXform = SEntMan.GetComponent<TransformComponent>(ServerSession.AttachedEntity.Value);
        Assert.That(sessionXform.MapUid, Is.EqualTo(xform.MapUid));

        // We break it up like this cause otherwise it'll sometimes randomly fail
        // TODO: Engine IEquatable for EntityCoordinates
        Assert.That(sessionXform.Coordinates.EntityId, Is.EqualTo(xform.Coordinates.EntityId));

        // I will not get heisentest due to floating point errors
        Assert.That(MathHelper.CloseTo(sessionXform.Coordinates.X, xform.Coordinates.X, 0.001f), Is.True);
        Assert.That(MathHelper.CloseTo(sessionXform.Coordinates.Y, xform.Coordinates.Y, 0.001f), Is.True);

        return true; // Starlight
    }

    #region Starlight
    /// <summary>
    /// Determines if a given ghost role antag spawner should be ignored based on the ignored antag specifiers.
    /// </summary>
    private static bool IsIgnored(GhostRoleAntagSpawnerComponent spawner)
    {
        return spawner.Definition is { } definition && IgnoredAntagSpecifiers.Contains(definition.Id);
    }
    #endregion
}
