using Content.Shared._Starlight.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._Starlight.Light;

/// <summary>
/// Turns the client's lighting effects on and off. A tonemap squeezes light that is too bright back into a
/// range the screen can show, so a spot lit by several lamps looks a bit brighter instead of turning flat white.
/// Adds the <see cref="LightTonemapOverlay"/> while <see cref="StarlightCCVars.LightTonemap"/> is on,
/// and the <see cref="ScreenPostProcessOverlay"/>, which checks its own cvars.
/// </summary>
public sealed partial class LightTonemapSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, StarlightCCVars.LightTonemap, OnTonemapChanged, true);
        _overlay.AddOverlay(new ScreenPostProcessOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<LightTonemapOverlay>();
        _overlay.RemoveOverlay<ScreenPostProcessOverlay>();
    }

    private void OnTonemapChanged(bool enabled)
    {
        if (!enabled)
            _overlay.RemoveOverlay<LightTonemapOverlay>();
        else if (!_overlay.HasOverlay<LightTonemapOverlay>())
            _overlay.AddOverlay(new LightTonemapOverlay());
    }
}
