using Content.Client.Replay.Spectator;
using Content.Shared._Starlight.Replay;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Replay;

// Hides the observer's own ghost. GhostSystem re-shows the local ghost whenever ghost visibility changes, so this is
// enforced each frame.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly EntProtoId _ownSpriteActionProto = "ActionReplayToggleOwnSprite";

    private bool _ownSpriteHidden;
    private EntityUid? _ownSpriteAction;

    private void OnToggleOwnSprite(EntityUid uid, ReplaySpectatorComponent component, ReplayToggleOwnSpriteActionEvent args)
    {
        if (args.Handled || !IsReplayActive)
            return;

        args.Handled = true;
        _ownSpriteHidden = !_ownSpriteHidden;
        _actions.SetToggled(_ownSpriteAction, _ownSpriteHidden);

        if (!_ownSpriteHidden && TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.SetVisible((uid, sprite), true);

        var msg = _ownSpriteHidden
            ? Loc.GetString("replay-observer-own-sprite-hidden")
            : Loc.GetString("replay-observer-own-sprite-shown");
        _popup.PopupEntity(msg, uid);
    }

    private void AddOwnSpriteAction(EntityUid observer)
    {
        _ownSpriteAction = null;
        _actions.AddAction(observer, ref _ownSpriteAction, _ownSpriteActionProto);
        _actions.SetToggled(_ownSpriteAction, _ownSpriteHidden);
    }

    private void UpdateOwnSprite()
    {
        if (!_ownSpriteHidden
            || _observer is not { } observer
            || observer != _player.LocalEntity
            || !TryComp<SpriteComponent>(observer, out var sprite)
            || !sprite.Visible)
        {
            return;
        }

        _sprite.SetVisible((observer, sprite), false);
    }
}
