using Content.Shared._Starlight.CCVar;
using Prometheus;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Content.Server._Starlight.Physics.Diagnostics;

/// <summary>
/// Periodically exports awake-body counts so their growth over a round shows in Grafana.
/// Each sample is spread over several ticks, at most <see cref="BodiesPerTick"/> bodies per tick,
/// so its cost per tick stays flat however many bodies are awake.
/// </summary>
public sealed partial class PhysicsAwakeMetricsSystem : EntitySystem
{
    private const int BodiesPerTick = 500;

    private static readonly Gauge _awake = Metrics.CreateGauge(
        "physics_awake_bodies",
        "Awake physics bodies, including those on paused maps.");

    private static readonly Gauge _paused = Metrics.CreateGauge(
        "physics_awake_paused_bodies",
        "Awake bodies on paused maps, which are never put to sleep.");

    private static readonly Gauge _still = Metrics.CreateGauge(
        "physics_awake_still_bodies",
        "Awake bodies under the solver's sleep tolerance, excluding paused maps.");

    private static readonly Gauge _inSpace = Metrics.CreateGauge(
        "physics_awake_in_space_bodies",
        "Awake bodies not on a grid, excluding paused maps.");

    private static readonly Gauge _stuck = Metrics.CreateGauge(
        "physics_awake_stuck_bodies",
        "Bodies awake in both this sample and the previous one, excluding paused maps.");

    private static readonly Gauge _timerFull = Metrics.CreateGauge(
        "physics_awake_timer_full_bodies",
        "Still awake bodies with a full sleep timer: jammed, jointed, or in a moving island. Excludes paused maps.");

    private static readonly Gauge _timerReset = Metrics.CreateGauge(
        "physics_awake_timer_reset_bodies",
        "Still awake bodies whose sleep timer was reset within the last physics step. Excludes paused maps.");

    private static readonly Gauge _sampleSeconds = Metrics.CreateGauge(
        "physics_awake_sample_seconds",
        "Time the last sample took, summed over the ticks it was spread across.");

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<MetaDataComponent> _metaQuery;

    private readonly List<Entity<PhysicsComponent, TransformComponent>> _pending = new();
    private HashSet<EntityUid> _previous = new();
    private HashSet<EntityUid> _current = new();

    private bool _metricsEnabled;
    private float _interval;
    private float _accumulator;

    private bool _sampling;
    private int _cursor;
    private long _elapsed;
    private Counts _counts;

    private float _linearToleranceSq;
    private float _angularToleranceSq;
    private float _timeToSleep;
    private float _physicsStep;

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _metaQuery = GetEntityQuery<MetaDataComponent>();

        Subs.CVar(_cfg, CVars.MetricsEnabled, value => _metricsEnabled = value, true);
        Subs.CVar(_cfg, StarlightCCVars.PhysicsAwakeMetricsInterval, value => _interval = value, true);
    }

    public override void Update(float frameTime)
    {
        if (!_metricsEnabled || _interval <= 0f)
        {
            _pending.Clear();
            _sampling = false;
            return;
        }

        _accumulator += frameTime;
        if (!_sampling)
        {
            if (_accumulator < _interval)
                return;

            _accumulator = 0f;
            Begin();
        }

        Continue();
    }

    private void Begin()
    {
        var linear = _cfg.GetCVar(CVars.LinearSleepTolerance);
        var angular = _cfg.GetCVar(CVars.AngularSleepTolerance);
        var substeps = Math.Ceiling(_cfg.GetCVar(CVars.TargetMinimumTickrate) / (float) _cfg.GetCVar(CVars.NetTickrate));

        _linearToleranceSq = linear * linear;
        _angularToleranceSq = angular * angular;
        _timeToSleep = _cfg.GetCVar(CVars.TimeToSleep);
        _physicsStep = (float) (1.0 / (_timing.TickRate * substeps));

        // Copying the set is a flat copy; the per-body work is what gets spread over ticks.
        var start = Stopwatch.GetTimestamp();
        _pending.Clear();
        _pending.AddRange(_physics.AwakeBodies);
        _current.Clear();
        _counts = default;
        _cursor = 0;
        _elapsed = Stopwatch.GetTimestamp() - start;
        _sampling = true;
    }

    private void Continue()
    {
        var start = Stopwatch.GetTimestamp();
        var end = Math.Min(_cursor + BodiesPerTick, _pending.Count);

        for (; _cursor < end; _cursor++)
        {
            var (uid, body, xform) = _pending[_cursor];

            // Skip bodies that slept or were deleted since the sample began.
            if (body.Deleted || !body.Awake)
                continue;

            _counts.Awake++;

            // Paused bodies are never solved, so only their count is meaningful.
            if (_metaQuery.TryComp(uid, out var meta) && meta.EntityPaused)
            {
                _counts.Paused++;
                continue;
            }

            _current.Add(uid);
            if (_previous.Contains(uid))
                _counts.Stuck++;

            if (xform.GridUid == null && !_gridQuery.HasComp(uid))
                _counts.InSpace++;

            if (body.LinearVelocity.LengthSquared() > _linearToleranceSq
                || body.AngularVelocity * body.AngularVelocity > _angularToleranceSq)
            {
                continue;
            }

            _counts.Still++;
            if (body.SleepTime >= _timeToSleep)
                _counts.TimerFull++;
            if (body.SleepTime < _physicsStep)
                _counts.TimerReset++;
        }

        _elapsed += Stopwatch.GetTimestamp() - start;
        if (_cursor < _pending.Count)
            return;

        _awake.Set(_counts.Awake);
        _paused.Set(_counts.Paused);
        _still.Set(_counts.Still);
        _inSpace.Set(_counts.InSpace);
        _stuck.Set(_counts.Stuck);
        _timerFull.Set(_counts.TimerFull);
        _timerReset.Set(_counts.TimerReset);
        _sampleSeconds.Set((double) _elapsed / Stopwatch.Frequency);

        (_previous, _current) = (_current, _previous);
        _pending.Clear();
        _sampling = false;
    }

    private struct Counts
    {
        public int Awake;
        public int Paused;
        public int Still;
        public int InSpace;
        public int Stuck;
        public int TimerFull;
        public int TimerReset;
    }
}
