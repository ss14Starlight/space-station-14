using Content.Shared._Starlight.CCVar;
using Content.Shared.Tips;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

namespace Content.Client.Tips;

// Starlight begin - Move the tippy event subscription to somewhere it makes sense to be in, not in a fucking UI controller.
public sealed partial class TipsSystem : SharedTipsSystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private TippyUIController _tui => _ui.GetUIController<TippyUIController>();

    private bool _tippyEnabled = true;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<TippyEvent>(OnTippyEvent);
        Subs.CVar(_cfg, StarlightCCVars.TippyClientEnabled, value => _tippyEnabled = value, true);
    }

    private void OnTippyEvent(TippyEvent ev)
    {
        if (_tippyEnabled)
            _tui.AddTippyToQueue(ev);
    }
}
// Starlight end
