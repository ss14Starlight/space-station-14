// ReSharper disable CheckNamespace
using Content.Client._Starlight.Replay;
using Content.Shared.Chat;

namespace Content.Client.Replay;

public sealed partial class ContentReplayPlaybackManager
{
    private bool ShouldSkipChatMessage(ChatMessage chat) =>
        !_entMan.System<ReplayObserverSystem>().ShouldHearChat(chat);
}
