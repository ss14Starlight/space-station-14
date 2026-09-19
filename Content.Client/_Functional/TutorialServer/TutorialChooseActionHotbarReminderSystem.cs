using Content.Client.Actions;
using Content.Client.UserInterface.Systems.Actions;
using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.Chat;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Functional.TutorialServer;

/// <summary>
/// While the player still has Choose-a-tutorial but cleared it off the hotbar, posts a chat tip
/// immediately and every 60 seconds directing them to the actions menu (their bound key) to drag it back.
/// </summary>
public sealed partial class TutorialChooseActionHotbarReminderSystem : EntitySystem
{
    private static readonly EntProtoId TutorialChooseRoleActionProto = "ActionTutorialChooseRole";
    private static readonly TimeSpan ReminderInterval = TimeSpan.FromSeconds(60);

    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private ActionsSystem _actions = default!;

    /// <summary>
    /// Next real-time when a reminder may fire while the action remains off the hotbar.
    /// Null when not currently missing (so the next transition to missing reminds immediately).
    /// </summary>
    private TimeSpan? _nextReminderAt;

    /// <summary>
    /// True once we've seen Choose-a-tutorial on the hotbar this possession.
    /// Avoids nagging during the brief grant→auto-populate window on spawn.
    /// </summary>
    private bool _sawOnHotbar;

    public override void Update(float frameTime)
    {
        if (_player.LocalEntity == null)
        {
            ResetTracking();
            return;
        }

        if (!HasTutorialChooseRoleAction())
        {
            ResetTracking();
            return;
        }

        var onHotbar = _ui.GetUIController<ActionUIController>().IsTutorialChooseRoleOnHotbar();
        if (onHotbar)
        {
            _sawOnHotbar = true;
            _nextReminderAt = null;
            return;
        }

        // Never observed on the bar — treat as not-yet-populated, not removed.
        if (!_sawOnHotbar)
            return;

        var now = _timing.RealTime;
        if (_nextReminderAt != null && now < _nextReminderAt)
            return;

        SendReminder();
        _nextReminderAt = now + ReminderInterval;
    }

    private void ResetTracking()
    {
        _nextReminderAt = null;
        _sawOnHotbar = false;
    }

    private bool HasTutorialChooseRoleAction()
    {
        foreach (var (actionUid, _) in _actions.GetClientActions())
        {
            if (MetaData(actionUid).EntityPrototype?.ID == TutorialChooseRoleActionProto.Id)
                return true;
        }

        return false;
    }

    private void SendReminder()
    {
        // FTL embeds [keybind="OpenAbilitiesMenu"] (ContentKeyFunctions.OpenActionsMenu, default K).
        var markup = Loc.GetString("tutorial-server-choose-action-off-hotbar");
        var parsed = FormattedMessage.FromMarkupPermissive(markup);
        var plain = parsed.ToString();
        if (string.IsNullOrWhiteSpace(plain))
            return;

        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", parsed.ToMarkup()));
        var msg = new ChatMessage(
            ChatChannel.Server,
            plain,
            wrapped,
            NetEntity.Invalid,
            senderKey: null);

        _ui.GetUIController<ChatUIController>().ProcessChatMessage(msg, speechBubble: false);
    }
}
