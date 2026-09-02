using Content.Shared._Starfall.Traits.Assorted;
using Content.Shared.CCVar;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Starfall.Traits.Assorted;

/// <summary>
/// Manages the migraine overlay effect for players with the migraine status effect.
/// </summary>
public sealed partial class MigraineOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> AuraShader = "MigraineAura";

    [Dependency]
    private IEntityManager _entityManager = null!;

    [Dependency]
    private IPlayerManager _playerManager = null!;

    [Dependency]
    private IPrototypeManager _prototypeManager = null!;

    [Dependency]
    private IConfigurationManager _configuration = null!;

    private readonly StatusEffectsSystem _statusEffects;
    private readonly ShaderInstance _auraShader;

    public override bool RequestScreenTexture => true;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private const float BlurryMagnitude = 4f;
    private const float RampUpSpeed = 0.5f;
    private float _currentBlur;
    private const float RampDownSpeed = 2f;

    public MigraineOverlay()
    {
        IoCManager.InjectDependencies(this);

        _statusEffects = _entityManager.System<StatusEffectsSystem>();

        _auraShader = _prototypeManager.Index(AuraShader).InstanceUnique();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var frameTime = args.DeltaSeconds;
        var player = _playerManager.LocalEntity;

        if (player != null && _statusEffects.HasStatusEffect(player.Value, MigraineEffectComponent.Prototype))
        {
            var rampFactor = Math.Clamp(frameTime * RampUpSpeed, 0f, 1f);

            _currentBlur = MathHelper.Lerp(_currentBlur, BlurryMagnitude, rampFactor);

            return;
        }

        if (_currentBlur <= 0.001f)
        {
            ResetVisualState();
            return;
        }

        var fadeFactor = Math.Clamp(
            frameTime * MathF.Max(RampDownSpeed, 0.01f),
            0f,
            1f);

        _currentBlur = MathHelper.Lerp(
            _currentBlur,
            0f,
            fadeFactor);

        if (_currentBlur <= 0.001f)
        {
            ResetVisualState();
        }
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var player = _playerManager.LocalEntity;

        if (player == null)
            return false;

        if (!_entityManager.TryGetComponent<EyeComponent>(player.Value, out var eye))
            return false;

        if (args.Viewport.Eye != eye.Eye)
            return false;

        return MathF.Max(_currentBlur, 0f) > 0.001f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var player = _playerManager.LocalEntity;

        if (player == null)
            return;

        var strength = MathF.Max(_currentBlur, 0f);

        if (strength <= 0.001f)
            return;

        strength = Math.Clamp(strength, 0f, BlurryVisionComponent.MaxMagnitude);

        var normalized = MathF.Pow(MathF.Min(strength / BlurryVisionComponent.MaxMagnitude, 1f), BlurryVisionComponent.DefaultCorrectionPower);

        DrawAura(args.WorldHandle, args.WorldBounds, normalized, _configuration.GetCVar(CCVars.ReducedMotion));
    }

    /// Draws the migraine effect, optionally without motion blur if reduced motion is enabled.
    private void DrawAura(DrawingHandleWorld worldHandle, Box2Rotated viewport, float strength, bool reducedMotion)
    {
        if (ScreenTexture == null)
            return;

        var auraStrength = Math.Clamp(strength * 2.5f, 0f, 1f);

        _auraShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _auraShader.SetParameter("Strength", auraStrength);
        _auraShader.SetParameter("ReducedMotion", reducedMotion ? 1f : 0f);

        worldHandle.UseShader(_auraShader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null);
    }

    private void ResetVisualState()
    {
        _currentBlur = 0f;
    }
}
