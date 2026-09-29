using Content.Shared.Tips;
using Robust.Client.UserInterface; // Starlight

namespace Content.Client.Tips;

// Starlight begin - Move the tippy event subscription to somewhere it makes sense to be in, not in a fucking UI controller.
public sealed partial class TipsSystem : SharedTipsSystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;

    private TippyUIController _tui => _ui.GetUIController<TippyUIController>();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<TippyEvent>(OnTippyEvent);
    }

    private void OnTippyEvent(TippyEvent ev) =>
        _tui.AddTippyToQueue(ev);
}
// Starlight end
