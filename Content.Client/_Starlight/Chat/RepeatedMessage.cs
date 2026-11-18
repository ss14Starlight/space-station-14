using Content.Shared.Chat;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Chat;

/// <summary>
///     A chat message that can be stacked onto by later messages in the same chat box.
/// </summary>
public sealed class RepeatedMessage(
    int index,
    FormattedMessage formattedMessage,
    NetEntity senderEntity,
    int? senderKey,
    string message,
    ChatChannel channel,
    string? repeatChannel)
{
    /// <summary>
    ///     The line index of this message within its chat box.
    /// </summary>
    public readonly int Index = index;

    /// <summary>
    ///     The original message, without the repetition counter appended.
    /// </summary>
    public readonly FormattedMessage FormattedMessage = formattedMessage;

    /// <summary>
    ///     The entity that sent the message.
    /// </summary>
    public readonly NetEntity SenderEntity = senderEntity;

    /// <summary>
    ///     The author key that sent the message.
    /// </summary>
    public readonly int? SenderKey = senderKey;

    /// <summary>
    ///     The raw message text, used to compare messages without markup differences.
    /// </summary>
    public readonly string Message = message;

    /// <summary>
    ///     The channel the message was sent on. Messages only stack within the same channel.
    /// </summary>
    public readonly ChatChannel Channel = channel;

    /// <summary>
    ///     An identifier for channels that share the same <see cref="ChatChannel"/>.
    /// </summary>
    public readonly string? RepeatChannel = repeatChannel;

    /// <summary>
    ///     How many times this message has been received, including the first one.
    /// </summary>
    public int Count = 1;
}
