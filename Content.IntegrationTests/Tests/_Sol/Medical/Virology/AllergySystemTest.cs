using System.Collections.Generic;
using Content.Server._Sol.Medical.Allergy;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared._CD.Records;
using Content.Shared._Sol.Medical.Allergy;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sol.Medical.Virology;

[TestFixture]
public sealed class AllergySystemTest
{
    [Test]
    public async Task FreeTextMapsToMechanicalAllergy()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.Spawn("MobHuman");
            entMan.System<AllergySystem>().ApplyFromFreeText(human, "Peanut", "None");
            Assert.That(entMan.TryGetComponent(human, out AllergyComponent allergy), Is.True);
            Assert.That(allergy!.Allergies.Contains("SolAllergyPeanut"), Is.True);
            Assert.That(allergy.Severities["SolAllergyPeanut"], Is.EqualTo(AllergySeverity.Severe));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StructuredPreferencesApplyWithIntensity()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.Spawn("MobHuman");
            var preferences = new List<CharacterAllergyPreference>
            {
                new("SolAllergyLatex", AllergySeverity.Anaphylaxis),
            };

            entMan.System<AllergySystem>().ApplyFromPreferences(human, preferences);
            Assert.That(entMan.TryGetComponent(human, out AllergyComponent allergy), Is.True);
            Assert.That(allergy!.Allergies.Contains("SolAllergyLatex"), Is.True);
            Assert.That(allergy.Severities["SolAllergyLatex"], Is.EqualTo(AllergySeverity.Anaphylaxis));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InnateSpeciesAllergiesApply()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var mob = entMan.Spawn("MobHuman");
            entMan.System<AllergySystem>().ApplyInnateSpeciesAllergies(mob, "Avali");

            Assert.That(entMan.TryGetComponent(mob, out AllergyComponent allergy), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(allergy!.Allergies, Does.Contain("SolAllergySaline"));
                Assert.That(allergy.Allergies, Does.Contain("SolAllergyDexalin"));
                Assert.That(allergy.Allergies, Does.Contain("SolAllergyDexalinPlus"));
                Assert.That(allergy.InnateAllergies, Does.Contain("SolAllergySaline"));
                Assert.That(allergy.Allergies, Does.Not.Contain("SolAllergyAmoxla"));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ProfileAllergiesDoNotWriteRecordFreeText()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var defaults = PlayerProvidedCharacterRecords.DefaultRecords();
            var profile = HumanoidCharacterProfile.DefaultWithSpecies()
                .WithSolAllergies(
                [
                    new CharacterAllergyPreference("SolAllergyPeanut", AllergySeverity.Severe),
                    new CharacterAllergyPreference("SolAllergyLatex", AllergySeverity.Mild),
                ]);

            Assert.That(profile.SolAllergies, Has.Count.EqualTo(2));
            Assert.That(profile.CDCharacterRecords?.Allergies, Is.EqualTo(defaults.Allergies));
            Assert.That(profile.CDCharacterRecords?.DrugAllergies, Is.EqualTo(defaults.DrugAllergies));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FoodAllergensMatchReagentsAndPrototypeFamilies()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var allergy = entMan.System<AllergySystem>();
            var dairy = proto.Index<AllergyPrototype>("SolAllergyDairy");
            var soy = proto.Index<AllergyPrototype>("SolAllergySoy");
            var wheat = proto.Index<AllergyPrototype>("SolAllergyWheat");
            var amoxla = proto.Index<AllergyPrototype>("SolAllergyAmoxla");

            Assert.That(allergy.FoodMatchesAllergy("FoodVanillaIceCream", new Solution("Milk", 1), dairy), Is.True);
            Assert.That(allergy.FoodMatchesAllergy("FoodVanillaIceCream", new Solution("MilkOat", 1), dairy), Is.True,
                "Ice cream is explicitly a dairy prototype family even if the sampled bite lacks Milk");
            Assert.That(allergy.FoodMatchesAllergy("FoodSoybeans", new Solution("MilkSoy", 1), soy), Is.True);
            Assert.That(allergy.FoodMatchesAllergy("FoodSoybeans", new Solution("MilkSoy", 1), dairy), Is.False);
            Assert.That(allergy.FoodMatchesAllergy("FoodBreadPlain", null, wheat), Is.True);
            Assert.That(allergy.FoodMatchesAllergy("FoodBreadPlain", new Solution("Amoxla", 1), amoxla), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SyntheticBodiesCannotReceiveAllergies()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var allergy = entMan.System<AllergySystem>();
            var ipc = entMan.Spawn("MobIPC");
            var borg = entMan.Spawn("BorgChassisMedical");
            var preferences = new List<CharacterAllergyPreference>
            {
                new("SolAllergyPeanut", AllergySeverity.Severe),
            };

            foreach (var synthetic in new[] { ipc, borg })
            {
                Assert.That(allergy.CanHaveAllergies(synthetic), Is.False);
                allergy.ApplyFromPreferences(synthetic, preferences);
                allergy.ApplyFromFreeText(synthetic, "Peanut", "None");
                Assert.That(entMan.HasComponent<AllergyComponent>(synthetic), Is.False);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SevereWheatAllergyAppliesAsphyxiationDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var timing = server.ResolveDependency<Robust.Shared.Timing.IGameTiming>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.Spawn("MobHuman");
            var allergySys = entMan.System<AllergySystem>();
            allergySys.ApplyFromPreferences(human,
            [
                new CharacterAllergyPreference("SolAllergyWheat", AllergySeverity.Severe),
            ]);

            Assert.That(entMan.TryGetComponent(human, out AllergyComponent? allergy), Is.True);
            var wheat = proto.Index<AllergyPrototype>("SolAllergyWheat");
            allergySys.TriggerAllergy(human, allergy!, wheat);

            Assert.That(entMan.TryGetComponent(human, out DamageableComponent? damageable), Is.True);
            // First tick applies poison immediately; asphyxiation waits for AirlossStartsAt.
            Assert.That(damageable!.Damage.DamageDict["Poison"].Float(), Is.GreaterThan(0f));
            Assert.That(damageable.Damage.DamageDict.GetValueOrDefault("Asphyxiation").Float(), Is.EqualTo(0f));
            Assert.That(entMan.HasComponent<ActiveAllergyReactionComponent>(human), Is.True);
            Assert.That(allergySys.IsHavingSevereReaction(human), Is.True);

            var reaction = entMan.GetComponent<ActiveAllergyReactionComponent>(human);
            reaction.AirlossStartsAt = timing.CurTime;
            entMan.Dirty(human, reaction);
            allergySys.TriggerAllergy(human, allergy!, wheat);

            damageable = entMan.GetComponent<DamageableComponent>(human);
            Assert.That(damageable.Damage.DamageDict["Asphyxiation"].Float(), Is.GreaterThan(0f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AnaphylaxisBlocksAsphyxiationHealing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var timing = server.ResolveDependency<Robust.Shared.Timing.IGameTiming>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.Spawn("MobHuman");
            var allergySys = entMan.System<AllergySystem>();
            var damageableSys = entMan.System<Content.Shared.Damage.Systems.DamageableSystem>();
            allergySys.ApplyFromPreferences(human,
            [
                new CharacterAllergyPreference("SolAllergyWheat", AllergySeverity.Anaphylaxis),
            ]);

            var wheat = proto.Index<AllergyPrototype>("SolAllergyWheat");
            var allergy = entMan.GetComponent<AllergyComponent>(human);
            allergySys.TriggerAllergy(human, allergy, wheat);

            // Skip the airloss onset delay so asphyxiation damage lands for this assertion.
            var reaction = entMan.GetComponent<ActiveAllergyReactionComponent>(human);
            reaction.AirlossStartsAt = timing.CurTime;
            entMan.Dirty(human, reaction);
            allergySys.TriggerAllergy(human, allergy, wheat);

            var before = entMan.GetComponent<DamageableComponent>(human).Damage.DamageDict["Asphyxiation"].Float();
            Assert.That(before, Is.GreaterThan(0f));

            // Respirator-style asphyx healing must not land during a severe reaction.
            damageableSys.TryChangeDamage(human, new DamageSpecifier
            {
                DamageDict = { ["Asphyxiation"] = -20 },
            }, interruptsDoAfters: false);

            var after = entMan.GetComponent<DamageableComponent>(human).Damage.DamageDict["Asphyxiation"].Float();
            Assert.That(after, Is.EqualTo(before));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MildWheatAllergyDoesNotApplyAsphyxiationDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.Spawn("MobHuman");
            var allergySys = entMan.System<AllergySystem>();
            allergySys.ApplyFromPreferences(human,
            [
                new CharacterAllergyPreference("SolAllergyWheat", AllergySeverity.Mild),
            ]);

            var wheat = proto.Index<AllergyPrototype>("SolAllergyWheat");
            allergySys.TriggerAllergy(human, entMan.GetComponent<AllergyComponent>(human), wheat);

            var damageable = entMan.GetComponent<DamageableComponent>(human);
            Assert.That(damageable.Damage.DamageDict["Poison"].Float(), Is.GreaterThan(0f));
            Assert.That(damageable.Damage.DamageDict.GetValueOrDefault("Asphyxiation").Float(), Is.EqualTo(0f));
            Assert.That(entMan.HasComponent<ActiveAllergyReactionComponent>(human), Is.True);
            Assert.That(
                entMan.GetComponent<ActiveAllergyReactionComponent>(human).EndsAt,
                Is.LessThanOrEqualTo(server.ResolveDependency<Robust.Shared.Timing.IGameTiming>().CurTime + TimeSpan.FromSeconds(30.01)));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AnaphylaxisMutesAndCapsRemainingDuration()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var timing = server.ResolveDependency<Robust.Shared.Timing.IGameTiming>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.Spawn("MobHuman");
            var allergySys = entMan.System<AllergySystem>();
            allergySys.ApplyFromPreferences(human,
            [
                new CharacterAllergyPreference("SolAllergyWheat", AllergySeverity.Anaphylaxis),
            ]);

            var wheat = proto.Index<AllergyPrototype>("SolAllergyWheat");
            var allergy = entMan.GetComponent<AllergyComponent>(human);
            allergySys.TriggerAllergy(human, allergy, wheat, exposureUnits: 1f, delayedOnset: false);

            Assert.That(entMan.HasComponent<Content.Shared.Speech.Muting.MutedComponent>(human), Is.True);
            var reaction = entMan.GetComponent<ActiveAllergyReactionComponent>(human);

            // Spam exposure while already at/near max remaining — must not exceed now + 100s.
            for (var i = 0; i < 20; i++)
                allergySys.TriggerAllergy(human, allergy, wheat, exposureUnits: 5f, delayedOnset: false);

            reaction = entMan.GetComponent<ActiveAllergyReactionComponent>(human);
            Assert.That(reaction.EndsAt, Is.LessThanOrEqualTo(timing.CurTime + TimeSpan.FromSeconds(100.01)));

            // Simulate time passing so remaining budget frees up, then re-expose.
            reaction.EndsAt = timing.CurTime + TimeSpan.FromSeconds(10);
            entMan.Dirty(human, reaction);
            var beforeRebuild = reaction.EndsAt;
            allergySys.TriggerAllergy(human, allergy, wheat, exposureUnits: 3f, delayedOnset: false);
            reaction = entMan.GetComponent<ActiveAllergyReactionComponent>(human);
            Assert.That(reaction.EndsAt, Is.GreaterThan(beforeRebuild));
            Assert.That(reaction.EndsAt, Is.LessThanOrEqualTo(timing.CurTime + TimeSpan.FromSeconds(100.01)));
            Assert.That(reaction.Intensity, Is.GreaterThan(1f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IngestedAllergyDelaysDamageOnset()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var timing = server.ResolveDependency<Robust.Shared.Timing.IGameTiming>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.Spawn("MobHuman");
            var allergySys = entMan.System<AllergySystem>();
            allergySys.ApplyFromPreferences(human,
            [
                new CharacterAllergyPreference("SolAllergyWheat", AllergySeverity.Anaphylaxis),
            ]);

            var wheat = proto.Index<AllergyPrototype>("SolAllergyWheat");
            allergySys.TriggerAllergy(
                human,
                entMan.GetComponent<AllergyComponent>(human),
                wheat,
                exposureUnits: 2f,
                delayedOnset: true);

            var reaction = entMan.GetComponent<ActiveAllergyReactionComponent>(human);
            Assert.That(reaction.DamageStartsAt, Is.GreaterThan(timing.CurTime));
            var asphyx = entMan.GetComponent<DamageableComponent>(human).Damage.DamageDict
                .GetValueOrDefault("Asphyxiation").Float();
            Assert.That(asphyx, Is.EqualTo(0f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShortenAllergyReactionClearsActiveReaction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.Spawn("MobHuman");
            var allergySys = entMan.System<AllergySystem>();
            allergySys.ApplyFromPreferences(human,
            [
                new CharacterAllergyPreference("SolAllergyWheat", AllergySeverity.Severe),
            ]);

            var wheat = proto.Index<AllergyPrototype>("SolAllergyWheat");
            allergySys.TriggerAllergy(human, entMan.GetComponent<AllergyComponent>(human), wheat);
            Assert.That(entMan.HasComponent<ActiveAllergyReactionComponent>(human), Is.True);

            var timing = server.ResolveDependency<Robust.Shared.Timing.IGameTiming>();
            var reaction = entMan.GetComponent<ActiveAllergyReactionComponent>(human);
            reaction.EndsAt = timing.CurTime + TimeSpan.FromSeconds(5);
            entMan.Dirty(human, reaction);

            reaction.EndsAt -= TimeSpan.FromSeconds(12);
            entMan.Dirty(human, reaction);
            if (reaction.EndsAt <= timing.CurTime)
                entMan.RemoveComponent<ActiveAllergyReactionComponent>(human);

            Assert.That(entMan.HasComponent<ActiveAllergyReactionComponent>(human), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
