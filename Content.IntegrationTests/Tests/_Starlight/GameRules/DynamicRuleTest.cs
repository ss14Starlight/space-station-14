#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.GameTicking.Rules;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Rules;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.GameRules;

[TestFixture]
[TestOf(typeof(DynamicRuleSystem))]
public sealed class DynamicRuleTest : GameTest
{
    private const string SequentialBudgetRule = "TestDynamicSequentialBudget";
    private const string MutualExclusionRule = "TestDynamicMutualExclusion";
    private const string CooldownRule = "TestDynamicCooldown";
    private const string CooldownChildRule = "TestDynamicCooldownChild";
    private const string CooldownGroupOwnerRule = "TestDynamicCooldownGroupOwner";
    private const string CooldownGroupRelatedRule = "TestDynamicCooldownGroupRelated";
    private const string CooldownPreset = "TestDynamicCooldownPreset";
    private const string DefaultCooldownRule = "TestDynamicDefaultCooldown";
    private const string DynamicOnlyCooldownRule = "TestDynamicOnlyCooldown";

    [TestPrototypes]
    private const string Prototypes = """
    -   type: entity
        id: TestDynamicSequentialBudget
        parent: BaseGameRule
        components:
        -   type: GameRule
            minPlayers: 0
        -   type: DynamicRule
            startingBudgetMin: 325
            startingBudgetMax: 326
            budgetPerSecond: 0
            variantBudgetPerSecond: 0
            minRuleInterval: 86400
            maxRuleInterval: 86401
            table: !type:AllSelector
                children:
                -   id: TestDynamicBudgetFirst
                    conditions:
                    -   !type:HasBudgetCondition
                -   id: TestDynamicBudgetSecond
                    conditions:
                    -   !type:HasBudgetCondition

    -   type: entity
        id: TestDynamicBudgetFirst
        parent: BaseGameRule
        components:
        -   type: DynamicRuleCost
            cost: 125

    -   type: entity
        id: TestDynamicBudgetSecond
        parent: BaseGameRule
        components:
        -   type: DynamicRuleCost
            cost: 300

    -   type: entity
        id: TestDynamicMutualExclusion
        parent: BaseGameRule
        components:
        -   type: GameRule
            minPlayers: 0
        -   type: DynamicRule
            startingBudgetMin: 100
            startingBudgetMax: 101
            budgetPerSecond: 0
            variantBudgetPerSecond: 0
            minRuleInterval: 86400
            maxRuleInterval: 86401
            table: !type:AllSelector
                children:
                -   id: TestDynamicExclusiveFirst
                    conditions:
                    -   !type:HasBudgetCondition
                -   id: TestDynamicExclusiveSecond
                    conditions:
                    -   !type:HasBudgetCondition
                    -   !type:MutuallyExclusiveRuleCondition
                        rules:
                        - TestDynamicExclusiveFirst
                -   id: TestDynamicExclusiveFirst
                    conditions:
                    -   !type:HasBudgetCondition
                    -   !type:MaxRuleOccurenceCondition

    -   type: entity
        id: TestDynamicExclusiveFirst
        parent: BaseGameRule
        components:
        -   type: GameRule
            delay:
                min: 60
                max: 60
        -   type: DynamicRuleCost
            cost: 10

    -   type: entity
        id: TestDynamicExclusiveSecond
        parent: BaseGameRule
        components:
        -   type: DynamicRuleCost
            cost: 10

    -   type: entity
        id: TestDynamicCooldown
        parent: BaseGameRule
        components:
        -   type: GameRule
            minPlayers: 0
        -   type: DynamicRule
            startingBudgetMin: 100
            startingBudgetMax: 101
            budgetPerSecond: 0
            variantBudgetPerSecond: 0
            minRuleInterval: 86400
            maxRuleInterval: 86401
            table: !type:AllSelector
                children:
                -   id: TestDynamicCooldownChild
                    conditions:
                    -   !type:HasBudgetCondition

    -   type: entity
        id: TestDynamicCooldownChild
        parent: BaseGameRule
        components:
        -   type: DynamicRuleCost
            cost: 10
        -   type: DynamicRuleCooldown
            cooldown: 1

    -   type: entity
        id: TestDynamicCooldownGroupOwner
        parent: BaseGameRule
        components:
        -   type: DynamicRuleCooldown
            cooldown: 2
            rules:
            - TestDynamicCooldownGroupRelated
            presets:
            - TestDynamicCooldownPreset

    -   type: entity
        id: TestDynamicCooldownGroupRelated
        parent: BaseGameRule

    -   type: entity
        id: TestDynamicDefaultCooldown
        parent: BaseGameRule
        components:
        -   type: DynamicRuleCooldown
            cooldown: 1

    -   type: entity
        id: TestDynamicOnlyCooldown
        parent: BaseGameRule
        components:
        -   type: DynamicRuleCooldown
            cooldown: 1
            decrementOnNonDynamicRounds: false

    -   type: gamePreset
        id: TestDynamicCooldownPreset
        name: Test Dynamic Cooldown Preset
        description: Test preset for Dynamic cooldowns.
        showInVote: false
        voteCooldown: 3
        rules: []
    """;

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true,
    };

    /// <summary>
    /// Tests that the budget is updated between table children, so that a child rule can be selected and then the next child rule can be selected in the same table roll.
    /// </summary>
    [Test]
    public async Task DynamicBudgetUpdateTest()
    {
        var server = Pair.Server;

        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            var uid = ticker.AddGameRule(SequentialBudgetRule);
            var component = server.EntMan.GetComponent<DynamicRuleComponent>(uid);

            Assert.Multiple(() =>
            {
                Assert.That(component.Budget, Is.EqualTo(200));
                Assert.That(component.Rules, Has.Count.EqualTo(1));
                Assert.That(GetPrototypeId(server.EntMan, component.Rules.Single()),
                    Is.EqualTo("TestDynamicBudgetFirst"));
            });

            ticker.EndGameRule(uid);
        });
    }

    /// <summary>
    /// Tests that mutually exclusive rules are rejected within the same table roll, so that only one of the mutually exclusive rules is selected.
    /// </summary>
    /// <returns></returns>
    [Test]
    public async Task DynamicMutuallyExclusiveRulesRejectionTest()
    {
        var server = Pair.Server;

        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            var uid = ticker.AddGameRule(MutualExclusionRule);
            var component = server.EntMan.GetComponent<DynamicRuleComponent>(uid);

            Assert.Multiple(() =>
            {
                Assert.That(component.Budget, Is.EqualTo(90));
                Assert.That(component.Rules, Has.Count.EqualTo(1));
                Assert.That(GetPrototypeId(server.EntMan, component.Rules.Single()),
                    Is.EqualTo("TestDynamicExclusiveFirst"));
            });

            ticker.EndGameRule(uid);
        });
    }

    /// <summary>
    /// Tests that a rule with a cooldown is not selected in the next Dynamic round after being selected in the previous Dynamic round.
    /// </summary>
    [Test]
    public async Task DynamicRuleCooldownTest()
    {
        var server = Pair.Server;
        var ticker = server.System<GameTicker>();
        var dynamic = server.System<DynamicRuleSystem>();

        await server.WaitAssertion(() =>
        {
            var uid = ticker.AddGameRule(CooldownRule);
            var component = server.EntMan.GetComponent<DynamicRuleComponent>(uid);

            Assert.Multiple(() =>
            {
                Assert.That(component.Rules, Has.Count.EqualTo(1));
                Assert.That(GetPrototypeId(server.EntMan, component.Rules.Single()),
                    Is.EqualTo(CooldownChildRule));
            });

            // A cooldown applies to future Dynamic rounds, not later rolls in the current round.
            Assert.That(dynamic.ExecuteNow(uid).Count(), Is.EqualTo(1));
            Assert.That(component.Rules, Has.Count.EqualTo(2));
        });

        await server.WaitPost(() => ticker.RestartRound());
        await Pair.RunUntilSynced();

        await server.WaitAssertion(() =>
        {
            var uid = ticker.AddGameRule(CooldownRule);
            var component = server.EntMan.GetComponent<DynamicRuleComponent>(uid);

            Assert.Multiple(() =>
            {
                Assert.That(component.Rules, Is.Empty);
                Assert.That(component.Budget, Is.EqualTo(100));
            });

            // The snapshot remains in force for every roll during this Dynamic round.
            Assert.That(dynamic.ExecuteNow(uid), Is.Empty);
        });

        await server.WaitPost(() => ticker.RestartRound());
        await Pair.RunUntilSynced();

        await server.WaitAssertion(() =>
        {
            var uid = ticker.AddGameRule(CooldownRule);
            var component = server.EntMan.GetComponent<DynamicRuleComponent>(uid);

            Assert.That(component.Rules, Has.Count.EqualTo(1));
            Assert.That(GetPrototypeId(server.EntMan, component.Rules.Single()),
                Is.EqualTo(CooldownChildRule));
        });
    }

    /// <summary>
    /// Tests that related rules and presets form one cooldown group, and that selecting a linked preset
    /// applies the preset's own vote cooldown back to the Dynamic rules.
    /// </summary>
    [Test]
    public async Task DynamicRuleCooldownGroupTest()
    {
        var server = Pair.Server;
        var ticker = server.System<GameTicker>();
        var cooldowns = server.System<DynamicRuleCooldownSystem>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var ownerRule = new EntProtoId(CooldownGroupOwnerRule);
        var relatedRule = new EntProtoId(CooldownGroupRelatedRule);

        await server.WaitPost(() => ticker.RestartRound());
        await Pair.RunUntilSynced();

        await server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: true);
            // Selecting an included rule activates the owning rule's whole cooldown group.
            cooldowns.ApplyRuleCooldown(relatedRule);
        });

        await server.WaitPost(() => ticker.RestartRound());
        await Pair.RunUntilSynced();

        await server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: false);

            Assert.Multiple(() =>
            {
                Assert.That(cooldowns.CurrentRuleCooldowns, Does.Contain(ownerRule));
                Assert.That(cooldowns.CurrentRuleCooldowns, Does.Contain(relatedRule));
                Assert.That(
                    cooldowns.TryGetPresetCooldown(new ProtoId<GamePresetPrototype>(CooldownPreset), out var remaining),
                    Is.True);
                Assert.That(remaining, Is.EqualTo(2));
            });
        });

        await server.WaitPost(() => ticker.RestartRound());
        await Pair.RunUntilSynced();
        await server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: true);

            Assert.Multiple(() =>
            {
                Assert.That(cooldowns.CurrentRuleCooldowns, Does.Contain(ownerRule));
                Assert.That(cooldowns.CurrentRuleCooldowns, Does.Contain(relatedRule));
                Assert.That(
                    cooldowns.TryGetPresetCooldown(new ProtoId<GamePresetPrototype>(CooldownPreset), out var remaining),
                    Is.True);
                Assert.That(remaining, Is.EqualTo(1));
            });
        });

        await server.WaitPost(() => ticker.RestartRound());
        await Pair.RunUntilSynced();

        await server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: true);
            Assert.That(cooldowns.CurrentRuleCooldowns, Does.Not.Contain(ownerRule));

            var preset = prototypes.Index(new ProtoId<GamePresetPrototype>(CooldownPreset));
            cooldowns.ApplyPresetCooldown(preset);
        });

        await server.WaitPost(() => ticker.RestartRound());
        await Pair.RunUntilSynced();

        await server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: true);

            Assert.Multiple(() =>
            {
                Assert.That(cooldowns.CurrentRuleCooldowns, Does.Contain(ownerRule));
                Assert.That(cooldowns.CurrentRuleCooldowns, Does.Contain(relatedRule));
                Assert.That(
                    cooldowns.TryGetPresetCooldown(new ProtoId<GamePresetPrototype>(CooldownPreset), out var remaining),
                    Is.True);
                Assert.That(remaining, Is.EqualTo(3));
            });
        });
    }

    /// <summary>
    /// Tests that cooldowns count non-Dynamic rounds by default and can opt out.
    /// </summary>
    [Test]
    public async Task DynamicRuleCooldownNonDynamicDecrementTest()
    {
        var server = Pair.Server;
        var ticker = server.System<GameTicker>();
        var cooldowns = server.System<DynamicRuleCooldownSystem>();
        var defaultRule = new EntProtoId(DefaultCooldownRule);
        var dynamicOnlyRule = new EntProtoId(DynamicOnlyCooldownRule);

        await server.WaitPost(() => ticker.RestartRound());
        await Pair.RunUntilSynced();

        await server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: true);
            cooldowns.ApplyRuleCooldown(defaultRule);
            cooldowns.ApplyRuleCooldown(dynamicOnlyRule);
        });

        await server.WaitPost(() => ticker.RestartRound());
        await Pair.RunUntilSynced();

        await server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: false);

            Assert.Multiple(() =>
            {
                Assert.That(cooldowns.CurrentRuleCooldowns, Does.Contain(defaultRule));
                Assert.That(cooldowns.CurrentRuleCooldowns, Does.Contain(dynamicOnlyRule));
            });
        });

        await server.WaitPost(() => ticker.RestartRound());
        await Pair.RunUntilSynced();

        await server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: true);

            Assert.Multiple(() =>
            {
                Assert.That(cooldowns.CurrentRuleCooldowns, Does.Not.Contain(defaultRule));
                Assert.That(cooldowns.CurrentRuleCooldowns, Does.Contain(dynamicOnlyRule));
            });
        });

        await server.WaitPost(() => ticker.RestartRound());
        await Pair.RunUntilSynced();

        await server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: true);
            Assert.That(cooldowns.CurrentRuleCooldowns, Does.Not.Contain(dynamicOnlyRule));
        });
    }

    private static string? GetPrototypeId(IEntityManager entityManager, EntityUid uid) => entityManager.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID;
}
