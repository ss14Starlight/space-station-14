using Content.Shared._Starlight.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._Starlight.Light;

/// <summary>
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
