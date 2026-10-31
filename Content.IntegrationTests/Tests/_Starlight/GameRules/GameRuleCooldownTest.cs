#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.GameTicking.Rules;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.CCVar;
using Content.Shared.GameTicking.Rules;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.IntegrationTests.Tests._Starlight.GameRules;

[TestFixture]
[TestOf(typeof(DynamicRuleCooldownSystem))]
public sealed class GameRuleCooldownTest : GameTest
{
    private const string Rule = "TestSharedCooldownRule";
    private const string Dynamic = "TestSharedCooldownDynamic";
    private const string Preset = "TestSharedCooldownPreset";
    private const string OtherPreset = "TestSharedCooldownOtherPreset";
    private const string ZombieSourceWeights = "TestZombieSequenceSourceWeights";
    private const string ZombieNextWeights = "TestZombieSequenceNextWeights";
    private const string SecretRule = "Secret";

    [TestPrototypes]
    private const string Prototypes = $"""
    -   type: entity
        id: {Rule}
        parent: BaseGameRule
        components:
        -   type: DynamicRuleCost
            cost: 10
        -   type: DynamicRuleCooldown
            cooldown: 1

    -   type: entity
        id: {Dynamic}
        parent: BaseGameRule
        components:
        -   type: DynamicRule
            startingBudgetMin: 100
            startingBudgetMax: 101
            budgetPerSecond: 0
            variantBudgetPerSecond: 0
            minRuleInterval: 86400
            maxRuleInterval: 86401
            table: !type:EntSelector
                id: {Rule}
                conditions:
                - !type:HasBudgetCondition

    -   type: gamePreset
        id: {Preset}
        name: Test shared cooldown
        description: A preset linked only by its gamerule.
        rules:
        - {Rule}

    -   type: gamePreset
        id: {OtherPreset}
        name: Test related preset
        description: Another preset containing the same gamerule.
        rules:
        - {Rule}

    -   type: gamePreset
        id: TestSharedCooldownFallback
        name: Test fallback
        description: A preset without a rule cooldown.
        voteCooldown: 0
        rules: []

    -   type: gamePreset
        id: TestSharedCooldownBypass
        name: Test cooldown bypass
        description: Bypasses incoming cooldowns but records its Dynamic rolls.
        bypassCooldowns: true
        rules:
        - {Dynamic}

    -   type: weightedRandom
        id: TestSharedCooldownFirstWeights
        weights:
            {Preset}: 1

    -   type: weightedRandom
        id: TestSharedCooldownSecondWeights
        weights:
            {OtherPreset}: 1
            TestSharedCooldownFallback: 0.001

    -   type: weightedRandom
        id: TestSharedCooldownBypassWeights
        weights:
            TestSharedCooldownBypass: 1

    -   type: weightedRandom
        id: {ZombieSourceWeights}
        weights:
            Zombieteors: 1

    -   type: weightedRandom
        id: {ZombieNextWeights}
        weights:
            Zombie: 1
            TestSharedCooldownFallback: 0.001
    """;

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true,
    };

    [TestCase("Zombie", "Zombieteors", false)]
    [TestCase("Zombieteors", "Zombie", false)]
    [TestCase("Zombie", "Zombie", true)]
    [TestCase("Zombie", "Zombieteors", true)]
    [TestCase("Xenoborgs", "SubXenoborgs", false)]
    [TestCase("SubXenoborgs", "Xenoborgs", false)]
    [TestCase("DynamicSurvivalEventScheduler", "KesslerSyndrome", true)]
    [TestCase("DynamicKesslerSyndromeEventScheduler", "Survival", true)]
    public async Task GameruleCooldownPresetBlockAndExpireTest(string source, string target, bool sourceIsRule)
    {
        var cooldowns = Server.System<DynamicRuleCooldownSystem>();
        await Server.WaitAssertion(() =>
        {
            if (sourceIsRule)
                cooldowns.ApplyRuleCooldown(new EntProtoId(source));
            else
                cooldowns.ApplyPresetCooldown(SProtoMan.Index(new ProtoId<GamePresetPrototype>(source)));
        });

        await RestartRound();
        await Server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: false);
            Assert.That(cooldowns.TryGetPresetCooldown(new ProtoId<GamePresetPrototype>(target), out var remaining), Is.True);
            Assert.That(remaining, Is.EqualTo(1));
        });

        await RestartRound();
        await Server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: false);
            Assert.That(cooldowns.TryGetPresetCooldown(new ProtoId<GamePresetPrototype>(target), out _), Is.False);
        });
    }

    [Test]
    public async Task GameruleCooldownSecretPresetTest()
    {
        var ticker = Server.System<GameTicker>();
        var cooldowns = Server.System<DynamicRuleCooldownSystem>();
        var configuration = Server.ResolveDependency<IConfigurationManager>();
        await Server.WaitAssertion(() =>
        {
            configuration.SetCVar(CCVars.SecretWeightPrototype, "TestSharedCooldownFirstWeights");
            var secret = ticker.AddGameRule(SecretRule);
            Assert.That(Server.EntMan.GetComponent<SecretRuleComponent>(secret).AdditionalGameRules, Has.Count.EqualTo(1));
        });

        await RestartRound();
        await Server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: false);
            Assert.That(cooldowns.CurrentRuleCooldowns, Does.Contain(new EntProtoId(Rule)));
            Assert.That(cooldowns.TryGetPresetCooldown(new ProtoId<GamePresetPrototype>(Preset), out _), Is.True);
            Assert.That(cooldowns.TryGetPresetCooldown(new ProtoId<GamePresetPrototype>(OtherPreset), out _), Is.True);

            configuration.SetCVar(CCVars.SecretWeightPrototype, "TestSharedCooldownSecondWeights");
            Server.ResolveDependency<IRobustRandom>().SetSeed(1);
            var secret = ticker.AddGameRule(SecretRule);
            Assert.That(Server.EntMan.GetComponent<SecretRuleComponent>(secret).AdditionalGameRules, Is.Empty,
                "Secret must select the fallback instead of a different preset containing the cooled-down rule.");

            var dynamic = ticker.AddGameRule(Dynamic);
            Assert.That(Server.EntMan.GetComponent<DynamicRuleComponent>(dynamic).Rules, Is.Empty);
        });
    }

    // oddly specific test but it keeps happening and i want it to stop
    [Test]
    public async Task GameruleCooldownSecretZombiePresetTest()
    {
        var ticker = Server.System<GameTicker>();
        var cooldowns = Server.System<DynamicRuleCooldownSystem>();
        var configuration = Server.ResolveDependency<IConfigurationManager>();
        var random = Server.ResolveDependency<IRobustRandom>();
        var minimumPlayers = 0;

        // Consume a Zombie cooldown left by an earlier test on the pooled server.
        await RestartRound();
        await Server.WaitPost(() => cooldowns.EnsureRoundInitialized(dynamicRound: true));
        await RestartRound();

        await Server.WaitPost(() => minimumPlayers = Math.Max(
                ticker.GetMinimumPlayerCount(new ProtoId<GamePresetPrototype>("Zombie")),
                ticker.GetMinimumPlayerCount(new ProtoId<GamePresetPrototype>("Zombieteors"))));

        if (minimumPlayers > 1)
            await Server.AddDummySessions(minimumPlayers - 1);

        await Pair.RunUntilSynced();
        await Server.WaitAssertion(() =>
        {
            ticker.ToggleReadyAll(true);
            Assert.That(ticker.ReadyPlayerCount(), Is.GreaterThanOrEqualTo(minimumPlayers),
                "Not enough players.");

            configuration.SetCVar(CCVars.SecretWeightPrototype, ZombieSourceWeights);
            var secret = ticker.AddGameRule(SecretRule);
            var rules = Server.EntMan.GetComponent<SecretRuleComponent>(secret).AdditionalGameRules
                .Select(rule => Server.EntMan.GetComponent<MetaDataComponent>(rule).EntityPrototype?.ID);
            Assert.That(rules, Does.Contain("Zombie"));
            Assert.That(rules, Does.Contain("KesslerSyndromeScheduler"),
                "The first Secret selection must be Zombieteors.");
        });

        await RestartRound();
        await Server.WaitAssertion(() =>
        {
            ticker.ToggleReadyAll(true);
            Assert.That(ticker.ReadyPlayerCount(), Is.GreaterThanOrEqualTo(minimumPlayers));

            cooldowns.EnsureRoundInitialized(dynamicRound: false);
            Assert.That(cooldowns.CurrentRuleCooldowns, Does.Contain(new EntProtoId("Zombie")));
            Assert.That(cooldowns.TryGetPresetCooldown(new ProtoId<GamePresetPrototype>("Zombie"), out var remaining), Is.True);
            Assert.That(remaining, Is.EqualTo(1));

            configuration.SetCVar(CCVars.SecretWeightPrototype, ZombieNextWeights);
            random.SetSeed(1);
            var secret = ticker.AddGameRule(SecretRule);
            Assert.That(Server.EntMan.GetComponent<SecretRuleComponent>(secret).AdditionalGameRules, Is.Empty,
                "Secret must reject Zombie after Zombieteors and select the empty fallback preset.");
        });

        await RestartRound();
        await Server.WaitAssertion(() =>
        {
            ticker.ToggleReadyAll(true);
            Assert.That(ticker.ReadyPlayerCount(), Is.GreaterThanOrEqualTo(minimumPlayers));

            cooldowns.EnsureRoundInitialized(dynamicRound: false);
            Assert.That(cooldowns.TryGetPresetCooldown(new ProtoId<GamePresetPrototype>("Zombie"), out _), Is.False);

            // The same pool and seed must select Zombie once its cooldown expires.
            random.SetSeed(1);
            var secret = ticker.AddGameRule(SecretRule);
            var rules = Server.EntMan.GetComponent<SecretRuleComponent>(secret).AdditionalGameRules
                .Select(rule => Server.EntMan.GetComponent<MetaDataComponent>(rule).EntityPrototype?.ID);
            Assert.That(rules, Does.Contain("Zombie"),
                "The selection must be Zombie after its cooldown expires.");
        });

        // Consume the positive control's outgoing cooldown before returning the pooled server.
        await RestartRound();
        await Server.WaitPost(() => cooldowns.EnsureRoundInitialized(dynamicRound: true));
        await RestartRound();
    }

    [Test]
    public async Task GameruleCooldownAddedRuleTest()
    {
        var ticker = Server.System<GameTicker>();
        var cooldowns = Server.System<DynamicRuleCooldownSystem>();
        await Server.WaitAssertion(() =>
        {
            cooldowns.ApplyPresetCooldown(SProtoMan.Index(new ProtoId<GamePresetPrototype>("Dynamic")));
            ticker.AddGameRule(Rule);
            Assert.That(cooldowns.CurrentRuleCooldowns, Does.Not.Contain(new EntProtoId(Rule)),
                "A newly selected rule must not change this round's incoming cooldown snapshot.");
        });

        await RestartRound();
        await Server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: true);
            Assert.That(cooldowns.CurrentRuleCooldowns, Does.Contain(new EntProtoId(Rule)));
            Assert.That(cooldowns.TryGetPresetCooldown(new ProtoId<GamePresetPrototype>(Preset), out _), Is.True);
            Assert.That(cooldowns.TryGetPresetCooldown(new ProtoId<GamePresetPrototype>(OtherPreset), out _), Is.True);
        });
    }

    [TestCase("AllerAtOnce")]
    public async Task GameruleCooldownAllAtOnceTest(string presetId)
    {
        var cooldowns = Server.System<DynamicRuleCooldownSystem>();
        await Server.WaitPost(() => cooldowns.ApplyRuleCooldown(new EntProtoId("Nukeops")));
        await RestartRound();
        await Server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: false);
            Assert.That(cooldowns.TryGetPresetCooldown(new ProtoId<GamePresetPrototype>("Nukeops"), out _), Is.True);
            Assert.That(cooldowns.TryGetPresetCooldown(new ProtoId<GamePresetPrototype>(presetId), out _), Is.False);

            var preset = SProtoMan.Index(new ProtoId<GamePresetPrototype>(presetId));
            Assert.That(preset.BypassCooldowns, Is.True);
            cooldowns.BeginRound(preset);
            cooldowns.ApplyPresetCooldown(preset);
            Assert.That(cooldowns.CurrentRuleCooldowns, Is.Empty);
        });

        await RestartRound();
        await Server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: true);
            Assert.That(cooldowns.BypassCooldowns, Is.False);
            Assert.That(cooldowns.CurrentRuleCooldowns, Does.Contain(new EntProtoId("Nukeops")));
            Assert.That(cooldowns.CurrentRuleCooldowns, Does.Contain(new EntProtoId("DynamicKesslerSyndromeEventScheduler")));
            Assert.That(cooldowns.TryGetPresetCooldown(new ProtoId<GamePresetPrototype>("Nukeops"), out _), Is.True);
            Assert.That(cooldowns.TryGetPresetCooldown(new ProtoId<GamePresetPrototype>("WizardDuel"), out var wizardRemaining), Is.True);
            Assert.That(wizardRemaining, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task GameruleCooldownSecretBypassTest()
    {
        var ticker = Server.System<GameTicker>();
        var cooldowns = Server.System<DynamicRuleCooldownSystem>();
        var configuration = Server.ResolveDependency<IConfigurationManager>();
        await Server.WaitAssertion(() =>
        {
            configuration.SetCVar(CCVars.SecretWeightPrototype, "TestSharedCooldownBypassWeights");
            ticker.AddGameRule(SecretRule);
        });

        await RestartRound();
        await Server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: false);
            Assert.That(cooldowns.CurrentRuleCooldowns, Does.Contain(new EntProtoId(Rule)));

            // Both Secret's own preset cooldown and the shared rule cooldown are active.
            var secret = ticker.AddGameRule(SecretRule);
            var dynamic = Server.EntMan.GetComponent<SecretRuleComponent>(secret).AdditionalGameRules.Single();
            Assert.That(Server.EntMan.GetComponent<DynamicRuleComponent>(dynamic).Rules, Has.Count.EqualTo(1));
            Assert.That(cooldowns.CurrentRuleCooldowns, Is.Empty);

            cooldowns.ApplyPresetCooldown(SProtoMan.Index(new ProtoId<GamePresetPrototype>("Secret")));
            Assert.That(cooldowns.BypassCooldowns, Is.True);
        });

        await RestartRound();
        await Server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: true);
            Assert.That(cooldowns.CurrentRuleCooldowns, Does.Contain(new EntProtoId(Rule)));
        });
    }

    private async Task RestartRound()
    {
        await Server.WaitPost(() => Server.System<GameTicker>().RestartRound());
        await Pair.RunUntilSynced();
    }
}
