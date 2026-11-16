using Content.Shared._Starlight.Ghost;
using Content.Shared.Actions;
using Content.Shared.Ghost;
using Content.Shared.Popups;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Ghost;

/// <summary>
/// Hides your own ghost on your screen only, for observers, admins and replay viewers who want a clear view.
/// GhostSystem re-shows the local ghost whenever ghost visibility changes, so this is enforced each frame.
/// Ghosts set to always visible (corporeal) are never hidden, matching GhostSystem.
/// </summary>
public sealed partial class GhostOwnSpriteSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly EntProtoId _actionProto = "ActionToggleOwnGhostSprite";

    private bool _hidden;

    // The ghost and button state last synced; a new ghost's button starts untoggled.
    private EntityUid? _synced;
    private bool _syncedHidden;

    [SubscribeLocalEvent]
    private void OnToggle(EntityUid uid, GhostComponent component, ToggleOwnGhostSpriteActionEvent args)
    {
        if (args.Handled || uid != _player.LocalEntity)
            return;

        args.Handled = true;
        if (component.AlwaysVisible)
        {
            _popup.PopupEntity(Loc.GetString("ghost-own-sprite-always-visible"), uid);
            return;
        }

        _hidden = !_hidden;
        _popup.PopupEntity(Loc.GetString(_hidden ? "ghost-own-sprite-hidden" : "ghost-own-sprite-shown"), uid);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } local || !TryComp<GhostComponent>(local, out var ghost))
            return;

        var hide = _hidden && !ghost.AlwaysVisible;
        if (_synced != local || _syncedHidden != hide)
            SyncAction(local, hide);

        if (hide && TryComp<SpriteComponent>(local, out var sprite) && sprite.Visible)
            _sprite.SetVisible((local, sprite), false);
    }

    // Keeps the button's toggled look matching whether the ghost is actually hidden.
    private void SyncAction(EntityUid ghost, bool hide)
    {
        var wasHidden = _syncedHidden;
        _syncedHidden = hide;

        foreach (var action in _actions.GetActions(ghost))
        {
            if (MetaData(action.Owner).EntityPrototype?.ID != _actionProto.Id)
                continue;

            _actions.SetToggled((EntityUid?) action.Owner, hide);
            _synced = ghost;
            break;
        }

        // Shown again by the toggle or by becoming always visible: restore the sprite once.
        if (wasHidden && !hide && TryComp<SpriteComponent>(ghost, out var sprite))
            _sprite.SetVisible((ghost, sprite), true);
    }
}
