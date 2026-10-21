using Content.Shared._Starlight.Admeme.Jumpscare;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Admeme.Jumpscare;

/// <summary>
/// For showing jumpscares. The actual drawing is done by <see cref="JumpscareUIController"/>.
/// </summary>
public sealed partial class JumpscareSystem : SharedJumpscareSystem
{
    [Dependency] private IUserInterfaceManager _ui = null!;

    private JumpscareUIController _controller = null!;

    public override void Initialize()
    {
        base.Initialize();
        _controller = _ui.GetUIController<JumpscareUIController>();
    }

    [SubscribeNetworkEvent]
    private void OnDoJumpscare(DoJumpscareEvent ev)
    {
        if (ev.IgnoreDelay)
            _controller.PlayNow(ev.Jumpscare);
        else
            _controller.Play(ev.Jumpscare);
    }
}
