using Content.Client.Resources;
using Content.Shared._Daiquiri.CCVar;
using Content.Shared._Daiquiri.Planets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Daiquiri.Planets;

public sealed partial class PlanetDiscoverySystem : EntitySystem
{
    private const string BannerFont = "/Fonts/_Daiquiri/Cormorant-Regular.ttf";
    private const int BannerFontSize = 64;

    private static readonly SoundSpecifier DiscoverySound = new SoundPathSpecifier("/Audio/_Daiquiri/Planets/planet_discovery.ogg");

    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private PlanetDiscoveryOverlay? _overlay;
    private bool _soundEnabled = true;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PlanetDiscoveredEvent>(OnPlanetDiscovered);
        Subs.CVar(_cfg, DaiquiriCCVars.PlanetDiscoverySoundEnabled, OnSoundEnabledChanged, true);
    }

    private void OnSoundEnabledChanged(bool enabled)
    {
        _soundEnabled = enabled;
    }

    private void OnPlanetDiscovered(PlanetDiscoveredEvent ev)
    {
        _overlay ??= new PlanetDiscoveryOverlay(
            _timing,
            _cache.GetFont(BannerFont, BannerFontSize));

        if (!_overlays.HasOverlay<PlanetDiscoveryOverlay>())
            _overlays.AddOverlay(_overlay);

        _overlay.Show(ev.Name, ev.Accent);

        if (_soundEnabled)
            _audio.PlayGlobal(DiscoverySound, Filter.Local(), false);
    }
}