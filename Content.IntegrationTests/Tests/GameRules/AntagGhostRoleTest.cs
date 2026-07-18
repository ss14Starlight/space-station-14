#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Utility;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Antag;
using Content.Shared.Players;
using Content.Shared.Roles;
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
    [SidedDependency(Side.Server)] private AntagSelectionSystem _antagSelection = default!; // Starlight
    [SidedDependency(Side.Server)] private PlayTimeTrackingSystem _playTime = default!; // Starlight

    private static readonly string[] AntagGameRules = GameDataScrounger.EntitiesWithComponent("AntagSelection");

    [Test]
    [TestOf(typeof(GameTicker)), TestOf(typeof(AntagSelectionSystem)), TestOf(typeof(AntagSelectionComponent)), TestOf(typeof(GhostRoleSystem))]
    [TestCaseSource(nameof(AntagGameRules))]
    [Description($"Ensures all GameRule entities with {nameof(AntagSelectionComponent)} can properly spawn those roles and they can be taken.")]
    [RunOnSide(Side.Server)]
    public void TestAntagGhostRoles(string ruleId)
    {
        Server.CfgMan.SetCVar(StarlightCCVars.DisableLoadMapRule, false); // Starlight
        var rule = SProtoMan.Index<EntityPrototype>(ruleId);
        Assert.That(rule.TryGetComponent<AntagSelectionComponent>(out var antag, SEntMan.ComponentFactory), Is.True);

        STicker.StartGameRule(ruleId, out var gameRule);
        var gameRuleSelection = SEntMan.GetComponent<AntagSelectionComponent>(gameRule); // Starlight

        Dictionary<ProtoId<AntagSpecifierPrototype>, int> rules = [];

        foreach (var selector in antag!.Antags)
        {
            var specifier = SProtoMan.Index(selector.Proto);
            var count = _antagSelection.GetTargetAntagCount((gameRule, gameRuleSelection), 1, selector.Proto); // Starlight, get the target antag count for this selector
            // We should always spawn at least one antag if we add a GameRule
            Assert.That(count, Is.GreaterThanOrEqualTo(0)); // Starlight, we have some antags that intentionally underspawn based on playerRatio. As the person who implemented that... I'm really starting to regret my life choices.

            if (specifier.SpawnerPrototype == null)
                continue;

            var value = rules.GetValueOrDefault(specifier);
            rules[selector.Proto] = value + count;
        }

        var roleEnumerator = SEntMan.EntityQueryEnumerator<GhostRoleAntagSpawnerComponent, GhostRoleComponent, TransformComponent>();
        while (roleEnumerator.MoveNext(out var spawner, out var role, out var xform))
        {
            // Ensure the ghost role spawner spawned correctly!
            Assert.That(spawner.Rule, Is.EqualTo(gameRule));
            Assert.That(spawner.Definition, Is.Not.Null);
            AssertGhostRoleTaken(spawner, role, xform);
            var value = rules[spawner.Definition.Value];
            rules[spawner.Definition.Value] = value - 1;
        }

        // Ensure all ghost roles spawned and were assigned!!!
        Assert.That(rules.Values, Is.All.GreaterThanOrEqualTo(0)); // Starlight. Some rules may target more antags than can be physically placed on this integration map, aka Terror Spiders, so ensure we didn't over-spawn instead.

        // End all rules
        STicker.ClearGameRules();
        Assert.That(STicker.GetAddedGameRules(), Is.Empty);
        Server.CfgMan.SetCVar(StarlightCCVars.DisableLoadMapRule, true); // Starlight
    }

    [Test]
    [TestOf(typeof(GameTicker)), TestOf(typeof(AntagSelectionSystem)), TestOf(typeof(AntagSelectionComponent)), TestOf(typeof(GhostRoleSystem))]
    [Description("Ensures a player can take all antag ghost roles sequentially without transferring unwanted mind data.")]
    [RunOnSide(Side.Server)]
    public void TestAntagGhostRolesSequential()
    {
        Server.CfgMan.SetCVar(StarlightCCVars.DisableLoadMapRule, false); // Starlight
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
            // Attempt to take the ghost role for this spawner.
            var tookRole = AssertGhostRoleTaken(spawner, role, xform);
            if (!tookRole)
                continue;
            #endregion

            var newMind = ServerSession!.GetMind();
            Assert.That(newMind, Is.Not.EqualTo(mind));
            mind = newMind;
        }

        // End all rules
        STicker.ClearGameRules();
        Assert.That(STicker.GetAddedGameRules(), Is.Empty);
        Server.CfgMan.SetCVar(StarlightCCVars.DisableLoadMapRule, true); // Starlight
    }

    private bool AssertGhostRoleTaken(GhostRoleAntagSpawnerComponent spawner, GhostRoleComponent role, TransformComponent xform) // Starlight, returns a bool instead, so we can use it elsewhere
    {
        // Ensure the ghost role spawner spawned correctly!
        Assert.That(spawner.Definition, Is.Not.Null);
        Assert.That(xform.MapUid, Is.Not.Null);
        Assert.That(xform.MapID, Is.Not.EqualTo(MapId.Nullspace));

        #region Starlight
        // Takeover should match runtime eligibility checks.
        var definition = SProtoMan.Index(spawner.Definition!.Value);
        var eligibilityRoles = definition.PrefRoles;
        if (eligibilityRoles.Count == 0 && SProtoMan.HasIndex<AntagPrototype>(definition.ID))
            eligibilityRoles = [definition.ID];

        var expectedAllowed = !_antagSelection.IsAntagBanned(ServerSession!, definition)
                              && _playTime.IsAllowed(ServerSession!, eligibilityRoles);
        Assert.That(_ghostRole.Takeover(ServerSession!, role.Identifier), Is.EqualTo(expectedAllowed));

        if (!expectedAllowed)
            return false; // Starlight
        #endregion

        // Take the ghost role and ensure we take it!
        Assert.That(ServerSession!.AttachedEntity, Is.Not.Null);
        SAssertAntagInitialized(definition, ServerSession); // Starlight antag -> Definition

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
}
