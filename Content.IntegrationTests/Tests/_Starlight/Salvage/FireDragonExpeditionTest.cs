#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Procedural.Events;
using Content.Server.Parallax;
using Content.Server.Procedural;
using Content.Server.Salvage;
using Content.Server.Salvage.Expeditions;
using Content.Shared._Starlight.FireDragon;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Procedural;
using Content.Shared.Salvage;
using Content.Shared.Salvage.Expeditions;
using Content.Shared.Shuttles.Components;
using Content.Shared.StationRecords;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Salvage;

[TestFixture]
public sealed class FireDragonExpeditionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Fresh = true, Dirty = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task HardCarpExpeditionsRequireBossPlacement(bool blockBossPlacement)
    {
        SpawnSalvageMissionJob job = null!;
        var queue = new JobQueue();
        EntityUid disk = default;
        EntityUid station = default;
        EntityUid otherMap = default;
        EntityUid otherDisk = default;
        var blockers = Server.System<BossPlacementTestSystem>();
        await Server.WaitAssertion(() =>
        {
            var salvage = Server.System<SharedSalvageSystem>();
            foreach (var difficultyName in new[] { "Carp-Easy", "Carp-Medium", "Carp-Hard" })
            {
                ProtoId<SalvageDifficultyPrototype> difficultyId = difficultyName;
                var difficulty = SProtoMan.Index(difficultyId);
                for (var seed = 0; seed < 64; seed++)
                {
                    var mission = salvage.GetMission(difficulty, seed);
                    if (difficultyName == "Carp-Hard")
                    {
                        Assert.That(mission.Faction, Is.EqualTo("CarpsDragon"));
                        Assert.That(mission.Dungeon, Is.EqualTo("FireDragon"));
                    }
                    else
                    {
                        Assert.That(mission.Faction, Is.EqualTo("Carps"));
                        Assert.That(mission.Dungeon, Is.Not.EqualTo("FireDragon"));
                    }
                }
            }

            station = SEntMan.Spawn(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<StationRecordsComponent>(station);
            var data = SEntMan.AddComponent<SalvageExpeditionDataComponent>(station);
            data.ActiveMission = 1;
            data.NextOffer = SGameTiming.CurTime + TimeSpan.FromHours(1);
            otherMap = Server.System<SharedMapSystem>().CreateMap(out _);
            otherDisk = SEntMan.Spawn(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<ShuttleDestinationCoordinatesComponent>(otherDisk).Destination = otherMap;
            disk = SEntMan.Spawn(null, MapCoordinates.Nullspace);
            blockers.BlockBossPlacement = blockBossPlacement;
            job = new SpawnSalvageMissionJob(
                0.002,
                SEntMan,
                SGameTiming,
                Server.ResolveDependency<ILogManager>(),
                SProtoMan,
                Server.System<AnchorableSystem>(),
                Server.System<BiomeSystem>(),
                Server.System<DungeonSystem>(),
                Server.System<MetaDataSystem>(),
                Server.System<SharedMapSystem>(),
                station,
                disk,
                new SalvageMissionParams { Index = 1, Difficulty = "Carp-Hard", Seed = 0 });
            queue.EnqueueJob(job);
        });

        for (var tick = 0; tick < 3000 && job.Status != JobStatus.Finished; tick++)
        {
            await Server.WaitPost(queue.Process);
            await Server.WaitRunTicks(1);
        }

        await Server.WaitAssertion(() =>
        {
            Assert.That(job.Exception, Is.Null);
            Assert.That(job.Status, Is.EqualTo(JobStatus.Finished));
            Assert.That(SEntMan.GetComponent<ShuttleDestinationCoordinatesComponent>(otherDisk).Destination, Is.EqualTo(otherMap));

            if (blockBossPlacement)
            {
                Assert.That(blockers.BlockedMap, Is.Not.Null, "The test must block an actual generated dungeon before boss placement.");
                Assert.That(job.Result, Is.False);
                Assert.That(SEntMan.Deleted(blockers.BlockedMap), Is.True);
                Assert.That(SEntMan.GetComponent<ShuttleDestinationCoordinatesComponent>(disk).Destination, Is.Null);
                var data = SEntMan.GetComponent<SalvageExpeditionDataComponent>(station);
                Assert.That(data.ActiveMission, Is.Zero);
                Assert.That(data.Claimed, Is.False);
                Assert.That(data.Cooldown, Is.False, "Rejected generation must not start the normal completion cooldown.");
                return;
            }

            Assert.That(job.Result, Is.True);
            var generatedMap = SEntMan.GetComponent<ShuttleDestinationCoordinatesComponent>(disk).Destination;
            Assert.That(generatedMap, Is.Not.Null);

            var dragons = 0;
            var carps = 0;
            var sharks = 0;
            var mobs = SEntMan.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (mobs.MoveNext(out var uid, out var metadata, out var transform))
            {
                if (transform.MapUid != generatedMap)
                    continue;

                switch (metadata.EntityPrototype?.ID)
                {
                    case "MobFireDragon":
                        dragons++;
                        Assert.That(SEntMan.GetComponent<FireDragonComponent>(uid).RoarActionEntity, Is.Not.Null);
                        break;
                    case "MobCarpDungeon":
                    case "MobCarpRainbow":
                        carps++;
                        break;
                    case "MobShark":
                        sharks++;
                        break;
                    case "MobDragonDungeon":
                        Assert.Fail("The replaced space dragon must not also spawn in the western dragon's expedition.");
                        break;
                }
            }

            Assert.That(dragons, Is.EqualTo(1));
            Assert.That(carps, Is.InRange(12, 18));
            Assert.That(sharks, Is.EqualTo(2));
        });
    }

    private sealed class BossPlacementTestSystem : EntitySystem
    {
        public bool BlockBossPlacement;
        public EntityUid? BlockedMap;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MapGridComponent, DungeonGeneratedEvent>(OnDungeonGenerated);
        }

        private void OnDungeonGenerated(EntityUid uid, MapGridComponent grid, DungeonGeneratedEvent args)
        {
            if (!BlockBossPlacement || !HasComp<SalvageExpeditionComponent>(uid))
                return;

            BlockedMap = uid;
            var maps = EntityManager.System<SharedMapSystem>();
            foreach (var tile in maps.GetAllTiles(uid, grid).ToArray())
                SpawnAttachedTo("WallSolid", maps.GridTileToLocal(uid, grid, tile.GridIndices));
        }
    }
}
