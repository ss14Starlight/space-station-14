using System.Collections.Generic;
using Content.Server._Sol.Medical.Allergy;
using Content.Shared.Chemistry.Components;
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
            Assert.That(damageable!.Damage.DamageDict["Poison"].Float(), Is.GreaterThanOrEqualTo(2f));
            Assert.That(damageable.Damage.DamageDict["Asphyxiation"].Float(), Is.GreaterThanOrEqualTo(1f));
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
        });

        await pair.CleanReturnAsync();
    }
}
