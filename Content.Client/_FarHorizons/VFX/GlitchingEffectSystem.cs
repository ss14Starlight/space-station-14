using Content.Shared._FarHorizons.VFX;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._FarHorizons.VFX;

public sealed class GlitchingEffectSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;

    private GlitchingEffectOverlay _overlay = default!;

    private const int GlitchBaseSteps = 30;
    private const float VignetteBasePower = 1.3f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GlitchingEffectComponent, ComponentInit>(OnGlitchInit);
        SubscribeLocalEvent<GlitchingEffectComponent, ComponentShutdown>(OnGlitchShutdown);

        SubscribeLocalEvent<GlitchingEffectComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<GlitchingEffectComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        _overlay = new();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_entMan.TryGetComponent<GlitchingEffectComponent>(_player.LocalEntity, out var glitch) ||
            ((_timing.CurTime > glitch.FinishAt ||
            _timing.CurTime < glitch.StartAt) && glitch.Animated)) 
            return;

        if (!glitch.Animated)
        {
            glitch.Intensity = 1f;
            _overlay.GlitchSteps = (int)(GlitchBaseSteps * glitch.Intensity);
            _overlay.VignettePower = VignetteBasePower * glitch.Intensity;
            return;
        }

        var animDuration = glitch.FinishAt - glitch.StartAt;

        var rampPct = glitch.RampDuration / animDuration;
        var t = (_timing.CurTime - glitch.StartAt) / animDuration;

        if (t < rampPct)
            glitch.Intensity = (float)(t / rampPct);
        else if (t > 1 - rampPct)
            glitch.Intensity = (float)((1.0f - t) / rampPct);
        else
            glitch.Intensity = 1f;

        _overlay.GlitchSteps = (int)(GlitchBaseSteps * glitch.Intensity);
        _overlay.VignettePower = VignetteBasePower * glitch.Intensity;
    }

    private void OnGlitchInit(Entity<GlitchingEffectComponent> ent, ref ComponentInit args)
    {
        if (_player.LocalEntity == ent)
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnGlitchShutdown(Entity<GlitchingEffectComponent> ent, ref ComponentShutdown args)
    {
        if (_player.LocalEntity == ent)
            _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnPlayerAttached(Entity<GlitchingEffectComponent> ent, ref LocalPlayerAttachedEvent args) => 
        _overlayMan.AddOverlay(_overlay);

    private void OnPlayerDetached(Entity<GlitchingEffectComponent> ent, ref LocalPlayerDetachedEvent args) => 
        _overlayMan.RemoveOverlay(_overlay);
}