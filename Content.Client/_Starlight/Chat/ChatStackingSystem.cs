using Content.Client.UserInterface.Systems.Chat;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Chat;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Chat;

/// <summary>
///     Folds repeated chat messages into the most recent matching line and appends a multiplier to it.
/// </summary>
public sealed partial class ChatStackingSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    private int _repeatHistory;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_config, StarlightCCVars.ChatRepeatHistory, OnRepeatHistoryChanged, true);
    }

    private void OnRepeatHistoryChanged(int repeatHistory)
    {
        _repeatHistory = repeatHistory;

        foreach (var chat in _ui.GetUIController<ChatUIController>().Chats)
        {
            if (_repeatHistory <= 0)
            {
                chat.RepeatQueue.Clear();
                continue;
            }

            while (chat.RepeatQueue.Count > _repeatHistory)
            {
                chat.RepeatQueue.Dequeue();
            }
        }
    }

    /// <summary>
    ///     Tries to stack <paramref name="message"/> onto a recently received message in <paramref name="chat"/>.
    /// </summary>
    public bool TryRepetition(
        ChatBox chat,
        OutputPanel contents,
        FormattedMessage message,
        NetEntity sender,
        int? senderKey,
        string unwrapped,
        ChatChannel channel,
        bool repeatCheckSender,
        string? repeatChannel)
    {
        // A non-positive history disables stacking instead of keeping an unbounded queue.
        if (_repeatHistory <= 0)
            return false;

        // Ignore admin messages, there important.
        if ((channel & ChatChannel.AdminRelated) != 0)
            return false;

        foreach (var old in chat.RepeatQueue)
        {
            if (old.Message != unwrapped ||
                old.Channel != channel ||
                old.RepeatChannel != repeatChannel ||
                (repeatCheckSender && (old.SenderEntity != sender || old.SenderKey != senderKey)))
            {
                continue;
            }

            var copy = new FormattedMessage(old.FormattedMessage);
            old.Count++;
            copy.AddMarkupPermissive($" [color=red]{Loc.GetString("chat-stacking-count", ("count", old.Count))}[/color]");
            contents.SetMessage(old.Index, copy, tagsAllowed: null);
            return true;
        }

        chat.RepeatQueue.Enqueue(new RepeatedMessage(contents.EntryCount, message, sender, senderKey, unwrapped, channel, repeatChannel));
        while (chat.RepeatQueue.Count > _repeatHistory)
        {
            chat.RepeatQueue.Dequeue();
        }

        return false;
    }
}
