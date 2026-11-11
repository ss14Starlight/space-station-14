using System.Numerics;
using Content.Client.Power.Components;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Sleepiness.Components;
using Content.Shared._Starlight.Sleepiness.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Containers;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Sleepiness;

public sealed partial class LightSleepSonarOverlay : Robust.Client.Graphics.Overlay
{
    private static readonly ProtoId<ShaderPrototype> _echoShaderId = "LightSleepSonarEcho";

    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const float EchoRange = 10f;
    private const float EchoRangeSquared = EchoRange * EchoRange;
    private const float RadarWaveLifetimeSeconds = 12f;
    private const int MaximumActiveRadarWaves = 2;
    private const float RadarWaveEmissionIntervalSeconds = RadarWaveLifetimeSeconds / MaximumActiveRadarWaves;
    private const double RadarSweepStartAngle = Math.PI / 2d;
    private const double FullRotationRadians = Math.PI * 2d;
    private const double DegreesToRadians = Math.PI / 180d;
    private const double CatEarSweepSpeedVariation = 0.1d;
    private const float RadarSweepBeamHalfAngleRadians = 0.02f;
    private const float VisibilityFadeInSeconds = 0.2f;
    private const float VisibilityFadeSeconds = 3.5f;
    private const float MinimumVisibility = 0.05f;
    private const float MaximumWaveAlpha = 0.275f;
    private const float StationarySourceWaveLifetimeSeconds = 4.4f;
    private const float StationarySourceWaveIntervalSeconds = StationarySourceWaveLifetimeSeconds;
    private const float StationarySourceWaveStartRadius = 0.25f;
    private const float SourceWaveMaximumRadius = 1.25f;
    private const float SourceWaveMaximumRadiusSquared = SourceWaveMaximumRadius * SourceWaveMaximumRadius;
    private const float MovingSourceWaveRateMultiplier = 1.5f;
    private const float MovingSourceWaveLifetimeSeconds =
        StationarySourceWaveLifetimeSeconds / MovingSourceWaveRateMultiplier;
    private const float StationarySourceWaveAlpha = 0.275f;
    private const float MovingSourceWaveAlpha = 0.4f;
    private const float SourceDetectionRange = EchoRange + SourceWaveMaximumRadius;
    private const float SourceDetectionRangeSquared = SourceDetectionRange * SourceDetectionRange;

    private readonly ShaderInstance _echoShader;
    private readonly TransformSystem _transform;
    private readonly SharedContainerSystem _container;
    private readonly SpriteSystem _spriteSystem;
    private readonly LightSleepSystem _lightSleep;
    private readonly MobStateSystem _mobState;
    private readonly DamageableSystem _damageable;
    private readonly MobThresholdSystem _mobThresholds;
    private readonly List<SonarWave> _radarWaves = new();
    private readonly List<SonarWave> _sourceWaves = new();
    private readonly List<CatEarSweepSegment> _catEarSweepSegments = new();
    private readonly CatEarSweepState[] _catEarSweeps = [new(true), new(false)];
    private readonly List<EchoTarget> _visibleTargets = new();
    private readonly Dictionary<EntityUid, TargetRevealState> _targetRevealStates = new();
    private readonly Dictionary<EntityUid, Vector2> _previousTargetPositions = new();
    private readonly Dictionary<EntityUid, ShaderInstance> _targetEchoShaders = new();
    private readonly Dictionary<EntityUid, SourceEmitterState> _sourceEmitterStates = new();
    private readonly HashSet<EntityUid> _currentTargets = new();
    private readonly HashSet<EntityUid> _currentEmitters = new();
    private readonly List<EntityUid> _staleTargets = new();
    private readonly List<EntityUid> _staleEmitters = new();
    private EntityUid _waveEmitter;
    private MapId _waveMapId;
    private double _lastWaveEmissionTime = double.NegativeInfinity;
    private double _lastWaveUpdateTime = double.NaN;
    private double _radarSweepStartTime = double.NaN;
    private double _radarSweepRadiansPerSecond;
    private double _lastCatEarEyeRotation = double.NaN;
    private Vector2 _lastRadarOrigin;
    private SonarRevealType? _activeRevealType;
    private int? _lastLoggedInvalidRevealType;
    private bool _waveContextInitialized;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public LightSleepSonarOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = 1;
        _echoShader = _prototypeManager.Index(_echoShaderId).InstanceUnique();
        _transform = _entityManager.System<TransformSystem>();
        _container = _entityManager.System<SharedContainerSystem>();
        _spriteSystem = _entityManager.System<SpriteSystem>();
        _lightSleep = _entityManager.System<LightSleepSystem>();
        _mobState = _entityManager.System<MobStateSystem>();
        _damageable = _entityManager.System<DamageableSystem>();
        _mobThresholds = _entityManager.System<MobThresholdSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_playerManager.LocalEntity is not { } player ||
            !_lightSleep.IsLightSleepActive(player) ||
            !_entityManager.TryGetComponent(player, out EyeComponent? eye))
        {
            return false;
        }

        return args.Viewport.Eye == eye.Eye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_playerManager.LocalEntity is not { } player ||
            args.Viewport.Eye is not { } eye ||
            !_entityManager.TryGetComponent(player, out TransformComponent? playerTransform) ||
            playerTransform.MapID != args.MapId)
        {
            return;
        }

        var worldHandle = args.WorldHandle;
        var (radarOrigin, _) = _transform.GetWorldPositionRotation(playerTransform);
        var eyeRotation = eye.Rotation;
        var elapsed = _timing.RealTime.TotalSeconds;
        var revealType = GetRevealType(_cfg.GetCVar(StarlightCCVars.LightSleepSonarRevealType));
        var sweepSpeedDegreesPerSecond =
            _cfg.GetCVar(StarlightCCVars.LightSleepSonarSweepSpeedDegreesPerSecond);
        var catEarSweepSettings = new CatEarSweepSettings(
            _cfg.GetCVar(StarlightCCVars.LightSleepSonarCatEarStartAngleDegrees),
            _cfg.GetCVar(StarlightCCVars.LightSleepSonarCatEarLineSeparationDegrees));
        _radarSweepRadiansPerSecond =
            sweepSpeedDegreesPerSecond * DegreesToRadians;
        worldHandle.UseShader(null);
        worldHandle.SetTransform(Matrix3x2.Identity);
        EnsureWaveContext(player, args.MapId, radarOrigin, elapsed);
        if (_activeRevealType != revealType)
        {
            _activeRevealType = revealType;
            ResetCatEarSweep(elapsed, sweepSpeedDegreesPerSecond, catEarSweepSettings);
        }

        if (revealType == SonarRevealType.CatEars)
            UpdateCatEarSweep(elapsed, sweepSpeedDegreesPerSecond, catEarSweepSettings);
        else
            _catEarSweepSegments.Clear();

        if (elapsed - _lastWaveEmissionTime >= RadarWaveEmissionIntervalSeconds &&
            CountPropagatingWaves(args.MapId, elapsed) < MaximumActiveRadarWaves)
        {
            var echoMode = GetEchoMode(player);
            var injuryChance = echoMode == EchoMode.Critical ? 0f : GetInjuryChance(player);
            var red = echoMode == EchoMode.Critical || (injuryChance > 0f && _random.NextFloat() < injuryChance);
            _radarWaves.Add(new SonarWave(
                args.MapId,
                radarOrigin,
                null,
                elapsed,
                RadarWaveLifetimeSeconds,
                0f,
                EchoRange,
                MaximumWaveAlpha,
                true,
                red));
            _lastWaveEmissionTime = elapsed;
        }

        _visibleTargets.Clear();
        _currentTargets.Clear();
        _currentEmitters.Clear();

        var query = _entityManager.EntityQueryEnumerator<SpriteComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sprite, out var meta, out var xform))
        {
            if (!sprite.Visible ||
                xform.MapID != args.MapId ||
                _container.IsEntityInContainer(uid, meta))
            {
                continue;
            }

            var (position, rotation) = _transform.GetWorldPositionRotation(xform);
            var distanceSquared = Vector2.DistanceSquared(position, radarOrigin);
            if (distanceSquared > SourceDetectionRangeSquared)
                continue;

            var echoMode = GetEchoMode(uid);
            if (echoMode == EchoMode.None)
                continue;

            var isMob = _entityManager.TryGetComponent(uid, out MobStateComponent? _);
            if (isMob)
            {
                _currentEmitters.Add(uid);
                UpdateSourceWaves(uid, position, args.MapId, echoMode, uid != player, elapsed);
            }

            if (uid == player || distanceSquared > EchoRangeSquared)
                continue;

            var damageRatio = GetDamageRatio(uid, echoMode);
            _visibleTargets.Add(new EchoTarget(uid, sprite, position, rotation, damageRatio, isMob));
            _currentTargets.Add(uid);
        }

        ProcessWaveHits(elapsed, radarOrigin, revealType, eyeRotation.Theta, catEarSweepSettings);
        PruneExpiredWaves(elapsed);
        PruneExpiredSourceWaves(elapsed);
        PruneSourceEmitterStates();
        PruneTargetState();

        worldHandle.UseShader(null);
        worldHandle.SetTransform(Matrix3x2.Identity);
        DrawSourceWaves(worldHandle, args.MapId, elapsed);

        foreach (var target in _visibleTargets)
        {
            var visibility = GetRevealIntensity(target.Uid, elapsed);
            if (visibility <= 0f)
                continue;

            var echoShader = GetTargetEchoShader(target.Uid);
            echoShader.SetParameter("time", (float)(elapsed % 1024d));
            echoShader.SetParameter("damage", target.DamageRatio);
            echoShader.SetParameter("visibility", visibility);
            echoShader.SetParameter("isMob", target.IsMob ? 1f : 0f);
            worldHandle.UseShader(echoShader);
            _spriteSystem.RenderSprite(
                new Entity<SpriteComponent>(target.Uid, target.Sprite),
                worldHandle,
                eyeRotation,
                target.Rotation,
                target.Position);
            worldHandle.UseShader(null);
            worldHandle.SetTransform(Matrix3x2.Identity);
        }

        if (revealType == SonarRevealType.RotatingSweep)
            DrawRadarSweep(worldHandle, radarOrigin, elapsed);
        else if (revealType == SonarRevealType.CatEars)
            DrawCatEarSweep(worldHandle, radarOrigin, eyeRotation.Theta, catEarSweepSettings);
    }

    private float GetInjuryChance(EntityUid uid)
    {
        if (!_entityManager.TryGetComponent(uid, out DamageableComponent? damageable))
        {
            return 0f;
        }

        var totalDamage = _damageable.GetTotalDamage((uid, damageable));
        if (_entityManager.TryGetComponent(uid, out MobThresholdsComponent? thresholds))
        {
            var criticalThreshold = _mobThresholds.GetThresholdForState(uid, MobState.Critical, thresholds);
            if (criticalThreshold > FixedPoint2.Zero)
                return GetDamageRatio(totalDamage, criticalThreshold);
        }

        return _entityManager.TryGetComponent(uid, out SonarDamageThresholdComponent? destructionThreshold)
            ? GetDamageRatio(totalDamage, destructionThreshold.DestructionThreshold)
            : 0f;
    }

    private float GetDamageRatio(EntityUid uid, EchoMode echoMode)
    {
        if (echoMode == EchoMode.Critical)
            return 1f;

        if (!_entityManager.TryGetComponent(uid, out DamageableComponent? damageable))
            return 0f;

        var totalDamage = _damageable.GetTotalDamage((uid, damageable));
        if (_entityManager.TryGetComponent(uid, out MobThresholdsComponent? thresholds))
        {
            var criticalThreshold = _mobThresholds.GetThresholdForState(uid, MobState.Critical, thresholds);
            if (criticalThreshold > FixedPoint2.Zero)
                return GetDamageRatio(totalDamage, criticalThreshold);
        }

        return _entityManager.TryGetComponent(uid, out SonarDamageThresholdComponent? destructionThreshold)
            ? GetDamageRatio(totalDamage, destructionThreshold.DestructionThreshold)
            : 0f;
    }

    private static float GetDamageRatio(FixedPoint2 totalDamage, FixedPoint2 maximumDamage) =>
        maximumDamage > FixedPoint2.Zero
            ? Math.Clamp((totalDamage / maximumDamage).Float(), 0f, 1f)
            : 0f;

    internal void ResetSleepSession()
    {
        _waveContextInitialized = false;
        _waveEmitter = default;
        _waveMapId = default;
        _lastWaveEmissionTime = double.NegativeInfinity;
        _lastWaveUpdateTime = double.NaN;
        _radarSweepStartTime = double.NaN;
        _lastCatEarEyeRotation = double.NaN;
        _catEarSweepSegments.Clear();
        foreach (var ear in _catEarSweeps)
        {
            ear.BaseAngleDegrees = 0d;
            ear.Direction = 0d;
            ear.SpeedDegreesPerSecond = 0d;
            ear.LastUpdateTime = double.NaN;
        }
        _lastRadarOrigin = default;
        _activeRevealType = null;
        _radarWaves.Clear();
        _sourceWaves.Clear();
        _sourceEmitterStates.Clear();
        _targetRevealStates.Clear();
        _previousTargetPositions.Clear();
        _visibleTargets.Clear();
        _currentTargets.Clear();
        _currentEmitters.Clear();
        _staleTargets.Clear();
        _staleEmitters.Clear();

        foreach (var shader in _targetEchoShaders.Values)
            shader.Dispose();

        _targetEchoShaders.Clear();
    }

    private void EnsureWaveContext(EntityUid emitter, MapId mapId, Vector2 radarOrigin, double elapsed)
    {
        if (_waveContextInitialized && _waveEmitter == emitter && _waveMapId == mapId)
            return;

        _waveContextInitialized = true;
        _waveEmitter = emitter;
        _waveMapId = mapId;
        _lastWaveEmissionTime = double.NegativeInfinity;
        _lastWaveUpdateTime = double.NaN;
        _radarSweepStartTime = elapsed;
        _lastCatEarEyeRotation = double.NaN;
        _catEarSweepSegments.Clear();
        foreach (var ear in _catEarSweeps)
        {
            ear.BaseAngleDegrees = 0d;
            ear.Direction = 0d;
            ear.SpeedDegreesPerSecond = 0d;
            ear.LastUpdateTime = double.NaN;
        }
        _lastRadarOrigin = radarOrigin;
        _activeRevealType = null;
        _radarWaves.Clear();
        _sourceWaves.Clear();
        _sourceEmitterStates.Clear();
        _currentEmitters.Clear();
        _targetRevealStates.Clear();
        _previousTargetPositions.Clear();
    }

    private void PruneExpiredWaves(double elapsed)
    {
        for (var i = _radarWaves.Count - 1; i >= 0; i--)
        {
            if (elapsed - _radarWaves[i].StartTime >= _radarWaves[i].LifetimeSeconds)
                _radarWaves.RemoveAt(i);
        }
    }

    private void PruneExpiredSourceWaves(double elapsed)
    {
        for (var i = _sourceWaves.Count - 1; i >= 0; i--)
        {
            if (elapsed - _sourceWaves[i].StartTime >= _sourceWaves[i].LifetimeSeconds)
                _sourceWaves.RemoveAt(i);
        }
    }

    private SonarRevealType GetRevealType(int configuredType)
    {
        switch (configuredType)
        {
            case 0:
                _lastLoggedInvalidRevealType = null;
                return SonarRevealType.ExpandingWaves;
            case 1:
                _lastLoggedInvalidRevealType = null;
                return SonarRevealType.RotatingSweep;
            case 2:
                _lastLoggedInvalidRevealType = null;
                return SonarRevealType.CatEars;
            default:
                if (_lastLoggedInvalidRevealType != configuredType)
                {
                    _log.GetSawmill("sleep.sonar").Warning(
                        $"Invalid light_sleep_sonar.reveal_type value {configuredType}; expected 0, 1, or 2. Using type 2.");
                    _lastLoggedInvalidRevealType = configuredType;
                }

                return SonarRevealType.CatEars;
        }
    }

    private void ProcessWaveHits(
        double elapsed,
        Vector2 radarOrigin,
        SonarRevealType revealType,
        double eyeRotation,
        CatEarSweepSettings catEarSweepSettings)
    {
        var hasPreviousFrame = !double.IsNaN(_lastWaveUpdateTime);
        var previousFrameTime = hasPreviousFrame ? _lastWaveUpdateTime : elapsed;
        var previousEyeRotation = double.IsNaN(_lastCatEarEyeRotation)
            ? eyeRotation
            : _lastCatEarEyeRotation;

        foreach (var target in _visibleTargets)
        {
            if (!_previousTargetPositions.TryGetValue(target.Uid, out var previousPosition) || !hasPreviousFrame)
            {
                _previousTargetPositions[target.Uid] = target.Position;
                if ((revealType == SonarRevealType.RotatingSweep &&
                     IsRadarSweepAligned(target.Position, radarOrigin, elapsed)) ||
                    (revealType == SonarRevealType.CatEars &&
                     IsCatEarSweepAligned(target.Position, radarOrigin, eyeRotation, catEarSweepSettings)))
                {
                    RevealTargetAt(target.Uid, elapsed);
                }

                continue;
            }

            var hasSweepHit = false;
            var sweepHitTime = 0d;
            if (revealType == SonarRevealType.RotatingSweep)
            {
                hasSweepHit = TryGetRadarSweepHitTime(
                    previousPosition,
                    target.Position,
                    _lastRadarOrigin,
                    radarOrigin,
                    previousFrameTime,
                    elapsed,
                    out sweepHitTime);
            }
            else if (revealType == SonarRevealType.CatEars)
            {
                hasSweepHit = TryGetCatEarSweepHitTime(
                    previousPosition,
                    target.Position,
                    _lastRadarOrigin,
                    radarOrigin,
                    previousFrameTime,
                    elapsed,
                    previousEyeRotation,
                    eyeRotation,
                    catEarSweepSettings,
                    out sweepHitTime);
            }

            if (hasSweepHit)
            {
                RevealTargetAt(target.Uid, sweepHitTime);
            }

            if (revealType == SonarRevealType.ExpandingWaves)
            {
                foreach (var wave in _radarWaves)
                    ProcessWaveHit(target.Uid, previousPosition, target.Position, wave, previousFrameTime, elapsed);
            }

            foreach (var wave in _sourceWaves)
            {
                if (wave.CanRevealTargets)
                    ProcessWaveHit(target.Uid, previousPosition, target.Position, wave, previousFrameTime, elapsed);
            }

            _previousTargetPositions[target.Uid] = target.Position;
        }

        _lastWaveUpdateTime = elapsed;
        _lastRadarOrigin = radarOrigin;
        _lastCatEarEyeRotation = eyeRotation;
    }

    private bool IsRadarSweepAligned(Vector2 targetPosition, Vector2 radarOrigin, double elapsed)
    {
        var offset = targetPosition - radarOrigin;
        if (offset.LengthSquared() <= 0.0001f)
            return true;

        var targetAngle = Math.Atan2(offset.Y, offset.X);
        return IsAlignedWithSweepLine(targetAngle, GetRadarSweepAngle(elapsed));
    }

    private bool IsCatEarSweepAligned(
        Vector2 targetPosition,
        Vector2 radarOrigin,
        double eyeRotation,
        CatEarSweepSettings catEarSweepSettings)
    {
        var offset = targetPosition - radarOrigin;
        if (offset.LengthSquared() <= 0.0001f)
            return true;

        var targetAngle = Math.Atan2(offset.Y, offset.X);
        foreach (var ear in _catEarSweeps)
        {
            for (var beam = 0; beam < 2; beam++)
            {
                if (IsAlignedWithSweepLine(
                        targetAngle,
                        GetCatEarBeamAngle(
                            ear.BaseAngleDegrees,
                            ear.IsLeftEar,
                            beam,
                            eyeRotation,
                            catEarSweepSettings)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsAlignedWithSweepLine(double targetAngle, double sweepAngle)
    {
        var angleOffset = targetAngle - sweepAngle;
        var angleDifference = Math.Atan2(Math.Sin(angleOffset), Math.Cos(angleOffset));
        return Math.Abs(angleDifference) <= RadarSweepBeamHalfAngleRadians;
    }

    private bool TryGetRadarSweepHitTime(
        Vector2 previousPosition,
        Vector2 currentPosition,
        Vector2 previousRadarOrigin,
        Vector2 currentRadarOrigin,
        double previousTime,
        double elapsed,
        out double hitTime)
    {
        hitTime = 0d;
        if (elapsed <= previousTime)
            return false;

        var previousOffset = previousPosition - previousRadarOrigin;
        var currentOffset = currentPosition - currentRadarOrigin;
        if (previousOffset.LengthSquared() <= 0.0001f || currentOffset.LengthSquared() <= 0.0001f)
        {
            hitTime = elapsed;
            return true;
        }

        var previousBearing = Math.Atan2(previousOffset.Y, previousOffset.X);
        var currentBearing = Math.Atan2(currentOffset.Y, currentOffset.X);
        var bearingDelta = Math.Atan2(
            Math.Sin(currentBearing - previousBearing),
            Math.Cos(currentBearing - previousBearing));
        return TryGetSweepLineHitTime(
            GetRadarSweepAngle(previousTime),
            GetRadarSweepAngle(elapsed),
            previousBearing,
            previousBearing + bearingDelta,
            previousTime,
            elapsed,
            out hitTime);
    }

    private bool TryGetCatEarSweepHitTime(
        Vector2 previousPosition,
        Vector2 currentPosition,
        Vector2 previousRadarOrigin,
        Vector2 currentRadarOrigin,
        double previousTime,
        double elapsed,
        double previousEyeRotation,
        double currentEyeRotation,
        CatEarSweepSettings catEarSweepSettings,
        out double hitTime)
    {
        hitTime = 0d;
        if (elapsed <= previousTime || _catEarSweepSegments.Count == 0)
            return false;

        var previousOffset = previousPosition - previousRadarOrigin;
        var currentOffset = currentPosition - currentRadarOrigin;
        if (previousOffset.LengthSquared() <= 0.0001f || currentOffset.LengthSquared() <= 0.0001f)
        {
            hitTime = elapsed;
            return true;
        }

        var previousBearing = Math.Atan2(previousOffset.Y, previousOffset.X);
        var currentBearing = Math.Atan2(currentOffset.Y, currentOffset.X);
        var bearingDelta = Math.Atan2(
            Math.Sin(currentBearing - previousBearing),
            Math.Cos(currentBearing - previousBearing));
        var eyeRotationDelta = Math.Atan2(
            Math.Sin(currentEyeRotation - previousEyeRotation),
            Math.Cos(currentEyeRotation - previousEyeRotation));
        var frameDuration = elapsed - previousTime;
        var latestHitTime = double.NegativeInfinity;

        foreach (var segment in _catEarSweepSegments)
        {
            var startFraction = (segment.StartTime - previousTime) / frameDuration;
            var endFraction = (segment.EndTime - previousTime) / frameDuration;
            var targetStartAngle = previousBearing + (bearingDelta * startFraction);
            var targetEndAngle = previousBearing + (bearingDelta * endFraction);
            var segmentStartEyeRotation = previousEyeRotation + (eyeRotationDelta * startFraction);
            var segmentEndEyeRotation = previousEyeRotation + (eyeRotationDelta * endFraction);
            for (var beam = 0; beam < 2; beam++)
            {
                if (TryGetSweepLineHitTime(
                        GetCatEarBeamAngle(
                            segment.StartBaseAngleDegrees,
                            segment.IsLeftEar,
                            beam,
                            segmentStartEyeRotation,
                            catEarSweepSettings),
                        GetCatEarBeamAngle(
                            segment.EndBaseAngleDegrees,
                            segment.IsLeftEar,
                            beam,
                            segmentEndEyeRotation,
                            catEarSweepSettings),
                        targetStartAngle,
                        targetEndAngle,
                        segment.StartTime,
                        segment.EndTime,
                        out var candidateHitTime))
                    latestHitTime = Math.Max(latestHitTime, candidateHitTime);
            }
        }

        if (double.IsNegativeInfinity(latestHitTime))
            return false;

        hitTime = latestHitTime;
        return true;
    }

    private static bool TryGetSweepLineHitTime(
        double previousSweepAngle,
        double currentSweepAngle,
        double previousTargetAngle,
        double currentTargetAngle,
        double previousTime,
        double elapsed,
        out double hitTime)
    {
        hitTime = 0d;
        var previousRelativeAngle = previousSweepAngle - previousTargetAngle;
        var currentRelativeAngle = currentSweepAngle - currentTargetAngle;
        var relativeDelta = currentRelativeAngle - previousRelativeAngle;
        if (Math.Abs(relativeDelta) < 0.000000000001d)
            return false;

        double crossingAngle;
        if (relativeDelta > 0d)
        {
            crossingAngle = (Math.Floor(previousRelativeAngle / FullRotationRadians) + 1d) * FullRotationRadians;
            if (crossingAngle > currentRelativeAngle)
                return false;
        }
        else
        {
            crossingAngle = (Math.Ceiling(previousRelativeAngle / FullRotationRadians) - 1d) * FullRotationRadians;
            if (crossingAngle < currentRelativeAngle)
                return false;
        }

        var hitFraction = (crossingAngle - previousRelativeAngle) / relativeDelta;
        hitTime = previousTime + ((elapsed - previousTime) * hitFraction);
        return true;
    }

    private double GetRadarSweepAngle(double elapsed) =>
        RadarSweepStartAngle + ((elapsed - _radarSweepStartTime) * _radarSweepRadiansPerSecond);

    private void ResetCatEarSweep(
        double elapsed,
        float baseSpeedDegreesPerSecond,
        CatEarSweepSettings catEarSweepSettings)
    {
        _catEarSweepSegments.Clear();
        foreach (var ear in _catEarSweeps)
        {
            ear.BaseAngleDegrees =
                _random.NextFloat() * catEarSweepSettings.GetSweepRangeDegrees(ear.IsLeftEar);
            ear.Direction = _random.NextFloat() < 0.5f ? -1d : 1d;
            ear.SpeedDegreesPerSecond = GetRandomizedCatEarSweepSpeed(baseSpeedDegreesPerSecond);
            ear.LastUpdateTime = elapsed;
        }
    }

    private double GetRandomizedCatEarSweepSpeed(float baseSpeedDegreesPerSecond) =>
        Math.Abs(baseSpeedDegreesPerSecond) *
        (1d - CatEarSweepSpeedVariation + (_random.NextFloat() * CatEarSweepSpeedVariation * 2d));

    private void UpdateCatEarSweep(
        double elapsed,
        float baseSpeedDegreesPerSecond,
        CatEarSweepSettings catEarSweepSettings)
    {
        _catEarSweepSegments.Clear();
        foreach (var ear in _catEarSweeps)
            UpdateCatEarSweep(
                ear,
                elapsed,
                baseSpeedDegreesPerSecond,
                catEarSweepSettings.GetSweepRangeDegrees(ear.IsLeftEar));
    }

    private void UpdateCatEarSweep(
        CatEarSweepState ear,
        double elapsed,
        float baseSpeedDegreesPerSecond,
        double sweepRangeDegrees)
    {
        if (elapsed <= ear.LastUpdateTime)
            return;

        ear.BaseAngleDegrees = Math.Clamp(ear.BaseAngleDegrees, 0d, sweepRangeDegrees);
        if (sweepRangeDegrees <= 0d)
        {
            ear.LastUpdateTime = elapsed;
            return;
        }

        if (baseSpeedDegreesPerSecond == 0f)
        {
            ear.SpeedDegreesPerSecond = 0d;
            ear.LastUpdateTime = elapsed;
            return;
        }

        if (ear.SpeedDegreesPerSecond <= 0d)
            ear.SpeedDegreesPerSecond = GetRandomizedCatEarSweepSpeed(baseSpeedDegreesPerSecond);

        var remainingTime = elapsed - ear.LastUpdateTime;
        var segmentStartTime = ear.LastUpdateTime;
        while (remainingTime > 0d)
        {
            var boundary = ear.Direction < 0d ? 0d : sweepRangeDegrees;
            var distanceToBoundary = Math.Abs(boundary - ear.BaseAngleDegrees);
            if (distanceToBoundary <= 0.0000000001d)
            {
                ear.BaseAngleDegrees = boundary;
                ear.Direction *= -1d;
                ear.SpeedDegreesPerSecond = GetRandomizedCatEarSweepSpeed(baseSpeedDegreesPerSecond);
                continue;
            }

            var timeToBoundary = distanceToBoundary / ear.SpeedDegreesPerSecond;
            var reachesBoundary = timeToBoundary <= remainingTime;
            var segmentDuration = reachesBoundary ? timeToBoundary : remainingTime;
            var startAngle = ear.BaseAngleDegrees;
            var endAngle = startAngle +
                           (ear.Direction * ear.SpeedDegreesPerSecond * segmentDuration);
            if (reachesBoundary)
                endAngle = boundary;

            var segmentEndTime = segmentStartTime + segmentDuration;
            _catEarSweepSegments.Add(new CatEarSweepSegment(
                ear.IsLeftEar,
                startAngle,
                endAngle,
                segmentStartTime,
                segmentEndTime));
            ear.BaseAngleDegrees = endAngle;
            remainingTime -= segmentDuration;
            segmentStartTime = segmentEndTime;

            if (reachesBoundary)
            {
                ear.Direction *= -1d;
                ear.SpeedDegreesPerSecond = GetRandomizedCatEarSweepSpeed(baseSpeedDegreesPerSecond);
            }
        }

        ear.LastUpdateTime = elapsed;
    }

    private static double GetCatEarBeamAngle(
        double baseAngleDegrees,
        bool isLeftEar,
        int beam,
        double eyeRotation,
        CatEarSweepSettings catEarSweepSettings)
    {
        var direction = catEarSweepSettings.GetSweepDirection(isLeftEar);
        var angleDegrees = catEarSweepSettings.StartAngleDegrees +
                           (direction * (baseAngleDegrees + (beam * catEarSweepSettings.LineSeparationDegrees)));
        return eyeRotation + (angleDegrees * DegreesToRadians);
    }

    private void RevealTargetAt(EntityUid target, double hitTime)
    {
        var startVisibility = 0f;
        if (_targetRevealStates.TryGetValue(target, out var previousState))
        {
            if (hitTime <= previousState.HitTime)
                return;

            startVisibility = GetRevealIntensity(previousState, hitTime);
        }

        _targetRevealStates[target] = new TargetRevealState(hitTime, startVisibility);
    }

    private void ProcessWaveHit(
        EntityUid target,
        Vector2 previousPosition,
        Vector2 currentPosition,
        SonarWave wave,
        double previousFrameTime,
        double elapsed)
    {
        if (wave.MapId != _waveMapId)
            return;

        var previousAge = Math.Clamp(previousFrameTime - wave.StartTime, 0d, wave.LifetimeSeconds);
        var currentAge = Math.Clamp(elapsed - wave.StartTime, 0d, wave.LifetimeSeconds);
        if (currentAge <= previousAge)
            return;

        var previousRadius = GetWaveRadius(wave, (float)(previousAge / wave.LifetimeSeconds));
        var currentRadius = GetWaveRadius(wave, (float)(currentAge / wave.LifetimeSeconds));
        var previousGap = Vector2.Distance(previousPosition, wave.Origin) - previousRadius;
        var currentGap = Vector2.Distance(currentPosition, wave.Origin) - currentRadius;
        if (previousGap < 0f || currentGap > 0f || previousGap == currentGap)
            return;

        var hitFraction = previousGap / (previousGap - currentGap);
        var hitTime = previousFrameTime + ((elapsed - previousFrameTime) * hitFraction);
        RevealTargetAt(target, hitTime);
    }

    private void UpdateSourceWaves(
        EntityUid uid,
        Vector2 position,
        MapId mapId,
        EchoMode echoMode,
        bool emitPositionalWave,
        double elapsed)
    {
        if (!_sourceEmitterStates.TryGetValue(uid, out var state) || state.MapId != mapId)
        {
            state = new SourceEmitterState(mapId, position);
            _sourceEmitterStates[uid] = state;
        }

        if (emitPositionalWave &&
            elapsed - state.LastStationaryWaveTime >= StationarySourceWaveIntervalSeconds)
        {
            EmitSourceWave(uid, position, mapId, echoMode, elapsed, false);
            state.LastStationaryWaveTime = elapsed;
        }

        if (Vector2.DistanceSquared(position, state.MovementWaveOrigin) >= SourceWaveMaximumRadiusSquared)
        {
            EmitSourceWave(uid, position, mapId, echoMode, elapsed, true);
            state.MovementWaveOrigin = position;
        }
    }

    private void EmitSourceWave(EntityUid uid, Vector2 position, MapId mapId, EchoMode echoMode, double elapsed, bool movement)
    {
        var injuryChance = echoMode == EchoMode.Critical ? 0f : GetInjuryChance(uid);
        var red = echoMode == EchoMode.Critical || (injuryChance > 0f && _random.NextFloat() < injuryChance);
        _sourceWaves.Add(movement
            ? new SonarWave(
                mapId,
                position,
                null,
                elapsed,
                MovingSourceWaveLifetimeSeconds,
                0f,
                SourceWaveMaximumRadius,
                MovingSourceWaveAlpha,
                true,
                red)
            : new SonarWave(
                mapId,
                position,
                uid,
                elapsed,
                StationarySourceWaveLifetimeSeconds,
                StationarySourceWaveStartRadius,
                SourceWaveMaximumRadius,
                StationarySourceWaveAlpha,
                false,
                red));

        if (movement)
            RevealTargetAt(uid, elapsed);
    }

    private void PruneSourceEmitterStates()
    {
        _staleEmitters.Clear();
        foreach (var uid in _sourceEmitterStates.Keys)
        {
            if (!_currentEmitters.Contains(uid))
                _staleEmitters.Add(uid);
        }

        foreach (var uid in _staleEmitters)
            _sourceEmitterStates.Remove(uid);
    }

    private void PruneTargetState()
    {
        _staleTargets.Clear();
        foreach (var uid in _previousTargetPositions.Keys)
        {
            if (!_currentTargets.Contains(uid))
                _staleTargets.Add(uid);
        }

        foreach (var uid in _staleTargets)
        {
            _previousTargetPositions.Remove(uid);
            _targetRevealStates.Remove(uid);
            if (_targetEchoShaders.Remove(uid, out var shader))
                shader.Dispose();
        }

        _staleTargets.Clear();
        foreach (var uid in _targetRevealStates.Keys)
        {
            if (!_currentTargets.Contains(uid))
                _staleTargets.Add(uid);
        }

        foreach (var uid in _staleTargets)
            _targetRevealStates.Remove(uid);
    }

    private int CountPropagatingWaves(MapId mapId, double elapsed)
    {
        var count = 0;
        foreach (var wave in _radarWaves)
        {
            var age = elapsed - wave.StartTime;
            if (wave.MapId == mapId && age >= 0d && age < wave.LifetimeSeconds)
                count++;
        }

        return count;
    }

    private void DrawSourceWaves(DrawingHandleWorld handle, MapId mapId, double elapsed)
    {
        foreach (var wave in _sourceWaves)
            DrawWave(handle, mapId, wave, elapsed);
    }

    private void DrawRadarSweep(DrawingHandleWorld handle, Vector2 origin, double elapsed)
    {
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
        DrawRadarSweepLine(handle, origin, GetRadarSweepAngle(elapsed));
    }

    private void DrawCatEarSweep(
        DrawingHandleWorld handle,
        Vector2 origin,
        double eyeRotation,
        CatEarSweepSettings catEarSweepSettings)
    {
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
        foreach (var ear in _catEarSweeps)
        {
            for (var beam = 0; beam < 2; beam++)
            {
                DrawRadarSweepLine(
                    handle,
                    origin,
                    GetCatEarBeamAngle(
                        ear.BaseAngleDegrees,
                        ear.IsLeftEar,
                        beam,
                        eyeRotation,
                        catEarSweepSettings));
            }
        }
    }

    private static void DrawRadarSweepLine(DrawingHandleWorld handle, Vector2 origin, double angle)
    {
        var normalizedAngle = (float)(angle % FullRotationRadians);
        var direction = new Vector2(MathF.Cos(normalizedAngle), MathF.Sin(normalizedAngle));
        handle.DrawLine(origin, origin + (direction * EchoRange), new Color(0.15f, 0.8f, 1f, 0.8f));
    }

    private void DrawWave(DrawingHandleWorld handle, MapId mapId, SonarWave wave, double elapsed)
    {
        var age = elapsed - wave.StartTime;
        if (wave.MapId != mapId || age < 0d || age >= wave.LifetimeSeconds)
            return;

        var phase = (float)(age / wave.LifetimeSeconds);
        var radius = GetWaveRadius(wave, phase);
        var pulse = wave.IsRed ? 0.65f + (0.35f * MathF.Sin((float) elapsed * 4f)) : 1f;
        var alpha = wave.MaximumAlpha * (1f - phase) * pulse;
        var color = wave.IsRed
            ? new Color(1f, 0.1f, 0.08f, alpha)
            : new Color(0.15f, 0.8f, 1f, alpha);
        handle.DrawCircle(GetWaveOrigin(wave), radius, color, false);
    }

    private Vector2 GetWaveOrigin(SonarWave wave)
    {
        if (wave.FollowEntity is { } uid &&
            _entityManager.TryGetComponent(uid, out TransformComponent? transform) &&
            transform.MapID == wave.MapId)
        {
            var (position, _) = _transform.GetWorldPositionRotation(transform);
            return position;
        }

        return wave.Origin;
    }

    private static float GetWaveRadius(SonarWave wave, float phase) =>
        wave.StartRadius + ((wave.MaximumRadius - wave.StartRadius) * phase);

    private float GetRevealIntensity(EntityUid uid, double elapsed)
    {
        if (!_targetRevealStates.TryGetValue(uid, out var revealState))
            return 0f;

        return GetRevealIntensity(revealState, elapsed);
    }

    private static float GetRevealIntensity(TargetRevealState revealState, double elapsed)
    {
        var timeSinceReveal = elapsed - revealState.HitTime;
        if (timeSinceReveal < 0d)
            return 0f;

        var fadeInProgress = (float)Math.Clamp(timeSinceReveal / VisibilityFadeInSeconds, 0d, 1d);
        var fadeInEase = fadeInProgress * fadeInProgress * (3f - (2f * fadeInProgress));
        var fadeIn = revealState.StartVisibility +
                     ((1f - revealState.StartVisibility) * fadeInEase);
        var fadeOutProgress = (float)Math.Clamp(timeSinceReveal / VisibilityFadeSeconds, 0d, 1d);
        var fadeOut = 1f - ((1f - MinimumVisibility) * fadeOutProgress);
        return fadeIn * fadeOut;
    }

    private ShaderInstance GetTargetEchoShader(EntityUid uid)
    {
        if (_targetEchoShaders.TryGetValue(uid, out var shader))
            return shader;

        shader = _echoShader.Duplicate();
        _targetEchoShaders.Add(uid, shader);
        return shader;
    }

    protected override void DisposeBehavior()
    {
        foreach (var shader in _targetEchoShaders.Values)
            shader.Dispose();

        _targetEchoShaders.Clear();
        _echoShader.Dispose();
        base.DisposeBehavior();
    }

    private EchoMode GetEchoMode(EntityUid uid)
    {
        if (_mobState.IsAlive(uid))
            return EchoMode.Active;

        if (_mobState.IsCritical(uid) || _mobState.IsSoftCritical(uid))
            return EchoMode.Critical;

        return _entityManager.TryGetComponent(uid, out ApcPowerReceiverComponent? power) &&
                power is { Powered: true, PowerDisabled: false }
                ? EchoMode.Active
                : EchoMode.None;
    }

    private enum EchoMode
    {
        None,
        Active,
        Critical
    }

    private enum SonarRevealType
    {
        ExpandingWaves = 0,
        RotatingSweep = 1,
        CatEars = 2
    }

    private readonly record struct TargetRevealState(double HitTime, float StartVisibility);

    private readonly record struct CatEarSweepSettings(
        double StartAngleDegrees,
        double LineSeparationDegrees)
    {
        private double GetSweepDegrees(bool isLeftEar) =>
            (isLeftEar ? 1d : -1d) * (180d - LineSeparationDegrees);

        public double GetSweepDirection(bool isLeftEar) =>
            GetSweepDegrees(isLeftEar) < 0d ? -1d : 1d;

        public double GetSweepRangeDegrees(bool isLeftEar) =>
            Math.Abs(GetSweepDegrees(isLeftEar));
    }

    private readonly record struct CatEarSweepSegment(
        bool IsLeftEar,
        double StartBaseAngleDegrees,
        double EndBaseAngleDegrees,
        double StartTime,
        double EndTime);

    private sealed class CatEarSweepState(bool isLeftEar)
    {
        public bool IsLeftEar { get; } = isLeftEar;
        public double BaseAngleDegrees;
        public double Direction;
        public double SpeedDegreesPerSecond;
        public double LastUpdateTime = double.NaN;
    }

    private readonly record struct SonarWave(
        MapId MapId,
        Vector2 Origin,
        EntityUid? FollowEntity,
        double StartTime,
        float LifetimeSeconds,
        float StartRadius,
        float MaximumRadius,
        float MaximumAlpha,
        bool CanRevealTargets,
        bool IsRed);

    private sealed class SourceEmitterState(MapId mapId, Vector2 position)
    {
        public MapId MapId { get; } = mapId;
        public Vector2 MovementWaveOrigin = position;
        public double LastStationaryWaveTime = double.NegativeInfinity;
    }

    private readonly record struct EchoTarget(
        EntityUid Uid,
        SpriteComponent Sprite,
        Vector2 Position,
        Angle Rotation,
        float DamageRatio,
        bool IsMob);
}
