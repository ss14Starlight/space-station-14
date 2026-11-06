using Content.IntegrationTests.Fixtures;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Rotation;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Starlight.Humanoid;

[TestFixture]
public sealed class PetBedSleepPoseTest : GameTest
{
    private static readonly string[] PetBedPrototypes =
    [
        "DogBed",
        "CatBed",
        "RaccoonBed",
        "SoftPetBedBlue",
    ];

    [Test]
    public async Task BuckledHumanoidsRotateSidewaysOnlyWhileSleeping()
    {
        var pair = Pair;
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var coordinates = testMap.GridCoords;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var buckleSystem = entityManager.System<SharedBuckleSystem>();
        var sleepingSystem = entityManager.System<SleepingSystem>();
        var beds = new EntityUid[PetBedPrototypes.Length];
        var occupants = new EntityUid[PetBedPrototypes.Length];

        try
        {
            await server.WaitAssertion(() =>
            {
                for (var i = 0; i < PetBedPrototypes.Length; i++)
                {
                    beds[i] = entityManager.SpawnEntity(PetBedPrototypes[i], coordinates);
                    occupants[i] = entityManager.SpawnEntity(
                        i == 0 ? "MobFelionoid" : "MobHuman",
                        coordinates);

                    Assert.That(buckleSystem.TryBuckle(occupants[i], occupants[i], beds[i]), Is.True);
                }
            });

            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                for (var i = 0; i < PetBedPrototypes.Length; i++)
                {
                    var rotation = entityManager.GetComponent<RotationVisualsComponent>(occupants[i]);
                    var strap = entityManager.GetComponent<StrapComponent>(beds[i]);

                    Assert.That(rotation.HorizontalRotation, Is.EqualTo(strap.Rotation));
                    Assert.That(sleepingSystem.TrySleeping(occupants[i]), Is.True);
                    Assert.That(rotation.HorizontalRotation, Is.EqualTo(rotation.DefaultRotation));
                    Assert.That(entityManager.GetComponent<StandingStateComponent>(occupants[i]).Standing, Is.False);

                    entityManager.RemoveComponent<SleepingComponent>(occupants[i]);
                    Assert.That(rotation.HorizontalRotation, Is.EqualTo(strap.Rotation));
                }
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                foreach (var occupant in occupants)
                {
                    if (occupant.IsValid())
                        entityManager.DeleteEntity(occupant);
                }

                foreach (var bed in beds)
                {
                    if (bed.IsValid())
                        entityManager.DeleteEntity(bed);
                }
            });
            await pair.RunUntilSynced();
        }
    }

    [Test]
    public async Task AlreadySleepingHumanoidRotatesWhenBuckledToPetBed()
    {
        var pair = Pair;
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var coordinates = testMap.GridCoords;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var buckleSystem = entityManager.System<SharedBuckleSystem>();
        var sleepingSystem = entityManager.System<SleepingSystem>();
        EntityUid occupant = default;
        EntityUid bed = default;

        try
        {
            await server.WaitPost(() =>
            {
                occupant = entityManager.SpawnEntity("MobHuman", coordinates);
                bed = entityManager.SpawnEntity("DogBed", coordinates);
            });
            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                Assert.That(sleepingSystem.TrySleeping(occupant), Is.True);
                Assert.That(buckleSystem.TryBuckle(occupant, occupant, bed), Is.True);

                var rotation = entityManager.GetComponent<RotationVisualsComponent>(occupant);
                Assert.That(rotation.HorizontalRotation, Is.EqualTo(rotation.DefaultRotation));
                Assert.That(entityManager.GetComponent<StandingStateComponent>(occupant).Standing, Is.False);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (occupant.IsValid())
                    entityManager.DeleteEntity(occupant);
                if (bed.IsValid())
                    entityManager.DeleteEntity(bed);
            });
            await pair.RunUntilSynced();
        }
    }
}
