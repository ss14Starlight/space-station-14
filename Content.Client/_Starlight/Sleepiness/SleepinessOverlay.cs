using Content.Shared._Starlight.Sleepiness.Components;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Sleepiness;

public sealed partial class SleepinessOverlay : Robust.Client.Graphics.Overlay
{
    private static readonly ProtoId<ShaderPrototype> _shaderId = "Drowsiness";
    private static readonly ProtoId<ShaderPrototype> _circleMaskShaderId = "CircleMask";

    // Required IoC-injected overlay dependencies.
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IEntitySystemManager _systemManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _logManager = default!;

    private readonly StatusEffectsSystem _statusEffects;
    private readonly ShaderInstance _shader;
    private readonly ShaderInstance _circleMaskShader;
    private const float IncreaseSmoothingSpeed = 3f;
    private const float DecreaseSmoothingSpeed = 8f;
    private const float VisualEpsilon = 0.001f;
    private const float DarknessCurveExponent = 1.35f;
    private const float MaximumBlurStrength = 0.2f;
    private const float MinimumBlurStrength = 0.01f;
    private const float CircleRadiusAtMinimumDarkness = 180f;
    private const float CircleRadiusAtMaximumDarkness = 60f;
    private static readonly TimeSpan _statusEffectGracePeriod = TimeSpan.FromSeconds(0.35);
    private TimeSpan _lastStatusEffectTime;
    private float _visualRatio;
    private float _lastTargetRatio;
    private bool _hasRecentStatusEffect;

    public bool IsAtRest => _visualRatio <= VisualEpsilon;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public SleepinessOverlay()
    {
        IoCManager.InjectDependencies(this);
        _statusEffects = _systemManager.GetEntitySystem<StatusEffectsSystem>();
        _shader = _prototypeManager.Index(_shaderId).InstanceUnique();
        _circleMaskShader = _prototypeManager.Index(_circleMaskShaderId).InstanceUnique();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var targetRatio = 0f;
        if (_playerManager.LocalEntity is { } player &&
            _statusEffects.TryGetMaxTime<SleepinessStatusEffectComponent>(player, out var effect) &&
            effect.EndEffectTime is { } endTime &&
            _entityManager.TryGetComponent(effect.EffectEnt, out SleepinessStatusEffectComponent? sleepiness))
        {
            var threshold = (float)sleepiness.SleepThreshold.TotalSeconds;
            if (threshold > 0f)
            {
                var remaining = (float)(endTime - _timing.CurTime).TotalSeconds;
                targetRatio = Math.Clamp(remaining / threshold, 0f, 1f);
                _lastTargetRatio = targetRatio;
                _lastStatusEffectTime = _timing.CurTime;
                _hasRecentStatusEffect = true;
            }
        }

        if (_hasRecentStatusEffect)
        {
            var statusEffectAge = _timing.CurTime - _lastStatusEffectTime;
            if (statusEffectAge <= _statusEffectGracePeriod)
                targetRatio = _lastTargetRatio;
            else
                _hasRecentStatusEffect = false;
        }

        var smoothingSpeed = targetRatio > _visualRatio
            ? IncreaseSmoothingSpeed
            : DecreaseSmoothingSpeed;
        var smoothing = 1f - MathF.Exp(-smoothingSpeed * args.DeltaSeconds);
        _visualRatio = MathHelper.Lerp(_visualRatio, targetRatio, smoothing);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_playerManager.LocalEntity is not { } player)
            return false;

        if (!_entityManager.TryGetComponent(player, out EyeComponent? eye))
            return false;

        if (args.Viewport.Eye != eye.Eye)
            return false;


        if (_visualRatio <= VisualEpsilon)
            return false;

        var darknessRatio = MathF.Pow(_visualRatio, DarknessCurveExponent);
        var blurStrength = darknessRatio * MaximumBlurStrength;
        if (blurStrength < MinimumBlurStrength)
            return false;

        _shader.SetParameter("Strength", blurStrength);
        _circleMaskShader.SetParameter("Zoom", eye.Zoom.X);
        _circleMaskShader.SetParameter("CircleRadius",
            MathHelper.Lerp(CircleRadiusAtMinimumDarkness, CircleRadiusAtMaximumDarkness, darknessRatio));

        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _circleMaskShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        args.WorldHandle.UseShader(_shader);
        args.WorldHandle.DrawRect(args.WorldBounds, Color.White);
        // Keep the circle mask as the final pass so the area outside the center stays darkened.
        args.WorldHandle.UseShader(_circleMaskShader);
        args.WorldHandle.DrawRect(args.WorldBounds, Color.White);
        args.WorldHandle.UseShader(null);
    }
}
