using System.Numerics;
using Content.Client.Power.Components;
using Content.Shared._Starlight.Sleepiness.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Sleepiness;

public sealed partial class LightSleepSonarOverlay : Robust.Client.Graphics.Overlay
{
    private static readonly ProtoId<ShaderPrototype> _echoShaderId = "LightSleepSonarEcho";

    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const float EchoRange = 10f;
    private const float EchoRangeSquared = EchoRange * EchoRange;
    private const float WavePeriodSeconds = 4.4f;
    private const float MinimumWaveRadius = 0.25f;
    private const float MaximumWaveRadius = 1.25f;
    private const float MaximumWaveAlpha = 0.275f;
    private const int WaveCount = 2;
    private const float MovingWaveRateMultiplier = 1.5f;
    private const float MovingWaveStrengthMultiplier = 1.5f;
    private const float MinimumMovementSpeed = 0.1f;
    private const double MaximumMovementSampleGapSeconds = 0.25d;
    private const double MovementGraceSeconds = 0.15d;

    private readonly ShaderInstance _echoShader;
    private readonly TransformSystem _transform;
    private readonly SharedContainerSystem _container;
    private readonly SpriteSystem _spriteSystem;
    private readonly LightSleepSystem _lightSleep;
    private readonly MobStateSystem _mobState;
    private readonly Dictionary<EntityUid, EchoMotionState> _motionStates = new();
    private readonly HashSet<EntityUid> _currentSources = new();
    private readonly List<EntityUid> _staleSources = new();

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
            args.Viewport.Eye is not { } eye)
        {
            return;
        }

        var worldHandle = args.WorldHandle;
        var eyePosition = eye.Position.Position;
        var eyeRotation = eye.Rotation;
        var elapsed = _timing.RealTime.TotalSeconds;
        _echoShader.SetParameter("time", (float)(elapsed % 1024d));
        worldHandle.UseShader(null);
        worldHandle.SetTransform(Matrix3x2.Identity);
        _currentSources.Clear();

        var query = _entityManager.EntityQueryEnumerator<SpriteComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sprite, out var meta, out var xform))
        {
            if (uid == player ||
                !sprite.Visible ||
                xform.MapID != args.MapId ||
                _container.IsEntityInContainer(uid, meta))
            {
                continue;
            }

            var (position, rotation) = _transform.GetWorldPositionRotation(xform);
            if (Vector2.DistanceSquared(position, eyePosition) > EchoRangeSquared)
                continue;

            var echoMode = GetEchoMode(uid);
            if (echoMode == EchoMode.None)
                continue;

            worldHandle.SetTransform(Matrix3x2.Identity);
            _currentSources.Add(uid);
            var moving = IsMoving(uid, position, elapsed);
            DrawWaves(worldHandle, position, elapsed, moving);
            worldHandle.UseShader(_echoShader);
            _spriteSystem.RenderSprite(
                new Entity<SpriteComponent>(uid, sprite),
                worldHandle,
                eyeRotation,
                rotation,
                position);
            worldHandle.UseShader(null);
            worldHandle.SetTransform(Matrix3x2.Identity);
        }

        _staleSources.Clear();
        foreach (var uid in _motionStates.Keys)
        {
            if (!_currentSources.Contains(uid))
                _staleSources.Add(uid);
        }

        foreach (var uid in _staleSources)
            _motionStates.Remove(uid);
    }

    private bool IsMoving(EntityUid uid, Vector2 position, double elapsed)
    {
        if (!_motionStates.TryGetValue(uid, out var previous))
        {
            _motionStates.Add(uid, new EchoMotionState(position, elapsed, double.NegativeInfinity));
            return false;
        }

        var timeSinceSample = elapsed - previous.SampleTime;
        var lastMovementTime = previous.LastMovementTime;
        if (timeSinceSample is <= 0d or > MaximumMovementSampleGapSeconds)
        {
            lastMovementTime = double.NegativeInfinity;
        }
        else
        {
            var minimumDistance = MinimumMovementSpeed * (float) timeSinceSample;
            if (Vector2.DistanceSquared(position, previous.Position) > minimumDistance * minimumDistance)
                lastMovementTime = elapsed;
        }

        _motionStates[uid] = new EchoMotionState(position, elapsed, lastMovementTime);
        return elapsed - lastMovementTime <= MovementGraceSeconds;
    }

    private static void DrawWaves(DrawingHandleWorld handle, Vector2 position, double elapsed, bool moving)
    {
        var wavePeriod = moving ? WavePeriodSeconds / MovingWaveRateMultiplier : WavePeriodSeconds;
        var waveAlpha = moving ? MaximumWaveAlpha * MovingWaveStrengthMultiplier : MaximumWaveAlpha;
        var phaseOffset = (MathF.Sin((position.X * 12.9898f) + (position.Y * 78.233f)) + 1f) * 0.5f;
        for (var wave = 0; wave < WaveCount; wave++)
        {
            var phase = (float)(((elapsed / wavePeriod) + phaseOffset + (wave / (float) WaveCount)) % 1d);
            var radius = MinimumWaveRadius + ((MaximumWaveRadius - MinimumWaveRadius) * phase);
            var alpha = waveAlpha * (1f - phase);
            handle.DrawCircle(position, radius, new Color(0.15f, 0.8f, 1f, alpha), false);
        }
    }

    private EchoMode GetEchoMode(EntityUid uid)
    {
        if (_mobState.IsAlive(uid))
            return EchoMode.Active;

        return _entityManager.TryGetComponent(uid, out ApcPowerReceiverComponent? power) &&
                power is { Powered: true, PowerDisabled: false }
                ? EchoMode.Active
                : EchoMode.None;
    }

    private enum EchoMode
    {
        None,
        Active,
    }

    private readonly record struct EchoMotionState(Vector2 Position, double SampleTime, double LastMovementTime);
}
