using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests._Starlight;
using Content.Server._Starlight.Salvage.Ruins;
using Content.Shared.CCVar;
using Content.Shared.Friction;
using Content.Shared.Shuttles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests._Starlight.Salvage;

/// <summary>
/// Verifies that generated wreck debris retains moving-grid physics and can damage a station on impact.
/// </summary>
[TestFixture]
[TestOf(typeof(RuinGeneratorSystem))]
[EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.ImpactEnabled), true)]
public sealed class RuinWreckImpactTest : GameTest
{
    #region Tests

    [Test]
    public async Task MovingRuinWreckDamagesStaticStation()
    {
        const int sideLength = 10;

        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entityManager = server.EntMan;
        var mapSystem = entityManager.System<SharedMapSystem>();
        var physicsSystem = entityManager.System<SharedPhysicsSystem>();
        var ruinGenerator = entityManager.System<RuinGeneratorSystem>();
        var transformSystem = entityManager.System<SharedTransformSystem>();

        var stationGrid = map.Grid;
        var stationTile = map.Tile.Tile;
        var initialStationTileCount = 0;
        EntityUid wreckGrid = default;

        await server.WaitAssertion(() =>
        {
            mapSystem.SetTiles(stationGrid.Owner, stationGrid.Comp, RuinTestHelpers.MakeSquareTiles(sideLength, stationTile));
            initialStationTileCount = mapSystem.GetAllTiles(stationGrid.Owner, stationGrid.Comp).Count();

            Assert.That(entityManager.HasComponent<ShuttleComponent>(stationGrid.Owner), Is.True,
                "The static station needs ShuttleComponent so its collision invokes shuttle impact damage.");

            var stationPhysics = entityManager.GetComponent<PhysicsComponent>(stationGrid.Owner);
            physicsSystem.SetBodyType(stationGrid.Owner, BodyType.Static, body: stationPhysics);

            var ruin = new RuinGeneratorSystem.RuinResult
            {
                FloorTiles = RuinTestHelpers.MakeSquareTiles(sideLength, stationTile),
                Bounds = new Box2(0f, 0f, sideLength, sideLength),
            };

            var spawnedWreck = ruinGenerator.SpawnRuinGrid(map.MapId, ruin, seed: 1);
            Assert.That(spawnedWreck, Is.Not.Null);
            wreckGrid = spawnedWreck!.Value;

            var wreckPhysics = entityManager.GetComponent<PhysicsComponent>(wreckGrid);
            Assert.Multiple(() =>
            {
                Assert.That(entityManager.HasComponent<ShuttleComponent>(wreckGrid), Is.False);
                Assert.That(entityManager.GetComponent<TileFrictionModifierComponent>(wreckGrid).Modifier, Is.EqualTo(0.50f));
                Assert.That(wreckPhysics.BodyType, Is.EqualTo(BodyType.Dynamic));
                Assert.That(stationPhysics.BodyType, Is.EqualTo(BodyType.Static));
            });

            // Leave a short gap so the test observes a real physics collision rather than an overlap at spawn.
            transformSystem.SetWorldPosition(wreckGrid, new Vector2(-sideLength - 0.05f, 0f));
            physicsSystem.SetLinearVelocity(wreckGrid, new Vector2(50f, 0f), body: wreckPhysics);
        });

        await server.WaitRunTicks(120);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.EntityExists(stationGrid.Owner), Is.True,
                "The station grid was deleted; tile loss cannot be attributed to impact damage.");

            var remainingTiles = mapSystem.GetAllTiles(stationGrid.Owner, stationGrid.Comp).Count();
            var wreckPosition = transformSystem.GetWorldPosition(wreckGrid);
            var wreckVelocity = entityManager.GetComponent<PhysicsComponent>(wreckGrid).LinearVelocity;

            Assert.That(remainingTiles, Is.LessThan(initialStationTileCount),
                $"The wreck did not damage the station. Final wreck position: {wreckPosition}; " +
                $"velocity: {wreckVelocity}; station tiles: {remainingTiles}/{initialStationTileCount}.");
        });
    }

    #endregion
}

