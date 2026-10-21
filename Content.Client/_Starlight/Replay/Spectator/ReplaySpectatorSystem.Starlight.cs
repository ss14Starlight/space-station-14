// ReSharper disable CheckNamespace
using Content.Client._Starlight.Replay;

namespace Content.Client.Replay.Spectator;

public sealed partial class ReplaySpectatorSystem
{
    // Recorded server messages to a spectated mob (e.g. health analyzer results) are validated with an interaction
    // check, so the blocker must let them through.
    private bool IsApplyingRecordedMessages() =>
        EntityManager.System<ReplayObserverSystem>().IsApplyingRecordedMessages;
}
