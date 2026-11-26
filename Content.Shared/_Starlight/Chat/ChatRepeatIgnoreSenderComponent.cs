namespace Content.Shared._Starlight.Chat;

/// <summary>
///     Marks a chat message source whose messages may stack with messages from other speakers.
///     Should be given to entities that relay chat from multiple people, such as PA speakers.
/// </summary>
[RegisterComponent]
public sealed partial class ChatRepeatIgnoreSenderComponent : Component;
