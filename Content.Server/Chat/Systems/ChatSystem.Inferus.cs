using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Chat.Systems;

/// <summary>
/// Inferus-specific subtle / subtle OOC chat support
/// Ported from Floof-Station / Panta-Rhei subtle chat PRs
/// </summary>
public sealed partial class ChatSystem
{
    /// <summary>
    /// Public entry point used by the subtle command
    /// </summary>
    public void TrySendSubtle(EntityUid source, string message, bool hideLog = false, bool ignoreActionBlocker = false)
    {
        SendEntitySubtle(source, message, ChatTransmitRange.Normal, null, hideLog, ignoreActionBlocker);
    }

    /// <summary>
    /// Public entry point used by the sooc command
    /// </summary>
    public void TrySendSubtleOOC(EntityUid source, ICommonSession player, string message, bool hideChat = false)
    {
        SendSubtleLooc(source, player, message, hideChat);
    }

    private void SendEntitySubtle(
        EntityUid source,
        string action,
        ChatTransmitRange range,
        string? nameOverride,
        bool hideLog = false,
        bool ignoreActionBlocker = false,
        NetUserId? author = null)
    {
        if (!_actionBlocker.CanEmote(source) && !ignoreActionBlocker)
            return;

        // get the entity's apparent name (if no override provided)
        var ent = Identity.Entity(source, EntityManager);
        string name = FormattedMessage.EscapeText(nameOverride ?? Name(ent));

        var wrappedMessage = Loc.GetString("chat-manager-entity-subtle-wrap-message",
            ("entityName", name),
            ("entity", ent),
            ("message", action)); // DO NOT remove markup, there's an EscapeText call upstream

        SendInSubtleRange(ChatChannel.Subtle, source, action, wrappedMessage, range);

        if (!hideLog)
        {
            if (name != Name(source))
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Subtle from {ToPrettyString(source):user} as {name}: {action}");
            else
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Subtle from {ToPrettyString(source):user}: {action}");
        }
    }

    private void SendSubtleLooc(EntityUid source, ICommonSession player, string message, bool hideChat)
    {
        var name = FormattedMessage.EscapeText(Identity.Name(source, EntityManager));

        if (_adminManager.IsAdmin(player) && !_adminLoocEnabled || !_loocEnabled)
            return;

        // If crit player LOOC is disabled, don't send the message at all
        if (!_critLoocEnabled && _mobStateSystem.IsCritical(source))
            return;

        var wrappedMessage = Loc.GetString("chat-manager-entity-subtle-looc-wrap-message",
            ("entityName", name),
            ("message", FormattedMessage.EscapeText(message)));

        SendInSubtleRange(ChatChannel.SubtleOOC, source, message, wrappedMessage,
            hideChat ? ChatTransmitRange.HideChat : ChatTransmitRange.Normal);

        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"SOOC from {player:Player}: {message}");
    }

    /// <summary>
    /// Sends a message in subtle range (short range + LOS)
    /// Non-admin ghosts are blocked from seeing these
    /// </summary>
    private void SendInSubtleRange(ChatChannel channel, EntityUid source, string message, string wrappedMessage, ChatTransmitRange range)
{
    foreach (var (session, data) in GetRecipients(source, WhisperClearRange)) // or whatever range constant you prefer
    {
        // Ghost protection
        if (!data.Subtle)
            continue;

        // Wall / LOS protection
        if (!data.InLOS)
            continue;

        if (session.AttachedEntity is not { Valid: true })
            continue;

        _chatManager.ChatMessageToOne(channel, message, wrappedMessage, source, false, session.Channel);
    }
}
}
