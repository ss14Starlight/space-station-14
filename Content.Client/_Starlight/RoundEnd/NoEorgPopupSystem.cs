using Content.Shared.GameTicking;
using Content.Shared._Starlight.CCVar;
using Robust.Shared.Configuration;

namespace Content.Client._Starlight.RoundEnd;

public sealed partial class NoEorgPopupSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;

    private NoEorgPopup? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RoundEndMessageEvent>(OnRoundEnd);
    }

    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        if (_cfg.GetCVar(StarlightCCVars.SkipRoundEndNoEorgPopup) || _cfg.GetCVar(StarlightCCVars.RoundEndNoEorgPopup) == false)
            return;

        OpenNoEorgPopup();
    }

    private void OpenNoEorgPopup()
    {
        if (_window != null)
            return;

        _window = new NoEorgPopup();
        _window.OpenCentered();
        _window.OnClose += () => _window = null;
    }
}
