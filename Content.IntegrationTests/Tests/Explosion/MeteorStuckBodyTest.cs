using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Stacks;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Explosion;

/// <summary>
/// Worst case a large-meteor blast can actually produce.
/// Ten impacts, each in its own walled room, with three groups of loose items:
/// items spawned inside a wall, one loose item on every tile inside the blast,
/// and items on the same grid far enough away that debris cannot reach them.
/// A pass means every probe has settled within one minute and the blast did not wake the far cohort.
/// </summary>
public sealed class MeteorStuckBodyTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true, Connected = false };

    // Large meteor: totalIntensity 150, slope 2, maxIntensity 100. That is about a 4-tile blast.
    private const string ExplosionType = "Default";
    private const float TotalIntensity = 150f;
    private const float Slope = 2f;
    private const float MaxTileIntensity = 100f;
    private const float TileBreakScale = 2f;
    private const int MaxTileBreak = 1;
    private const float BlastRadius = 4f;

    private const int Impacts = 10;
    private const float GridSpacing = 30000f;
    // physics.maxlinvelocity is 400 m/s. 400 m/s for the whole run cannot reach this pad.
    private const int FarPadTile = 15000;
    private const float MovedDistance = 1.5f;

    private const int EarlyCensusSeconds = 2;
    private const int ActivityObservationSeconds = 30;

    // Two per impact is already more than one blast produced here.
    // An island bigger than a few crowbars in one wall means the contact walk escaped the impact.
    private const int BlastStuckCeiling = 20;
    private const int IslandDynamicCeiling = 8;
    private const int ActivitySampleIntervalFrames = 5;

    private static readonly ProtoId<DamageTypePrototype> BluntDamageTypeId = "Blunt";

    private const string ItemProto = "Crowbar";
    private const string WallProto = "WallSolid";

    [Test]
    public async Task LargeMeteorBlast_StuckBodiesDoNotHoldTheGrid()
    {
        var map = await Pair.CreateTestMap();
        var tileDef = Server.ResolveDependency<ITileDefinitionManager>();
        var plating = new Tile(tileDef["Plating"].TileId);

        var probes = new List<Probe>();
        var walls = new HashSet<EntityUid>();

        await Server.WaitPost(() => BuildRooms(map.MapId, plating, probes, walls));
        await Server.WaitRunTicks(SGameTiming.TickRate);

        Census before = default;
        await Server.WaitAssertion(() => before = Measure(probes, walls));
        Assert.That(before.EmbeddedAlive, Is.EqualTo(Impacts * 8), "Embedded items were deleted before the blast.");
        Assert.That(before.BlastAlive, Is.GreaterThan(Impacts * 20), "The blast rooms were not stocked.");
        Assert.That(before.OutsideAlive, Is.EqualTo(Impacts * 4), "Far items were deleted before the blast.");
        Assert.That(before.AnchoredProbes, Is.EqualTo(0), "A probe spawned anchored, so the blast cannot throw it.");

        await Server.WaitPost(() =>
        {
            var explosions = Server.EntMan.System<ExplosionSystem>();
            var transform = Server.EntMan.System<SharedTransformSystem>();
            for (var i = 0; i < Impacts; i++)
            {
                var epicenter = transform.ToMapCoordinates(new EntityCoordinates(Rooms[i], 0.5f, 0.5f));
                explosions.QueueExplosion(
                    epicenter,
                    ExplosionType,
                    TotalIntensity,
                    Slope,
                    MaxTileIntensity,
                    null,
                    TileBreakScale,
                    MaxTileBreak,
                    canCreateVacuum: true,
                    addLog: false);
            }
        });

        Census early = default;
        var tickRate = SGameTiming.TickRate;
        var observationFrames = tickRate * ActivityObservationSeconds;
        var activity = await TrackActivity(
            probes,
            walls,
            observationFrames,
            tickRate * EarlyCensusSeconds,
            () => early = Measure(probes, walls));

        Census late = default;
        await Server.WaitAssertion(() => late = Measure(probes, walls));

        var cascade = late.OutsideAwakeNearSpawn > 0;
        var nothingMoving = late.EmbeddedAwake == 0
                            && late.BlastAwake == 0
                            && late.OutsideAwake == 0;
        var ruledOut = !cascade
            && nothingMoving
            && late.BlastAwakeOverlappingWall <= BlastStuckCeiling
            && late.MaxIslandDynamics <= IslandDynamicCeiling;

        var report = FormatReport(before, early, late, ActivityObservationSeconds, cascade, nothingMoving, ruledOut)
            + "\n"
            + FormatActivityReport(activity, observationFrames, tickRate);
        TestContext.Progress.WriteLine(report);
        Assert.That(ruledOut, Is.True, report);
    }

    /// <summary>
    /// One full glass stack breaks once per sheet, so 30 shards. A Booze-O-Mat restock dumps half its
    /// inventory and those glasses and bottles break into shards and broken bottles. The meteor rock
    /// then spawns on that pile.
    /// </summary>
    [Test]
    public async Task GlassStackAndBoozeRestockUnderMeteorRock_DoesNotHoldTheGrid()
    {
        var map = await Pair.CreateTestMap();
        var tileDef = Server.ResolveDependency<ITileDefinitionManager>();
        var plating = new Tile(tileDef["Plating"].TileId);
        var probes = new List<Probe>();
        var rocks = new HashSet<EntityUid>();
        var grid = EntityUid.Invalid;

        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            var mapSystem = entMan.System<SharedMapSystem>();
            var transform = entMan.System<SharedTransformSystem>();
            var created = mapSystem.CreateGridEntity(map.MapId);
            grid = created.Owner;

            var tiles = new List<(Vector2i, Tile)>();
            for (var x = -2; x <= 2; x++)
            for (var y = -2; y <= 2; y++)
                tiles.Add((new Vector2i(x, y), plating));
            tiles.Add((new Vector2i(FarPadTile, 0), plating));
            tiles.Add((new Vector2i(FarPadTile + 1, 0), plating));
            mapSystem.SetTiles(created.Owner, created.Comp, tiles);

            Spawn(entMan, "SheetGlass", grid, 0, 0);
            Spawn(entMan, "VendingMachineRestockBooze", grid, 0, 0);

            for (var i = 0; i < 4; i++)
            {
                var uid = Spawn(entMan, "SheetGlass1", grid, FarPadTile + (i % 2), 0);
                probes.Add(new Probe(uid, Cohort.Outside, WorldOf(transform, uid)));
            }
        });

        await Server.WaitRunTicks(5);

        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var blunt = SProtoMan.Index(BluntDamageTypeId);
            // 50 spawns one shard per sheet. 100 deletes the stack without the shard behavior.
            var stackDamage = new DamageSpecifier(blunt, FixedPoint2.New(60));
            // 20 dumps the restock. 40 deletes the box without dumping.
            var boxDamage = new DamageSpecifier(blunt, FixedPoint2.New(25));

            EntityUid stack = default;
            EntityUid box = default;
            var query = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var meta, out var xform))
            {
                if (!NearPile(xform))
                    continue;
                if (meta.EntityPrototype?.ID == "SheetGlass")
                    stack = uid;
                else if (meta.EntityPrototype?.ID == "VendingMachineRestockBooze")
                    box = uid;
            }

            Assert.That(entMan.EntityExists(stack), Is.True, "The full glass stack did not spawn.");
            Assert.That(entMan.GetComponent<StackComponent>(stack).Count, Is.EqualTo(30));
            Assert.That(entMan.EntityExists(box), Is.True, "The Booze-O-Mat restock did not spawn.");
            damageable.TryChangeDamage(stack, stackDamage, ignoreResistances: true);
            damageable.TryChangeDamage(box, boxDamage, ignoreResistances: true);
        });

        await Server.WaitRunTicks(1);

        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var blunt = SProtoMan.Index(BluntDamageTypeId);
            // Glasses and bottles break at 5. Shards from the stack survive until 100.
            var smash = new DamageSpecifier(blunt, FixedPoint2.New(15));
            var dumped = new List<EntityUid>();
            var query = entMan.EntityQueryEnumerator<DamageableComponent, TransformComponent, MetaDataComponent>();
            while (query.MoveNext(out var uid, out _, out var xform, out var meta))
            {
                if (!NearPile(xform))
                    continue;
                var id = meta.EntityPrototype?.ID;
                if (id is "ShardGlass" or "BrokenBottle" or "SheetGlass1")
                    continue;
                dumped.Add(uid);
            }

            Assert.That(dumped, Is.Not.Empty, "The restock box did not dump its inventory.");
            foreach (var uid in dumped)
                damageable.TryChangeDamage(uid, smash, ignoreResistances: true);
        });

        await Server.WaitRunTicks(5);

        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            var transform = entMan.System<SharedTransformSystem>();
            var shards = 0;
            var bottles = 0;
            var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var meta))
            {
                var id = meta.EntityPrototype?.ID;
                if (id != "ShardGlass" && id != "BrokenBottle")
                    continue;
                probes.Add(new Probe(uid, Cohort.Blast, WorldOf(transform, uid)));
                if (id == "ShardGlass")
                    shards++;
                else
                    bottles++;
            }

            // 30 from the stack, plus 10 glasses, 5 shot glasses and 5 coupes from half a Booze-O-Mat restock.
            Assert.That(shards, Is.GreaterThanOrEqualTo(50), $"Expected the stack and the restock glasses. Shards {shards}, broken bottles {bottles}.");

            rocks.Add(Spawn(entMan, "MeteorRock", grid, 0, 0));
        });

        await Server.WaitRunTicks(5);

        Census landed = default;
        await Server.WaitAssertion(() => landed = Measure(probes, rocks));

        var tickRate = SGameTiming.TickRate;
        var observationFrames = tickRate * ActivityObservationSeconds;
        Census early = default;
        var activity = await TrackActivity(
            probes,
            rocks,
            observationFrames,
            tickRate * EarlyCensusSeconds,
            () => early = Measure(probes, rocks));

        Census late = default;
        await Server.WaitAssertion(() => late = Measure(probes, rocks));

        var cascade = late.OutsideAwakeNearSpawn > 0;
        var nothingMoving = late.EmbeddedAwake == 0
                            && late.BlastAwake == 0
                            && late.OutsideAwake == 0;
        var ruledOut = !cascade
            && nothingMoving
            && late.BlastAwakeOverlappingWall <= BlastStuckCeiling
            && late.MaxIslandDynamics <= IslandDynamicCeiling;

        var report = "Glass under meteor rock. "
            + FormatReport(landed, early, late, ActivityObservationSeconds, cascade, nothingMoving, ruledOut)
            + "\n"
            + FormatActivityReport(activity, observationFrames, tickRate);
        TestContext.Progress.WriteLine(report);
        Assert.That(ruledOut, Is.True, report);
    }

    private readonly List<EntityUid> Rooms = new();

    private void BuildRooms(MapId mapId, Tile plating, List<Probe> probes, HashSet<EntityUid> walls)
    {
        var entMan = Server.EntMan;
        var mapSystem = entMan.System<SharedMapSystem>();
        var transform = entMan.System<SharedTransformSystem>();

        for (var i = 0; i < Impacts; i++)
        {
            var grid = mapSystem.CreateGridEntity(mapId);
            Rooms.Add(grid.Owner);
            transform.SetWorldPosition(grid.Owner, new Vector2(i * GridSpacing, 0f));

            var tiles = new List<(Vector2i, Tile)>();
            for (var x = -8; x <= 8; x++)
            {
                for (var y = -8; y <= 8; y++)
                    tiles.Add((new Vector2i(x, y), plating));
            }

            tiles.Add((new Vector2i(FarPadTile, 0), plating));
            tiles.Add((new Vector2i(FarPadTile, 1), plating));
            tiles.Add((new Vector2i(FarPadTile + 1, 0), plating));
            tiles.Add((new Vector2i(FarPadTile + 1, 1), plating));
            mapSystem.SetTiles(grid.Owner, grid.Comp, tiles);

            for (var x = -6; x <= 6; x++)
            {
                for (var y = -6; y <= 6; y++)
                {
                    if (Math.Abs(x) != 6 && Math.Abs(y) != 6)
                        continue;

                    walls.Add(Spawn(entMan, WallProto, grid.Owner, x, y));
                }
            }

            foreach (var (x, y) in new (int X, int Y)[]
            {
                (6, 0), (-6, 0), (0, 6), (0, -6),
                (6, 1), (6, -1), (-6, 1), (-6, -1),
            })
            {
                var uid = Spawn(entMan, ItemProto, grid.Owner, x, y);
                probes.Add(new Probe(uid, Cohort.Embedded, WorldOf(transform, uid)));
            }

            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                {
                    if (x * x + y * y > BlastRadius * BlastRadius)
                        continue;

                    var uid = Spawn(entMan, ItemProto, grid.Owner, x, y);
                    probes.Add(new Probe(uid, Cohort.Blast, WorldOf(transform, uid)));
                }
            }

            foreach (var (x, y) in new (int X, int Y)[]
            {
                (FarPadTile, 0), (FarPadTile, 1), (FarPadTile + 1, 0), (FarPadTile + 1, 1),
            })
            {
                var uid = Spawn(entMan, ItemProto, grid.Owner, x, y);
                probes.Add(new Probe(uid, Cohort.Outside, WorldOf(transform, uid)));
            }
        }
    }

    private static bool NearPile(TransformComponent xform)
    {
        var delta = xform.LocalPosition - new Vector2(0.5f, 0.5f);
        return delta.LengthSquared() < 9f;
    }

    private static EntityUid Spawn(IEntityManager entMan, string proto, EntityUid grid, int x, int y)
    {
        return entMan.SpawnEntity(proto, new EntityCoordinates(grid, x + 0.5f, y + 0.5f));
    }

    private static Vector2 WorldOf(SharedTransformSystem transform, EntityUid uid)
    {
        return transform.GetWorldPosition(uid);
    }

    private Census Measure(List<Probe> probes, HashSet<EntityUid> walls)
    {
        var entMan = Server.EntMan;
        var physics = entMan.System<SharedPhysicsSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var census = new Census();

        foreach (var probe in probes)
        {
            if (!entMan.EntityExists(probe.Entity))
            {
                census.Deleted++;
                continue;
            }

            var body = entMan.GetComponent<PhysicsComponent>(probe.Entity);
            var xform = entMan.GetComponent<TransformComponent>(probe.Entity);
            var awake = body.Awake;
            var inAir = body.BodyStatus == BodyStatus.InAir;
            var overlapsWall = OverlapsWall(physics, probe.Entity, walls);
            census.Alive++;
            if (xform.Anchored)
                census.AnchoredProbes++;

            switch (probe.Cohort)
            {
                case Cohort.Embedded:
                    census.EmbeddedAlive++;
                    if (awake)
                        census.EmbeddedAwake++;
                    if (inAir)
                        census.EmbeddedInAir++;
                    if (awake && overlapsWall)
                        census.EmbeddedAwakeOverlappingWall++;
                    break;
                case Cohort.Blast:
                    census.BlastAlive++;
                    if (awake)
                        census.BlastAwake++;
                    if (inAir)
                        census.BlastInAir++;
                    if (awake && overlapsWall)
                        census.BlastAwakeOverlappingWall++;
                    break;
                case Cohort.Outside:
                    census.OutsideAlive++;
                    if (awake)
                        census.OutsideAwake++;
                    if (inAir)
                        census.OutsideInAir++;
                    if (awake)
                    {
                        var moved = (transform.GetWorldPosition(probe.Entity) - probe.SpawnWorld).Length() > MovedDistance;
                        if (moved)
                            census.OutsideAwakeMoved++;
                        else
                            census.OutsideAwakeNearSpawn++;
                    }
                    break;
            }

            if (awake && overlapsWall)
                census.AwakeOverlappingWall++;
        }

        census.MaxIslandDynamics = MaxIslandDynamics(physics, entMan, probes);
        return census;
    }

    private async Task<Dictionary<EntityUid, ProbeActivity>> TrackActivity(
        List<Probe> probes,
        HashSet<EntityUid> obstacles,
        int observationFrames,
        int earlyFrame,
        System.Action captureEarly)
    {
        var activity = new Dictionary<EntityUid, ProbeActivity>();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var physics = entMan.System<SharedPhysicsSystem>();

            foreach (var probe in probes)
            {
                if (probe.Cohort == Cohort.Outside)
                    continue;

                var state = new ProbeActivity(probe.Cohort);
                if (entMan.EntityExists(probe.Entity))
                {
                    var body = entMan.GetComponent<PhysicsComponent>(probe.Entity);
                    var overlapsObstacle = OverlapsWall(physics, probe.Entity, obstacles);
                    state.AwakeAtImpact = body.Awake;
                    state.AwakeAtEnd = body.Awake;
                    state.OverlappingObstacleAtEnd = overlapsObstacle;
                    state.LastAwakeFrame = body.Awake ? 0 : -1;
                    state.LastObstacleContactFrame = overlapsObstacle ? 0 : -1;
                }
                else
                {
                    state.Deleted = true;
                }

                activity.Add(probe.Entity, state);
            }
        });

        for (var frame = ActivitySampleIntervalFrames; frame <= observationFrames; frame += ActivitySampleIntervalFrames)
        {
            await Server.WaitRunTicks(ActivitySampleIntervalFrames);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var physics = entMan.System<SharedPhysicsSystem>();

                foreach (var (uid, state) in activity)
                {
                    if (state.Deleted)
                        continue;

                    if (!entMan.EntityExists(uid))
                    {
                        state.Deleted = true;
                        continue;
                    }

                    var body = entMan.GetComponent<PhysicsComponent>(uid);
                    state.AwakeAtEnd = body.Awake;
                    if (body.Awake)
                        state.LastAwakeFrame = frame;

                    if (frame == observationFrames)
                    {
                        state.LinearVelocityAtEnd = body.LinearVelocity;
                        state.AngularVelocityAtEnd = body.AngularVelocity;
                        state.SleepTimeAtEnd = body.SleepTime;
                        state.SleepingAllowedAtEnd = body.SleepingAllowed;
                    }

                    state.OverlappingObstacleAtEnd = OverlapsWall(physics, uid, obstacles);
                    if (state.OverlappingObstacleAtEnd)
                        state.LastObstacleContactFrame = frame;
                }

                if (frame == earlyFrame)
                    captureEarly();
            });
        }

        return activity;
    }

    private static string FormatActivityReport(
        Dictionary<EntityUid, ProbeActivity> activity,
        int observationFrames,
        int tickRate)
    {
        var observationSeconds = observationFrames / tickRate;
        return $"Activity through {observationFrames} frames ({observationFrames / (float) tickRate:F1}s), " +
            $"sampled every {ActivitySampleIntervalFrames} frames:\n" +
            FormatCohortActivity(activity, Cohort.Embedded, observationSeconds, tickRate) + "\n" +
            FormatCohortActivity(activity, Cohort.Blast, observationSeconds, tickRate);
    }

    private static string FormatCohortActivity(
        Dictionary<EntityUid, ProbeActivity> activity,
        Cohort cohort,
        int observationSeconds,
        int tickRate)
    {
        var probes = 0;
        var awakeAtImpact = 0;
        var neverAwake = 0;
        var asleepAtEnd = 0;
        var awakeAtEnd = 0;
        var deleted = 0;
        var obstaclesTouched = 0;
        var stillOverlappingObstacle = 0;
        var lastAwakeFrames = new List<int>();
        var lastObstacleContactFrames = new List<int>();

        foreach (var state in activity.Values)
        {
            if (state.Cohort != cohort)
                continue;

            probes++;
            if (state.Deleted)
            {
                deleted++;
                continue;
            }

            if (state.AwakeAtImpact)
                awakeAtImpact++;

            if (state.LastAwakeFrame < 0)
            {
                neverAwake++;
            }
            else
            {
                lastAwakeFrames.Add(state.LastAwakeFrame);
                if (state.AwakeAtEnd)
                    awakeAtEnd++;
                else
                    asleepAtEnd++;
            }

            if (state.LastObstacleContactFrame < 0)
                continue;

            obstaclesTouched++;
            if (state.OverlappingObstacleAtEnd)
                stillOverlappingObstacle++;
            else
                lastObstacleContactFrames.Add(state.LastObstacleContactFrame);
        }

        return $"{cohort}: probes {probes}, awake at impact {awakeAtImpact}, never awake {neverAwake}, " +
            $"asleep at {observationSeconds}s after activity {asleepAtEnd}, still awake at {observationSeconds}s {awakeAtEnd}, deleted {deleted}; " +
            $"last-awake samples {FormatFrameSummary(lastAwakeFrames, tickRate)}; " +
            $"obstacle contacts {obstaclesTouched}, still overlapping at {observationSeconds}s {stillOverlappingObstacle}, " +
            $"last-contact samples {FormatFrameSummary(lastObstacleContactFrames, tickRate)}; " +
            $"awake probe velocities at end {FormatAwakeProbeVelocities(activity, cohort)}";
    }

    private static string FormatAwakeProbeVelocities(
        Dictionary<EntityUid, ProbeActivity> activity,
        Cohort cohort)
    {
        var velocities = new List<string>();
        foreach (var (uid, state) in activity)
        {
            if (state.Cohort != cohort || !state.AwakeAtEnd)
                continue;

            velocities.Add(
                $"{uid}: linear {state.LinearVelocityAtEnd.Length():F4} m/s, angular {state.AngularVelocityAtEnd:F4} rad/s, " +
                $"sleep time {state.SleepTimeAtEnd:F2}s, sleep allowed {state.SleepingAllowedAtEnd}");
        }

        return velocities.Count == 0 ? "none" : string.Join("; ", velocities);
    }

    private static string FormatFrameSummary(List<int> frames, int tickRate)
    {
        if (frames.Count == 0)
            return "none";

        frames.Sort();
        var median = frames[(frames.Count - 1) / 2];
        var percentile90 = frames[(int) Math.Ceiling(frames.Count * 0.9) - 1];
        var maximum = frames[^1];
        return $"p50/p90/max {median}/{percentile90}/{maximum}f " +
            $"({median / (float) tickRate:F1}/{percentile90 / (float) tickRate:F1}/{maximum / (float) tickRate:F1}s)";
    }

    private static bool OverlapsWall(SharedPhysicsSystem physics, EntityUid uid, HashSet<EntityUid> walls)
    {
        var contacts = physics.GetContacts(uid);
        while (contacts.MoveNext(out var contact))
        {
            if (!contact.IsTouching || !contact.Enabled)
                continue;
            if (contact.FixtureA?.Hard != true || contact.FixtureB?.Hard != true)
                continue;

            var other = contact.EntityA == uid ? contact.EntityB : contact.EntityA;
            if (walls.Contains(other))
                return true;
        }

        return false;
    }

    private static int MaxIslandDynamics(SharedPhysicsSystem physics, IEntityManager entMan, List<Probe> probes)
    {
        var seen = new HashSet<EntityUid>();
        var stack = new Stack<EntityUid>();
        var max = 0;

        foreach (var probe in probes)
        {
            if (!entMan.EntityExists(probe.Entity) || seen.Contains(probe.Entity))
                continue;
            if (!entMan.GetComponent<PhysicsComponent>(probe.Entity).Awake)
                continue;

            seen.Add(probe.Entity);
            var dynamics = 0;
            stack.Push(probe.Entity);
            while (stack.TryPop(out var uid))
            {
                if (!entMan.TryGetComponent<PhysicsComponent>(uid, out var body))
                    continue;
                if (body.BodyType == BodyType.Dynamic)
                    dynamics++;

                var contacts = physics.GetContacts(uid);
                while (contacts.MoveNext(out var contact))
                {
                    if (!contact.IsTouching || !contact.Enabled)
                        continue;
                    if (contact.FixtureA?.Hard != true || contact.FixtureB?.Hard != true)
                        continue;

                    var other = contact.EntityA == uid ? contact.EntityB : contact.EntityA;
                    if (seen.Add(other))
                        stack.Push(other);
                }
            }

            if (dynamics > max)
                max = dynamics;
        }

        return max;
    }

    private static string FormatReport(
        Census before,
        Census early,
        Census late,
        int observationSeconds,
        bool cascade,
        bool nothingMoving,
        bool ruledOut)
    {
        return $"""
            Meteor stuck-body census. Pass means every probe is asleep by the observation horizon and the far cohort stays asleep.
            Before: {before}
            {EarlyCensusSeconds}s:     {early}
            {observationSeconds}s:    {late}
            Far cohort woke without being reached: {cascade}
            Thrown items still overlapping a wall at {observationSeconds}s: {late.BlastAwakeOverlappingWall} (ceiling {BlastStuckCeiling})
            Items placed in a wall and still stuck at {observationSeconds}s: {late.EmbeddedAwakeOverlappingWall}
            Largest dynamic island at {observationSeconds}s: {late.MaxIslandDynamics} (ceiling {IslandDynamicCeiling})
            Nothing moving at {observationSeconds}s: {nothingMoving}
            Ruled out: {ruledOut}
            """;
    }

    private enum Cohort
    {
        Embedded,
        Blast,
        Outside,
    }

    private sealed class ProbeActivity(Cohort cohort)
    {
        public readonly Cohort Cohort = cohort;
        public int LastAwakeFrame = -1;
        public int LastObstacleContactFrame = -1;
        public bool AwakeAtImpact;
        public bool AwakeAtEnd;
        public bool OverlappingObstacleAtEnd;
        public bool Deleted;
        public Vector2 LinearVelocityAtEnd;
        public float AngularVelocityAtEnd;
        public float SleepTimeAtEnd;
        public bool SleepingAllowedAtEnd;
    }

    private readonly record struct Probe(EntityUid Entity, Cohort Cohort, Vector2 SpawnWorld);

    private struct Census
    {
        public int Alive;
        public int Deleted;
        public int AnchoredProbes;
        public int EmbeddedAlive;
        public int BlastAlive;
        public int OutsideAlive;
        public int EmbeddedAwake;
        public int BlastAwake;
        public int OutsideAwake;
        public int EmbeddedInAir;
        public int BlastInAir;
        public int OutsideInAir;
        public int EmbeddedAwakeOverlappingWall;
        public int BlastAwakeOverlappingWall;
        public int AwakeOverlappingWall;
        public int OutsideAwakeNearSpawn;
        public int OutsideAwakeMoved;
        public int MaxIslandDynamics;

        public override string ToString()
        {
            return $"alive {Alive} deleted {Deleted} anchored {AnchoredProbes}; " +
                $"embedded alive/awake/in-air/stuck {EmbeddedAlive}/{EmbeddedAwake}/{EmbeddedInAir}/{EmbeddedAwakeOverlappingWall}; " +
                $"blast alive/awake/in-air/stuck {BlastAlive}/{BlastAwake}/{BlastInAir}/{BlastAwakeOverlappingWall}; " +
                $"far alive/awake/in-air/near/moved {OutsideAlive}/{OutsideAwake}/{OutsideInAir}/{OutsideAwakeNearSpawn}/{OutsideAwakeMoved}; " +
                $"island {MaxIslandDynamics}";
        }
    }
}
