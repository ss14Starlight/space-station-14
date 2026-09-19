using Content.Client._Functional.TutorialServer.UI;
using Content.Shared._Functional.TutorialServer;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Functional.TutorialServer;

/// <summary>
/// Owns the on-screen control-hint banner — the tutorial's only popup surface.
/// The server pushes one short markup line per sub-goal; keybind tags in it resolve here against
/// the local player's bindings, so the hint always names the key they actually have bound.
/// </summary>
public sealed partial class TutorialStepHudSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IPlayerManager _player = default!;

    private TutorialControlHint? _hint;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<TutorialControlHintEvent>(OnControlHint);
        _player.LocalPlayerDetached += OnLocalPlayerDetached;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _player.LocalPlayerDetached -= OnLocalPlayerDetached;

        _hint?.Orphan();
        _hint = null;
    }

    private void OnControlHint(TutorialControlHintEvent ev)
    {
        if (!ev.Show)
        {
            _hint?.SetHint(string.Empty);
            return;
        }

        EnsureHint().SetHint(ev.Markup);
    }

    private void OnLocalPlayerDetached(EntityUid uid)
    {
        // Ghosting mid-tutorial ends the session; don't leave a stale instruction on screen.
        _hint?.SetHint(string.Empty);
    }

    private TutorialControlHint EnsureHint()
    {
        if (_hint != null)
            return _hint;

        _hint = new TutorialControlHint();
        _ui.PopupRoot.AddChild(_hint);
        LayoutContainer.SetAnchorPreset(_hint, LayoutContainer.LayoutPreset.Wide);

        return _hint;
    }
}
