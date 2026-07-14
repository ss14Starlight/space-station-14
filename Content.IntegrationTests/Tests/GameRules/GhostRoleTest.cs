#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Utility;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Players.PlayTimeTracking; // Starlight
using Content.Shared._Starlight.CCVar; // Starlight
using Content.Shared.Antag;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
public sealed partial class GhostRoleTest : GameTest
{
    [SidedDependency(Side.Server)] private IRobustRandom _random = default!;
    [SidedDependency(Side.Server)] private GameTicker _ticker = default!;
    [SidedDependency(Side.Server)] private GhostRoleSystem _ghostRole = default!;
    [SidedDependency(Side.Server)] private PlayTimeTrackingSystem _playTime = default!; // Starlight

    private static string[] _antagGameRules = GameDataScrounger.EntitiesWithComponent("AntagSelection");

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        Map = PoolManager.TestStation
    };

    [Test]
    [TestOf(typeof(GameTicker)), TestOf(typeof(AntagSelectionSystem)), TestOf(typeof(AntagSelectionComponent)), TestOf(typeof(GhostRoleSystem))]
    [TestCaseSource(nameof(_antagGameRules))]
    [Description("Ensures all GameRule entities with AntagSelectionComponent can properly spawn those roles and they can be taken.")]
    [RunOnSide(Side.Server)]
    public void TestAntagGhostRoles(string ruleId)
    {
        #region Starlight
        // The vast majority of this is from Wizden, but it's been moved around because we disable load map on tests. Alas...
        // It's really just "Starlight" because of the Try-Finally ccvar stuff.
        var serverCfg = Pair.Server.CfgMan;
        serverCfg.SetCVar(StarlightCCVars.DisableLoadMapRule, false);

        try
        {
            var rule = SProtoMan.Index<EntityPrototype>(ruleId);
            Assert.That(rule.TryGetComponent<AntagSelectionComponent>(out var antag, SEntMan.ComponentFactory), Is.True);

            _ticker.StartGameRule(ruleId, out var gameRule);

            // Some rules can be ended before activation in integration configuration (e.g. map loading disabled).
            // If the rule is not active, it cannot spawn antag ghost roles, so this case is not testable here.
            if (!_ticker.IsGameRuleActive(gameRule))
            {
                _ticker.ClearGameRules();
                Assert.That(_ticker.GetAddedGameRules(), Is.Empty);
                return;
            }

            Dictionary<ProtoId<AntagSpecifierPrototype>, int> rules = [];

            foreach (var selector in antag!.Antags)
            {
                var specifier = SProtoMan.Index(selector.Proto);
                var count = selector.GetTargetAntagCount(_random, 1);
                // Starlight - Some selectors (e.g. linear with min 0) validly return zero at low player counts.
                Assert.That(count, Is.GreaterThanOrEqualTo(0)); // Starlight - GreaterThan -> GreaterThanOrEqualTo

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
                Assert.That(xform.MapUid, Is.Not.Null);
                Assert.That(xform.MapID, Is.Not.EqualTo(MapId.Nullspace));

                var value = rules[spawner.Definition.Value];
                rules[spawner.Definition.Value] = value - 1;

                var definition = SProtoMan.Index(spawner.Definition.Value);
                var canTakeRole = _playTime.IsAllowed(ServerSession!, definition.PrefRoles);

                // Take the ghost role. Some antags (e.g. Brighteye) have requirement gating.
                var tookRole = _ghostRole.Takeover(ServerSession!, role.Identifier);
                Assert.That(tookRole, Is.EqualTo(canTakeRole));

                if (!tookRole)
                    continue;

                Assert.That(ServerSession!.AttachedEntity, Is.Not.Null);

                // Ensure we spawned in the correct location
                var sessionXform = SEntMan.GetComponent<TransformComponent>(ServerSession.AttachedEntity.Value);
                Assert.That(sessionXform.MapUid, Is.EqualTo(xform.MapUid));

                // We break it up like this cause otherwise it'll sometimes randomly fail
                // TODO: Engine IEquatable for EntityCoordinates
                Assert.That(sessionXform.Coordinates.EntityId, Is.EqualTo(xform.Coordinates.EntityId));

                // I will not get heisentest due to floating point errors
                Assert.That(MathHelper.CloseTo(sessionXform.Coordinates.X, xform.Coordinates.X, 0.001f), Is.True);
                Assert.That(MathHelper.CloseTo(sessionXform.Coordinates.Y, xform.Coordinates.Y, 0.001f), Is.True);
            }

            // Ensure all ghost roles spawned and were assigned!!!
            Assert.That(rules.Values, Is.All.Zero);

            // End all rules
            _ticker.ClearGameRules();
            Assert.That(_ticker.GetAddedGameRules(), Is.Empty);
        }
        finally
        {
            serverCfg.SetCVar(StarlightCCVars.DisableLoadMapRule, true);
        }
        #endregion
    }
}
