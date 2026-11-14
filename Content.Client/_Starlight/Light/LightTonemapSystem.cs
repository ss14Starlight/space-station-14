using Content.Shared._Starlight.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._Starlight.Light;

/// <summary>
/// Turns the client's lighting effects on and off. A tonemap squeezes light that is too bright back into a
/// range the screen can show, so a spot lit by several lamps looks a bit brighter instead of turning flat white.
/// Adds the <see cref="LightTonemapOverlay"/> while <see cref="StarlightCCVars.LightTonemap"/> is on or
/// <see cref="StarlightCCVars.LightBoost"/> is above 1,
/// the <see cref="GlobalIlluminationOverlay"/> while <see cref="StarlightCCVars.GlobalIllumination"/> is on,
/// and the <see cref="ScreenPostProcessOverlay"/>, which checks its own cvars.
/// </summary>
public sealed partial class LightTonemapSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, StarlightCCVars.LightTonemap, _ => UpdateTonemapOverlay());
        Subs.CVar(_cfg, StarlightCCVars.LightBoost, _ => UpdateTonemapOverlay());
        Subs.CVar(_cfg, StarlightCCVars.GlobalIllumination, OnGlobalIlluminationChanged, true);
        UpdateTonemapOverlay();
        _overlay.AddOverlay(new ScreenPostProcessOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<LightTonemapOverlay>();
        _overlay.RemoveOverlay<GlobalIlluminationOverlay>();
        _overlay.RemoveOverlay<ScreenPostProcessOverlay>();
    }

    private void OnGlobalIlluminationChanged(bool enabled)
    {
        if (!enabled)
            _overlay.RemoveOverlay<GlobalIlluminationOverlay>();
        else if (!_overlay.HasOverlay<GlobalIlluminationOverlay>())
            _overlay.AddOverlay(new GlobalIlluminationOverlay());
    }

    private void UpdateTonemapOverlay()
    {
        var enabled = _cfg.GetCVar(StarlightCCVars.LightTonemap)
            || _cfg.GetCVar(StarlightCCVars.LightBoost) > LightTonemapOverlay.MinLightBoost;
        if (!enabled)
            _overlay.RemoveOverlay<LightTonemapOverlay>();
        else if (!_overlay.HasOverlay<LightTonemapOverlay>())
            _overlay.AddOverlay(new LightTonemapOverlay());
    }
}
