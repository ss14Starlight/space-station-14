using Content.Shared._Starlight.CCVar;
using Content.Shared.Tips;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

// ReSharper disable once CheckNamespace
namespace Content.Client.Tips;

public sealed partial class TipsSystem : SharedTipsSystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private TippyUIController _tui => _ui.GetUIController<TippyUIController>();

    private bool _tippyEnabled = true;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, StarlightCCVars.TippyClientEnabled, OnTippyEnabledChanged, true);
    }

    private void OnTippyEnabledChanged(bool enabled)
    {
        _tippyEnabled = enabled;
        if (!enabled)
            _tui.ClearTippy();
    }

    [SubscribeNetworkEvent]
    private void OnTippyEvent(TippyEvent ev)
    {
        if (_tippyEnabled)
            _tui.AddTippyToQueue(ev);
    }
}
