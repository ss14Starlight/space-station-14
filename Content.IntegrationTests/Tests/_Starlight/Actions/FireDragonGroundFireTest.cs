#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.FireDragon;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Damage.Systems;
using Content.Shared.NPC;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Starlight.Actions;

[TestFixture]
public sealed class FireDragonGroundFireTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Fresh = true, Dirty = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task ContactDamageRespectsWalls(bool blocked)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            PrepareFloor(map.Grid);
            var prey = SEntMan.SpawnAttachedTo("MobHuman", new EntityCoordinates(map.Grid, 2.5f, 0.5f));
            if (blocked)
                SEntMan.SpawnAttachedTo("WallSolid", new EntityCoordinates(map.Grid, 1.5f, 0.5f));

            var fire = SEntMan.SpawnAttachedTo("FireDragonGroundFire", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            var component = SEntMan.GetComponent<DragonGroundFireComponent>(fire);
            component.ContactRange = 3f;
            component.FireStacks = 0f;

            Server.System<DragonFireSystem>().Update(0f);
            Assert.That(HeatDamage(prey), Is.EqualTo(blocked ? 0f : 8f));
        });
    }

    [Test]
    public async Task GroundFireKeepsAlliesSafeAfterDragonDeletion()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            PrepareFloor(map.Grid);
            var dragon = SEntMan.SpawnAttachedTo("MobFireDragon", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            SEntMan.RemoveComponent<HTNComponent>(dragon);
            SEntMan.RemoveComponent<ActiveNPCComponent>(dragon);
            var coordinates = new EntityCoordinates(map.Grid, 1.5f, 0.5f);
            var carp = SEntMan.SpawnAttachedTo("MobCarpDungeon", coordinates);
            var prey = SEntMan.SpawnAttachedTo("MobHuman", coordinates);
            SpawnRingWave(map.Grid, dragon);

            var fireSystem = Server.System<DragonFireSystem>();
            fireSystem.Update(0f);
            var fire = FindFire(coordinates);
            SEntMan.DeleteEntity(dragon);

            Assert.That(Server.System<NpcFactionSystem>().IsEntityFriendly(fire, carp), Is.True);
            fireSystem.Update(0f);
            Assert.That(HeatDamage(carp), Is.Zero);
            Assert.That(HeatDamage(prey), Is.GreaterThan(0f));
        });
    }

    [Test]
    public async Task WaveReplacesFireQueuedForDeletion()
    {
        var map = await Pair.CreateTestMap();
        var coordinates = new EntityCoordinates(map.Grid, 1.5f, 0.5f);
        EntityUid oldFire = default;
        EntityUid replacement = default;
        await Server.WaitAssertion(() =>
        {
            PrepareFloor(map.Grid);
            oldFire = SEntMan.SpawnAttachedTo("FireDragonGroundFire", coordinates);
            Assert.That(Server.System<EntityLookupSystem>().GetEntitiesInRange(coordinates, 0.1f), Does.Contain(oldFire));
            SEntMan.QueueDeleteEntity(oldFire);
            SpawnRingWave(map.Grid);
            Server.System<DragonFireSystem>().Update(0f);
            replacement = FindFire(coordinates);
            Assert.That(replacement, Is.Not.EqualTo(oldFire));
        });

        await Server.WaitRunTicks(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(oldFire), Is.True);
            Assert.That(SEntMan.Deleted(replacement), Is.False);
            Assert.That(FindFire(coordinates), Is.EqualTo(replacement));
        });
    }

    private void SpawnRingWave(EntityUid grid, EntityUid? source = null)
    {
        var uid = SEntMan.SpawnAttachedTo(null, new EntityCoordinates(grid, 0.5f, 0.5f));
        var wave = SEntMan.AddComponent<DragonFireWaveComponent>(uid);
        wave.Grid = grid;
        wave.Source = source ?? EntityUid.Invalid;
        wave.Rings = true;
        wave.Length = 1;
    }

    private void PrepareFloor(EntityUid grid)
    {
        var maps = Server.System<SharedMapSystem>();
        var component = SEntMan.GetComponent<MapGridComponent>(grid);
        var tile = maps.GetTileRef(grid, component, Vector2i.Zero).Tile;
        for (var x = -1; x <= 3; x++)
        for (var y = -1; y <= 1; y++)
            maps.SetTile((grid, component), new Vector2i(x, y), tile);
    }

    private EntityUid FindFire(EntityCoordinates coordinates) =>
        Server.System<EntityLookupSystem>().GetEntitiesInRange(coordinates, 0.1f)
            .Single(uid => SEntMan.HasComponent<DragonGroundFireComponent>(uid) && !SEntMan.IsQueuedForDeletion(uid));

    private float HeatDamage(EntityUid uid) =>
        Server.System<DamageableSystem>().GetAllDamage(uid).DamageDict.TryGetValue("Heat", out var heat)
            ? heat.Float()
            : 0f;
}
