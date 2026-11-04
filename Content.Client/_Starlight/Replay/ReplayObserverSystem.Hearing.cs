using Content.Client.Replay.Spectator;
using Content.Shared.Chat;
using Content.Shared.Ghost;

namespace Content.Client._Starlight.Replay;

// Replays record all chat, so ghost hearing is applied on playback.
public sealed partial class ReplayObserverSystem
{
    private bool _hearAll = true;

    private void OnToggleHearing(EntityUid uid, ReplaySpectatorComponent component, ToggleGhostHearingActionEvent args)
    {
        if (args.Handled || !IsReplayActive)
            return;

        args.Handled = true;
        _hearAll = !_hearAll;

        // Toggled means hearing is off, as in live.
        if (TryComp<GhostComponent>(uid, out var ghost))
            _actions.SetToggled(ghost.ToggleGhostHearingActionEntity, !_hearAll);

        var msg = _hearAll
            ? Loc.GetString("ghost-gui-toggle-hearing-popup-on")
            : Loc.GetString("ghost-gui-toggle-hearing-popup-off");
        _popup.PopupEntity(msg, uid);
    }

    public bool ShouldHearChat(ChatMessage message)
    {
        if (_hearAll || !IsReplayActive)
            return true;

        float range;
        switch (message.Channel)
        {
            case ChatChannel.Local:
            case ChatChannel.Emotes:
                range = SharedChatSystem.VoiceRange;
                break;
            case ChatChannel.Whisper:
                range = SharedChatSystem.WhisperMuffledRange;
                break;
            default:
                return true;
        }

        if (_player.LocalEntity is not { } listener)
            return true;

        var sender = GetEntity(message.SenderEntity);

        if (!Exists(sender))
            return true;

        var senderPos = _transform.GetMapCoordinates(sender);
        var listenerPos = _transform.GetMapCoordinates(listener);

        if (senderPos.MapId != listenerPos.MapId)
            return false;

        return (senderPos.Position - listenerPos.Position).Length() < range;
    }
}
