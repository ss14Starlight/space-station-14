using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Starlight.Humanoid;

[TestFixture]
public sealed class PetBedBuckleOffsetTest : GameTest
{
    private static readonly string[] NonPetBedPrototypes =
    [
        "Bed",
        "MedicalBed",
        "Mattress",
        "WebBed",
        "PsychBed",
        "OperatingTable",
        "StasisBed",
        "RollerBed",
        "CheapRollerBed",
        "EmergencyRollerBed",
        "StasisBedMedTak",
        "MedTakHoverBed",
    ];

    [Test]
    public async Task BedVariantsApplyConfiguredBuckleOffsets()
    {
        var pair = Pair;
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var coordinates = testMap.GridCoords;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var buckleSystem = entityManager.System<SharedBuckleSystem>();
        EntityUid regularBedHuman = default;
        EntityUid dogBedHuman = default;
        EntityUid felionoid = default;
        EntityUid raccoonBedHuman = default;
        EntityUid softPetBedHuman = default;
        EntityUid regularBed = default;
        EntityUid dogBed = default;
        EntityUid catBed = default;
        EntityUid raccoonBed = default;
        EntityUid softPetBed = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                regularBedHuman = entityManager.SpawnEntity("MobHuman", coordinates);
                dogBedHuman = entityManager.SpawnEntity("MobHuman", coordinates);
                felionoid = entityManager.SpawnEntity("MobFelionoid", coordinates);
                raccoonBedHuman = entityManager.SpawnEntity("MobHuman", coordinates);
                softPetBedHuman = entityManager.SpawnEntity("MobHuman", coordinates);
                regularBed = entityManager.SpawnEntity("Bed", coordinates);
                dogBed = entityManager.SpawnEntity("DogBed", coordinates);
                catBed = entityManager.SpawnEntity("CatBed", coordinates);
                raccoonBed = entityManager.SpawnEntity("RaccoonBed", coordinates);
                softPetBed = entityManager.SpawnEntity("SoftPetBedBlue", coordinates);

                Assert.That(buckleSystem.TryBuckle(regularBedHuman, regularBedHuman, regularBed), Is.True);
                Assert.That(buckleSystem.TryBuckle(dogBedHuman, dogBedHuman, dogBed), Is.True);
                Assert.That(buckleSystem.TryBuckle(felionoid, felionoid, catBed), Is.True);
                Assert.That(buckleSystem.TryBuckle(raccoonBedHuman, raccoonBedHuman, raccoonBed), Is.True);
                Assert.That(buckleSystem.TryBuckle(softPetBedHuman, softPetBedHuman, softPetBed), Is.True);

                var regularBedOffset = new Vector2(0, -0.2f);
                Assert.That(entityManager.GetComponent<StrapComponent>(regularBed).BuckleOffset,
                    Is.EqualTo(regularBedOffset));
                Assert.That(entityManager.GetComponent<TransformComponent>(regularBedHuman).LocalPosition,
                    Is.EqualTo(regularBedOffset));

                var petBedOffset = new Vector2(0, -0.15f);
                foreach (var bed in new[] { dogBed, catBed, raccoonBed, softPetBed })
                {
                    Assert.That(entityManager.GetComponent<StrapComponent>(bed).BuckleOffset,
                        Is.EqualTo(petBedOffset));
                }

                foreach (var occupant in new[] { dogBedHuman, felionoid, raccoonBedHuman, softPetBedHuman })
                {
                    Assert.That(entityManager.GetComponent<TransformComponent>(occupant).LocalPosition,
                        Is.EqualTo(petBedOffset));
                }
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (regularBedHuman.IsValid())
                    entityManager.DeleteEntity(regularBedHuman);
                if (dogBedHuman.IsValid())
                    entityManager.DeleteEntity(dogBedHuman);
                if (felionoid.IsValid())
                    entityManager.DeleteEntity(felionoid);
                if (raccoonBedHuman.IsValid())
                    entityManager.DeleteEntity(raccoonBedHuman);
                if (softPetBedHuman.IsValid())
                    entityManager.DeleteEntity(softPetBedHuman);
                if (regularBed.IsValid())
                    entityManager.DeleteEntity(regularBed);
                if (dogBed.IsValid())
                    entityManager.DeleteEntity(dogBed);
                if (catBed.IsValid())
                    entityManager.DeleteEntity(catBed);
                if (raccoonBed.IsValid())
                    entityManager.DeleteEntity(raccoonBed);
                if (softPetBed.IsValid())
                    entityManager.DeleteEntity(softPetBed);
            });
            await pair.RunUntilSynced();
        }
    }

    [Test]
    public async Task NonPetBedVariantsMatchStandardBedAlignment()
    {
        var pair = Pair;
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var beds = new EntityUid[NonPetBedPrototypes.Length];
        var expectedOffset = new Vector2(0, -0.2f);

        try
        {
            await server.WaitAssertion(() =>
            {
                for (var i = 0; i < NonPetBedPrototypes.Length; i++)
                {
                    beds[i] = entityManager.SpawnEntity(NonPetBedPrototypes[i], MapCoordinates.Nullspace);
                    Assert.That(
                        entityManager.GetComponent<StrapComponent>(beds[i]).BuckleOffset,
                        Is.EqualTo(expectedOffset),
                        NonPetBedPrototypes[i]);
                }
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                foreach (var bed in beds)
                {
                    if (bed.IsValid())
                        entityManager.DeleteEntity(bed);
                }
            });
            await pair.RunUntilSynced();
        }
    }
}
