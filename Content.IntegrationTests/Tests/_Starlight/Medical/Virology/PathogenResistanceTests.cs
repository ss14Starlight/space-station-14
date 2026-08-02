using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Starlight.Medical.Virology;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

[TestFixture]
public sealed class PathogenProtectionMathTests
{
    [Test]
    public void VirusUsesInternalsHoodsAndFiltersOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                Protection(PathogenType.Virus, false, Classes(PathogenProtectionClass.FilterMask)),
                Is.EqualTo(0.90f).Within(0.0001f));
            Assert.That(
                Protection(PathogenType.Virus, false, Classes(PathogenProtectionClass.SupplyMask)),
                Is.Zero);
            Assert.That(
                Protection(PathogenType.Virus, false, Classes(PathogenProtectionClass.SealedSuit)),
                Is.Zero);
            Assert.That(
                Protection(PathogenType.Virus, true, Classes(PathogenProtectionClass.SupplyMask)),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                Protection(PathogenType.Virus, false, Classes(PathogenProtectionClass.BioHood)),
                Is.EqualTo(1f).Within(0.0001f));
        });
    }

    [Test]
    public void BacteriaUsesCleanBarriersOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                Protection(PathogenType.Bacteria, false, Classes(PathogenProtectionClass.SterileBarrier)),
                Is.EqualTo(0.90f).Within(0.0001f));
            Assert.That(
                Protection(PathogenType.Bacteria, false, Classes(PathogenProtectionClass.SealedSuit)),
                Is.Zero);
            Assert.That(
                Protection(PathogenType.Bacteria, false, Classes(PathogenProtectionClass.BioSuit)),
                Is.EqualTo(1f).Within(0.0001f));
        });
    }

    [Test]
    public void FungusMatchesExpectedOutfitMatrix()
    {
        var fullClothing = PathogenProtectionMath.FungalSlotProtection(
            uniform: true,
            outerClothing: true,
            shoes: true,
            gloves: true,
            head: true,
            eyes: true);

        Assert.Multiple(() =>
        {
            Assert.That(fullClothing, Is.EqualTo(0.40f).Within(0.0001f));
            Assert.That(
                Protection(
                    PathogenType.Fungus,
                    false,
                    Classes(PathogenProtectionClass.BioSuit, PathogenProtectionClass.BioHood)),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                Protection(PathogenType.Fungus, true, Classes(PathogenProtectionClass.SealedSuit)),
                Is.EqualTo(0.95f).Within(0.0001f));
            Assert.That(
                Protection(
                    PathogenType.Fungus,
                    false,
                    Classes(PathogenProtectionClass.SealedSuit, PathogenProtectionClass.FilterMask)),
                Is.EqualTo(0.85f).Within(0.0001f));
            Assert.That(
                Protection(
                    PathogenType.Fungus,
                    false,
                    Classes(PathogenProtectionClass.FilterMask),
                    fullClothing),
                Is.EqualTo(0.80f).Within(0.0001f));
            Assert.That(
                Protection(PathogenType.Fungus, false, Classes(), fullClothing),
                Is.EqualTo(0.40f).Within(0.0001f));
            Assert.That(
                Protection(PathogenType.Fungus, false, Classes()),
                Is.Zero);
        });
    }

    [Test]
    public void BypassErodesOnlyPartialProtection()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                PathogenProtectionMath.ApplyBypass(1f, 0.5f),
                Is.Zero);
            Assert.That(
                PathogenProtectionMath.ApplyBypass(0.90f, 0f),
                Is.EqualTo(0.10f).Within(0.0001f));
            Assert.That(
                PathogenProtectionMath.ApplyBypass(0.90f, 0.5f),
                Is.EqualTo(0.55f).Within(0.0001f));
            Assert.That(
                PathogenProtectionMath.ApplyBypass(0f, 0.5f),
                Is.EqualTo(1f).Within(0.0001f));
        });
    }

    private static float Protection(
        PathogenType type,
        bool internals,
        IReadOnlySet<PathogenProtectionClass> classes,
        float fungalSlotProtection = 0f)
    {
        return PathogenProtectionMath.CalculateProtection(
            type,
            internals,
            classes,
            fungalSlotProtection);
    }

    private static HashSet<PathogenProtectionClass> Classes(
        params PathogenProtectionClass[] classes)
        => [.. classes];
}

[TestFixture]
public sealed class PathogenProtectionPrototypeTests : GameTest
{
    [Test]
    public async Task ProtectivePrototypesHaveExpectedClasses()
    {
        var server = Pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        (string Id, PathogenProtectionClass Class)[] expected =
        [
            ("ClothingMaskGas", PathogenProtectionClass.FilterMask),
            ("ClothingMaskGasCaptain", PathogenProtectionClass.FilterMask),
            ("ClothingMaskSterile", PathogenProtectionClass.FilterMask),
            ("ClothingMaskBreath", PathogenProtectionClass.SupplyMask),
            ("ClothingMaskBreathMedical", PathogenProtectionClass.SupplyMask),
            ("ClothingHeadHelmetHardsuitEngineering", PathogenProtectionClass.SupplyMask),
            ("ClothingHeadHelmetEVA", PathogenProtectionClass.SupplyMask),
            ("ClothingHandsGlovesLatex", PathogenProtectionClass.SterileBarrier),
            ("ClothingHandsGlovesNitrile", PathogenProtectionClass.SterileBarrier),
            ("ClothingOuterBioGeneral", PathogenProtectionClass.BioSuit),
            ("ClothingOuterBioSecurity", PathogenProtectionClass.BioSuit),
            ("ClothingHeadHatHoodBioGeneral", PathogenProtectionClass.BioHood),
            ("ClothingOuterHardsuitEngineering", PathogenProtectionClass.SealedSuit),
            ("ClothingOuterHardsuitEVA", PathogenProtectionClass.SealedSuit),
        ];

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var (id, expectedClass) in expected)
                {
                    var prototype = prototypes.Index<EntityPrototype>(id);
                    Assert.That(
                        prototype.TryGetComponent<PathogenResistanceComponent>(
                            out var component,
                            server.EntMan.ComponentFactory),
                        Is.True,
                        $"{id} should have pathogen protection");
                    Assert.That(
                        component.Class,
                        Is.EqualTo(expectedClass),
                        $"{id} should be classified as {expectedClass}");
                }
            });
        });
    }

    [Test]
    public async Task FungalArchetypesHaveNoPersonToPersonTransmission()
    {
        var server = Pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var id in new[] { "SporeBloom", "RedFlux" })
                {
                    var archetype = prototypes.Index<PathogenArchetypePrototype>(id);
                    Assert.That(archetype.PathogenType, Is.EqualTo(PathogenType.Fungus));
                    Assert.That(archetype.MinTransmissibility, Is.Zero);
                    Assert.That(archetype.MaxTransmissibility, Is.Zero);
                }
            });
        });
    }
}
