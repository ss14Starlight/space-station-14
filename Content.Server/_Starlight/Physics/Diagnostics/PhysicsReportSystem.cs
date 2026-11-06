using System.Collections.Concurrent;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading;
using Content.Server.Procedural;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Physics.Diagnostics;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics.Contacts;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Profiling;
using Robust.Shared.Threading;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Process = System.Diagnostics.Process;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Content.Server._Starlight.Physics.Diagnostics;

/// <summary>
/// Backs the <c>physicsreport</c> command. Phase times come from the engine profiler, switched on only while sampling.
/// </summary>
public sealed partial class PhysicsReportSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IParallelManager _parallel = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private ProfManager _prof = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    private const int TopRows = 25;
    private const int TopMaps = 15;
    private const int TopPausedRows = 10;
    private const int MinSnapshotsForStuck = 3;
    private const int NameWidth = 46;
    private const string TickKey = "Tick";
    private const string PhysicsTotalKey = "Physics total";
    private const string ContactLoopKey = "Collide minus manifolds";
    private const string PreSolve = "PreSolve";
    private const string PostSolve = "PostSolve";
    private const string PrePrefix = "pre: ";
    private const string PostPrefix = "post: ";

    // Written once per substep by SharedPhysicsSystem.SimulateWorld.
    private static readonly string[] _topPhases = [PreSolve, "Broadphase", "Collide", "Solve", PostSolve];

    private static readonly string[] _subPhases = ["Generate Manifolds", "Build Islands", "Solve Islands"];

    private static readonly ResPath _reportDir = new("/physics_reports");

    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<JointComponent> _jointQuery;
    private EntityQuery<DungeonAtlasTemplateComponent> _atlasQuery;

    private readonly Dictionary<string, double> _tick = new();
    private readonly List<(long Start, long End, bool Pre)> _solveRanges = new();
    private readonly HashSet<Contact> _seenContacts = new(ReferenceEqualityComparer.Instance);
    private readonly AwakeIslandEstimator _islands = new();

    private Session? _session;

    public override void Initialize()
    {
        base.Initialize();

        // Probe and snapshot right before physics.
        UpdatesBefore.Add(typeof(SharedPhysicsSystem));

        _mapQuery = GetEntityQuery<MapComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _jointQuery = GetEntityQuery<JointComponent>();
        _atlasQuery = GetEntityQuery<DungeonAtlasTemplateComponent>();

        SubscribeLocalEvent<PhysicsWakeEvent>(OnWake);
        SubscribeLocalEvent<StartCollideEvent>(OnStartCollide);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_session is { ProfWasEnabled: false })
            _cfg.SetCVar(CVars.ProfEnabled, false);

        _session = null;
    }

    /// <summary>
    /// Samples physics for <paramref name="seconds"/>, with the engine profiler on until the report is done.
    /// The report goes to <paramref name="shell"/>, and a player also gets a copy saved on their computer.
    /// </summary>
    /// <returns>False if a report is already being sampled.</returns>
    public bool TryStart(IConsoleShell shell, float seconds)
    {
        if (_session != null)
            return false;

        var profWasEnabled = _cfg.GetCVar(CVars.ProfEnabled);
        if (!profWasEnabled)
            _cfg.SetCVar(CVars.ProfEnabled, true);

        _session = new Session
        {
            Shell = shell,
            TicksTotal = Math.Max(2, (int) MathF.Round(seconds * _timing.TickRate)),
            ProfWasEnabled = profWasEnabled,
            LogOffset = _prof.Buffer.LogWriteOffset,
            Gen0 = GC.CollectionCount(0),
            Gen1 = GC.CollectionCount(1),
            Gen2 = GC.CollectionCount(2),
            GcPause = GC.GetTotalPauseDuration(),
            Cpu = CpuTime(),
            WallStart = Stopwatch.GetTimestamp(),
        };

        return true;
    }

    public override void Update(float frameTime)
    {
        if (_session is not { } s)
            return;

        ReadProfiler(s);

        s.PendingWorkItems.Add(ThreadPool.PendingWorkItemCount);
        ThreadPool.UnsafeQueueUserWorkItem(
            static state => state.Results.Enqueue(Stopwatch.GetElapsedTime(state.Queued).TotalMilliseconds),
            (Results: s.ProbeLatencies, Queued: Stopwatch.GetTimestamp()),
            preferLocal: false);

        // The snapshot inflates its tick, so that tick's whole-tick sample is dropped.
        s.SnapshotLastTick = s.TicksDone % _timing.TickRate == 0;
        if (s.SnapshotLastTick)
            TakeSnapshot(s);

        s.TicksDone++;
        if (s.TicksDone >= s.TicksTotal)
            Finish(s);
    }

    private void OnWake(ref PhysicsWakeEvent ev)
    {
        if (_session is not { } s)
            return;

        var key = ProtoName(ev.Entity);
        lock (s.Wakes)
        {
            s.Wakes[key] = s.Wakes.GetValueOrDefault(key) + 1;
        }
    }

    private void OnStartCollide(ref StartCollideEvent ev)
    {
        // Raised once per side; count each contact once.
        if (_session is not { } s || ev.OurEntity.Id > ev.OtherEntity.Id)
            return;

        var key = PairKey($"{ProtoName(ev.OurEntity)}:{ev.OurFixtureId}", $"{ProtoName(ev.OtherEntity)}:{ev.OtherFixtureId}");
        lock (s.CollideStarts)
        {
            s.CollideStarts[key] = s.CollideStarts.GetValueOrDefault(key) + 1;
        }
    }

    private void ReadProfiler(Session s)
    {
        ref var buffer = ref _prof.Buffer;
        var start = s.LogOffset;
        var end = buffer.LogWriteOffset;
        s.LogOffset = end;

        // The first read covers the tick the command ran in, before profiling started.
        if (s.TicksDone == 0)
            return;

        if (end - start > buffer.LogBuffer.LongLength)
        {
            s.Overruns++;
            return;
        }

        _tick.Clear();
        _solveRanges.Clear();

        for (var i = start; i < end; i++)
        {
            ref var log = ref buffer.Log(i);
            if (log.Type != ProfLogType.GroupEnd)
                continue;

            var name = _prof.GetString(log.GroupEnd.StringId);
            if (name is PreSolve or PostSolve)
                _solveRanges.Add((log.GroupEnd.StartIndex, i, name == PreSolve));
        }

        for (var i = start; i < end; i++)
        {
            ref var log = ref buffer.Log(i);
            if (log.Type != ProfLogType.GroupEnd || log.GroupEnd.Value.Type != ProfValueType.TimeAllocSample)
                continue;

            var name = _prof.GetString(log.GroupEnd.StringId);
            var ms = log.GroupEnd.Value.TimeAllocSample.Time * 1000.0;

            if (name == TickKey || _topPhases.Contains(name) || _subPhases.Contains(name))
            {
                AddTime(name, ms);
                continue;
            }

            // Anything nested in PreSolve/PostSolve is a VirtualController zone.
            foreach (var (rangeStart, rangeEnd, pre) in _solveRanges)
            {
                if (i <= rangeStart || i >= rangeEnd)
                    continue;

                AddTime((pre ? PrePrefix : PostPrefix) + name, ms);
                break;
            }
        }

        if (!_tick.ContainsKey("Solve"))
            return;

        var total = 0.0;
        foreach (var phase in _topPhases)
        {
            total += _tick.GetValueOrDefault(phase);
        }

        AddTime(PhysicsTotalKey, total);
        AddTime(ContactLoopKey, _tick.GetValueOrDefault("Collide") - _tick.GetValueOrDefault("Generate Manifolds"));

        if (s.SnapshotLastTick)
            _tick.Remove(TickKey);

        foreach (var (key, value) in _tick)
        {
            s.Samples.GetOrNew(key).Add(value);
        }

        s.RecordedTicks++;
    }

    private void AddTime(string key, double ms) => _tick[key] = _tick.GetValueOrDefault(key) + ms;

    private void TakeSnapshot(Session s)
    {
        s.Snapshots++;
        _seenContacts.Clear();
        _islands.Clear();

        // "Still" uses the solver's own sleep tolerances.
        var linearTolerance = _cfg.GetCVar(CVars.LinearSleepTolerance);
        var angularTolerance = _cfg.GetCVar(CVars.AngularSleepTolerance);
        var timeToSleep = _cfg.GetCVar(CVars.TimeToSleep);
        var physicsStep = 1f / (_timing.TickRate * Substeps());

        // The solver skips paused bodies, so they never join an island.
        foreach (var ent in _physics.AwakeBodies)
        {
            if (ent.Comp1.BodyType != BodyType.Static && !IsPaused(ent.Owner))
                _islands.Add(ent.Owner);
        }

        foreach (var ent in _physics.AwakeBodies)
        {
            var uid = ent.Owner;
            var body = ent.Comp1;
            var xform = ent.Comp2;

            var still = body.LinearVelocity.LengthSquared() <= linearTolerance * linearTolerance
                        && body.AngularVelocity * body.AngularVelocity <= angularTolerance * angularTolerance;
            var paused = IsPaused(uid);
            var inSpace = xform.GridUid == null && !_gridQuery.HasComp(uid);
            var protoName = ProtoName(uid);

            s.AwakeTotal++;
            s.AwakeStill += still ? 1 : 0;
            s.AwakeInSpace += inSpace ? 1 : 0;
            s.AwakePaused += paused ? 1 : 0;
            s.AwakeNoSleep += body.SleepingAllowed ? 0 : 1;
            s.BodyTypes[body.BodyType] = s.BodyTypes.GetValueOrDefault(body.BodyType) + 1;

            var map = GetMapStats(s, xform.MapUid);
            map.Awake++;
            map.Protos[protoName] = map.Protos.GetValueOrDefault(protoName) + 1;
            map.Still += still ? 1 : 0;

            var pressed = false;
            var bodyContacts = 0;
            var contacts = _physics.GetContacts(uid);
            while (contacts.MoveNext(out var contact))
            {
                bodyContacts++;
                pressed |= contact.IsTouching && contact.Hard;

                if (!_seenContacts.Add(contact))
                    continue;

                if (contact.Enabled && contact.IsTouching && contact.Hard)
                    _islands.Link(contact.EntityA, contact.EntityB);

                var touching = contact.IsTouching;
                var hardTouching = touching && contact.Hard;

                s.ContactTotal++;
                s.ContactTouching += touching ? 1 : 0;
                s.ContactHardTouching += hardTouching ? 1 : 0;
                map.Contacts++;

                var pair = s.Pairs.GetOrNew(PairKey(
                    $"{ProtoName(contact.EntityA)}:{contact.FixtureAId}",
                    $"{ProtoName(contact.EntityB)}:{contact.FixtureBId}"));
                pair.Count++;
                pair.Touching += touching ? 1 : 0;
                pair.HardTouching += hardTouching ? 1 : 0;
            }

            // Paused bodies are never solved, so they get their own section instead of crowding the tables.
            if (paused)
            {
                s.PausedProtos[protoName] = s.PausedProtos.GetValueOrDefault(protoName) + 1;
                continue;
            }

            var proto = s.Awake.GetOrNew(protoName);
            proto.Count++;
            proto.Still += still ? 1 : 0;
            proto.InSpace += inSpace ? 1 : 0;
            proto.NoSleep += body.SleepingAllowed ? 0 : 1;
            proto.Contacts += bodyContacts;

            var jointed = false;
            if (_jointQuery.TryComp(uid, out var joints))
            {
                jointed = joints.JointCount > 0;
                foreach (var joint in joints.GetJoints.Values)
                {
                    _islands.Link(joint.BodyAUid, joint.BodyBUid);
                }
            }

            var track = s.Tracked.GetOrNew(uid);
            track.Seen++;
            track.Proto = protoName;
            track.Still = still;
            track.InSpace = inSpace;
            track.Pressed = pressed;
            track.Jointed = jointed;
            track.TimerFull = still && body.SleepTime >= timeToSleep;
            track.TimerReset = still && body.SleepTime < physicsStep;
        }

        var (islandCount, largest) = _islands.Summarize();
        s.IslandSum += islandCount;
        s.LargestIslandSum += largest.Count;
        if (largest.Count > s.LargestIslandMax)
        {
            s.LargestIslandMax = largest.Count;
            s.LargestIslandProtos.Clear();
            foreach (var member in largest)
            {
                var name = ProtoName(member);
                s.LargestIslandProtos[name] = s.LargestIslandProtos.GetValueOrDefault(name) + 1;
            }
        }

        var grids = AllEntityQuery<MapGridComponent, TransformComponent>();
        while (grids.MoveNext(out var gridUid, out _, out var gridXform))
        {
            var gridMap = GetMapStats(s, gridXform.MapUid);
            gridMap.Grids++;
            gridMap.GridName ??= _metaQuery.TryComp(gridUid, out var gridMeta) ? gridMeta.EntityName : null;
            s.Grids++;
        }
    }

    private bool IsPaused(EntityUid uid) => _metaQuery.TryComp(uid, out var meta) && meta.EntityPaused;

    private MapStats GetMapStats(Session s, EntityUid? mapUid)
    {
        var key = mapUid ?? EntityUid.Invalid;
        if (s.Maps.TryGetValue(key, out var stats))
            return stats;

        stats = new MapStats { Uid = mapUid };
        if (mapUid is { } uid && _metaQuery.TryComp(uid, out var meta))
        {
            stats.MapName = meta.EntityName;
            stats.Paused = meta.EntityPaused;
            stats.AtlasFile = _atlasQuery.TryComp(uid, out var atlas) ? atlas.Path.Filename : null;
        }

        s.Maps[key] = stats;
        return stats;
    }

    private string ProtoName(EntityUid uid)
    {
        if (_gridQuery.HasComp(uid))
            return "(grid)";

        if (_mapQuery.HasComp(uid))
            return "(map)";

        return _metaQuery.TryComp(uid, out var meta)
            ? meta.EntityPrototype?.ID ?? "(no prototype)"
            : "(deleted)";
    }

    private int Substeps() =>
        (int) Math.Ceiling(_cfg.GetCVar(CVars.TargetMinimumTickrate) / (float) _cfg.GetCVar(CVars.NetTickrate));

    private static TimeSpan CpuTime()
    {
        using var process = Process.GetCurrentProcess();
        return process.TotalProcessorTime;
    }

    private static string PairKey(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? $"{a} <-> {b}" : $"{b} <-> {a}";

    private void Finish(Session s)
    {
        _session = null;

        if (!s.ProfWasEnabled)
            _cfg.SetCVar(CVars.ProfEnabled, false);

        var report = BuildReport(s);
        var path = SaveReport(report);
        Log.Info(path == null ? "Physics report finished; saving it failed." : $"Physics report saved to {path}");

        if (s.Shell.Player is { Status: SessionStatus.Disconnected })
            return;

        if (s.Shell.Player is { } player)
            RaiseNetworkEvent(new PhysicsReportReadyEvent(report), player);

        try
        {
            s.Shell.WriteLine(report);
            if (path != null)
                s.Shell.WriteLine(Loc.GetString("cmd-physicsreport-server-copy", ("path", path)));
        }
        catch (Exception e)
        {
            Log.Warning($"Could not print physics report to the requesting shell: {e.Message}");
        }
    }

    private string? SaveReport(string report)
    {
        try
        {
            _resources.UserData.CreateDir(_reportDir);
            var path = _reportDir / $"physics_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
            _resources.UserData.WriteAllText(path, report);
            return path.ToString();
        }
        catch (Exception e)
        {
            Log.Error($"Could not save physics report: {e}");
            return null;
        }
    }

    private string BuildReport(Session s)
    {
        var sb = new StringBuilder();
        var wall = Stopwatch.GetElapsedTime(s.WallStart).TotalSeconds;
        var tickRate = _timing.TickRate;
        var substeps = Substeps();
        var cpu = (CpuTime() - s.Cpu).TotalSeconds;
        var snapshots = Math.Max(1, s.Snapshots);

        ThreadPool.GetMinThreads(out var minWorkers, out _);
        ThreadPool.GetMaxThreads(out var maxWorkers, out _);

        sb.AppendLine($"=== Physics report: {wall:F1} s, {s.RecordedTicks} ticks recorded, {s.Snapshots} snapshots, {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC ===");
        sb.AppendLine($"Players {_players.PlayerCount}, entities {EntityManager.EntityCount}, grids {s.Grids / (float) snapshots:F0}");
        sb.AppendLine($"Tickrate {tickRate}, physics substeps {substeps}, mover substep gating {_cfg.GetCVar(StarlightCCVars.PhysicsMoverSubstepGating)}");
        sb.AppendLine($"Cores {Environment.ProcessorCount}, parallel count {_parallel.ParallelProcessCount} (thread.parallel_count {_cfg.GetCVar(CVars.ThreadParallelCount)}), ThreadPool threads {ThreadPool.ThreadCount} (min {minWorkers}, max {maxWorkers})");
        sb.AppendLine($"Server GC {GCSettings.IsServerGC}, latency mode {GCSettings.LatencyMode}");
        sb.AppendLine($"Process CPU: {cpu / wall:F2} cores busy on average out of {Environment.ProcessorCount}");
        sb.AppendLine($"GC during window: gen0 {GC.CollectionCount(0) - s.Gen0}, gen1 {GC.CollectionCount(1) - s.Gen1}, gen2 {GC.CollectionCount(2) - s.Gen2}, pause {(GC.GetTotalPauseDuration() - s.GcPause).TotalMilliseconds:F1} ms total");

        var probes = s.ProbeLatencies.ToList();
        var (_, probeP50, probeP95, probeMax) = Stats(probes);
        sb.AppendLine($"ThreadPool queue before physics: pending items avg {(s.PendingWorkItems.Count > 0 ? s.PendingWorkItems.Average() : 0):F1}, max {(s.PendingWorkItems.Count > 0 ? s.PendingWorkItems.Max() : 0)}");
        sb.AppendLine($"ThreadPool dispatch latency (ms, {probes.Count} probes): p50 {probeP50:F3}, p95 {probeP95:F3}, max {probeMax:F3}");
        if (s.Overruns > 0)
            sb.AppendLine($"Profiler buffer overran on {s.Overruns} ticks; raise prof.buffer_size if this is large.");

        sb.AppendLine();
        sb.AppendLine("--- Time per tick (ms), all substeps summed ---");
        sb.AppendLine($"{"",-NameWidth}{"mean",8}{"p50",8}{"p95",8}{"max",8}");
        AppendTime(sb, s, TickKey, "Whole tick (excl. snapshot ticks)");
        AppendTime(sb, s, PhysicsTotalKey, "Physics total");
        AppendTime(sb, s, PreSolve, "  PreSolve (controllers)");
        AppendTime(sb, s, "Broadphase", "  Broadphase (new pairs, PreventCollide)");
        AppendTime(sb, s, "Collide", "  Collide");
        AppendTime(sb, s, "Generate Manifolds", "    Generate Manifolds (parallel)");
        AppendTime(sb, s, ContactLoopKey, "    Contact loop + collide events");
        AppendTime(sb, s, "Solve", "  Solve");
        AppendTime(sb, s, "Build Islands", "    Build Islands");
        AppendTime(sb, s, "Solve Islands", "    Solve Islands (parallel)");
        AppendTime(sb, s, PostSolve, "  PostSolve (controllers)");

        var controllers = s.Samples.Keys
            .Where(k => k.StartsWith(PrePrefix) || k.StartsWith(PostPrefix))
            .OrderByDescending(k => s.Samples[k].Average());

        foreach (var key in controllers)
        {
            AppendTime(sb, s, key, "  " + key);
        }

        sb.AppendLine();
        sb.AppendLine("--- Awake bodies (average per snapshot) ---");
        sb.AppendLine($"Total {s.AwakeTotal / (float) snapshots:F1}: not moving {s.AwakeStill / (float) snapshots:F1}, in space {s.AwakeInSpace / (float) snapshots:F1}, on paused maps {s.AwakePaused / (float) snapshots:F1}, sleep disabled {s.AwakeNoSleep / (float) snapshots:F1}");
        sb.AppendLine("By body type: " + string.Join(", ", s.BodyTypes.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {p.Value / (float) snapshots:F1}")));
        sb.AppendLine($"Contacts on awake bodies: {s.ContactTotal / (float) snapshots:F1}, touching {s.ContactTouching / (float) snapshots:F1}, hard and touching {s.ContactHardTouching / (float) snapshots:F1}");

        sb.AppendLine();
        sb.AppendLine($"{"Map",-NameWidth}{"paused",8}{"grids",8}{"awake",8}{"still",8}{"contacts",10}");
        foreach (var map in s.Maps.Values.OrderByDescending(m => m.Awake).Take(TopMaps))
        {
            sb.AppendLine($"{Fit(map.Label),-NameWidth}{(map.Paused ? "yes" : "no"),8}{map.Grids / (float) snapshots,8:F0}{map.Awake / (float) snapshots,8:F1}{map.Still / (float) snapshots,8:F1}{map.Contacts / (float) snapshots,10:F1}");
        }

        sb.AppendLine();
        sb.AppendLine($"--- On paused maps: {s.AwakePaused / (float) snapshots:F1} awake bodies, never solved so never asleep ---");
        foreach (var (id, count) in s.PausedProtos.OrderByDescending(p => p.Value).Take(TopPausedRows))
        {
            sb.AppendLine($"{Fit(id),-NameWidth}{count / (float) snapshots,8:F1}");
        }

        sb.AppendLine();
        sb.AppendLine($"{"Awake prototype (not paused)",-NameWidth}{"awake",8}{"still",8}{"space",8}{"nosleep",8}{"cont/body",10}");
        foreach (var (id, p) in s.Awake.OrderByDescending(p => p.Value.Count).Take(TopRows))
        {
            sb.AppendLine($"{Fit(id),-NameWidth}{p.Count / (float) snapshots,8:F1}{p.Still / (float) snapshots,8:F1}{p.InSpace / (float) snapshots,8:F1}{p.NoSleep / (float) snapshots,8:F1}{p.Contacts / (float) p.Count,10:F1}");
        }

        sb.AppendLine();
        AppendStuckAwake(sb, s);

        sb.AppendLine();
        sb.AppendLine("--- Islands (approx.: touching hard contacts and joints between awake, unpaused bodies) ---");
        sb.AppendLine($"Multi-body islands {s.IslandSum / (float) snapshots:F1} per snapshot, largest {s.LargestIslandMax} bodies (average largest {s.LargestIslandSum / (float) snapshots:F1})");
        if (s.LargestIslandProtos.Count > 0)
            sb.AppendLine("Largest island: " + string.Join(", ", s.LargestIslandProtos.OrderByDescending(p => p.Value).Take(8).Select(p => $"{p.Key} x{p.Value}")));

        sb.AppendLine();
        sb.AppendLine($"{"Contact pair (prototype:fixture)",-NameWidth - 30}{"count",8}{"touch",8}{"hard",8}");
        foreach (var (key, p) in s.Pairs.OrderByDescending(p => p.Value.Count).Take(TopRows))
        {
            sb.AppendLine($"{Fit(key, NameWidth + 30),-NameWidth - 30}{p.Count / (float) snapshots,8:F1}{p.Touching / (float) snapshots,8:F1}{p.HardTouching / (float) snapshots,8:F1}");
        }

        sb.AppendLine();
        sb.AppendLine($"{"Wakes per second by prototype",-NameWidth - 30}{"per s",8}");
        foreach (var (id, count) in s.Wakes.OrderByDescending(p => p.Value).Take(TopRows))
        {
            sb.AppendLine($"{Fit(id, NameWidth + 30),-NameWidth - 30}{count / wall,8:F1}");
        }

        sb.AppendLine();
        sb.AppendLine($"{"Collision starts per second (prototype:fixture)",-NameWidth - 30}{"per s",8}");
        foreach (var (key, count) in s.CollideStarts.OrderByDescending(p => p.Value).Take(TopRows))
        {
            sb.AppendLine($"{Fit(key, NameWidth + 30),-NameWidth - 30}{count / wall,8:F1}");
        }

        sb.AppendLine();
        sb.AppendLine("Parallel phases (Broadphase, Generate Manifolds, Solve Islands) wait on the ThreadPool; compare them with the dispatch latency above.");
        sb.AppendLine("\"still\" = awake but under the solver's sleep tolerance. \"pressed\" = touching something hard.");
        sb.AppendLine("\"full\" = still with a full sleep timer: held up by a jam the solver can't resolve, a joint, or a moving body in its island.");
        sb.AppendLine("\"reset\" = still, but its sleep timer was reset within the last physics step: code waking it every tick, or jitter.");
        sb.AppendLine("Bodies in one island only sleep together, so one moving body keeps its whole island awake.");
        return sb.ToString();
    }

    private static void AppendStuckAwake(StringBuilder sb, Session s)
    {
        if (s.Snapshots < MinSnapshotsForStuck)
        {
            sb.AppendLine($"Stuck-awake bodies need at least {MinSnapshotsForStuck} snapshots; sample for longer.");
            return;
        }

        var stuck = s.Tracked.Values.Where(t => t.Seen == s.Snapshots).ToList();
        sb.AppendLine($"--- Awake in all {s.Snapshots} snapshots, not paused: {stuck.Count} of {s.Tracked.Count} bodies seen awake ---");
        sb.AppendLine($"{"Stuck-awake prototype",-NameWidth}{"count",8}{"still",8}{"space",8}{"pressed",8}{"joint",8}{"full",8}{"reset",8}");
        foreach (var group in stuck.GroupBy(t => t.Proto).OrderByDescending(g => g.Count()).Take(TopRows))
        {
            sb.AppendLine($"{Fit(group.Key),-NameWidth}{group.Count(),8}{group.Count(t => t.Still),8}{group.Count(t => t.InSpace),8}{group.Count(t => t.Pressed),8}{group.Count(t => t.Jointed),8}{group.Count(t => t.TimerFull),8}{group.Count(t => t.TimerReset),8}");
        }
    }

    private static void AppendTime(StringBuilder sb, Session s, string key, string label)
    {
        if (!s.Samples.TryGetValue(key, out var samples))
            return;

        var (mean, p50, p95, max) = Stats(samples);
        sb.AppendLine($"{Fit(label),-NameWidth}{mean,8:F3}{p50,8:F3}{p95,8:F3}{max,8:F3}");
    }

    private static (double Mean, double P50, double P95, double Max) Stats(List<double> values)
    {
        if (values.Count == 0)
            return default;

        var sorted = values.ToArray();
        Array.Sort(sorted);
        return (sorted.Average(), At(0.5), At(0.95), sorted[^1]);

        double At(double q) => sorted[Math.Min(sorted.Length - 1, (int) (q * sorted.Length))];
    }

    private static string Fit(string text, int width = NameWidth) =>
        text.Length < width ? text : text[..(width - 4)] + "...";

    private sealed class Session
    {
        public IConsoleShell Shell = default!;
        public int TicksTotal;
        public int TicksDone;
        public int RecordedTicks;
        public bool ProfWasEnabled;
        public long LogOffset;
        public int Overruns;
        public bool SnapshotLastTick;

        public int Gen0;
        public int Gen1;
        public int Gen2;
        public TimeSpan GcPause;
        public TimeSpan Cpu;
        public long WallStart;

        public readonly Dictionary<string, List<double>> Samples = new();
        public readonly ConcurrentQueue<double> ProbeLatencies = new();
        public readonly List<long> PendingWorkItems = new();

        public int Snapshots;
        public int Grids;
        public int AwakeTotal;
        public int AwakeStill;
        public int AwakeInSpace;
        public int AwakePaused;
        public int AwakeNoSleep;
        public int ContactTotal;
        public int ContactTouching;
        public int ContactHardTouching;
        public readonly Dictionary<BodyType, int> BodyTypes = new();
        public readonly Dictionary<string, ProtoStats> Awake = new();
        public readonly Dictionary<string, PairStats> Pairs = new();
        public readonly Dictionary<EntityUid, MapStats> Maps = new();
        public readonly Dictionary<EntityUid, AwakeTrack> Tracked = new();
        public readonly Dictionary<string, int> PausedProtos = new();

        public int IslandSum;
        public int LargestIslandSum;
        public int LargestIslandMax;
        public readonly Dictionary<string, int> LargestIslandProtos = new();

        public readonly Dictionary<string, int> Wakes = new();
        public readonly Dictionary<string, int> CollideStarts = new();
    }

    private sealed class ProtoStats
    {
        public int Count;
        public int Still;
        public int InSpace;
        public int NoSleep;
        public int Contacts;
    }

    private sealed class PairStats
    {
        public int Count;
        public int Touching;
        public int HardTouching;
    }

    /// <summary>
    /// Attributes are from the latest snapshot the body was awake in.
    /// </summary>
    private sealed class AwakeTrack
    {
        public int Seen;
        public string Proto = string.Empty;
        public bool Still;
        public bool InSpace;
        public bool Pressed;
        public bool Jointed;
        public bool TimerFull;
        public bool TimerReset;
    }

    private sealed class MapStats
    {
        public EntityUid? Uid;
        public string MapName = string.Empty;
        public string? GridName;
        public string? AtlasFile;
        public bool Paused;
        public int Grids;
        public int Awake;
        public int Still;
        public int Contacts;
        public readonly Dictionary<string, int> Protos = new();

        // Loaded maps are usually unnamed, so fall back to the dungeon atlas, the grid's name, or the most common body.
        public string Label =>
            Uid is not { } uid ? "(no map)"
            : MapName.Length > 0 ? $"{MapName} ({uid})"
            : AtlasFile != null ? $"atlas {AtlasFile} ({uid})"
            : GridName is { Length: > 0 } ? $"grid \"{GridName}\" ({uid})"
            : Protos.Count > 0 ? $"(unnamed, mostly {Protos.MaxBy(p => p.Value).Key}) ({uid})"
            : $"(unnamed) ({uid})";
    }
}
