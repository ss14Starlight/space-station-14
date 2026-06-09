using Content.Shared._NullLink;

namespace Content.Server._NullLink.PlayerData;

public sealed class NullLinkPlayTimeManager : INullLinkPlayTimeManager
{
    [Dependency] private readonly NullLinkPlayerManager _playTimeTrackingManager = default!;

    public TimeSpan GetPlayTime(string server, Guid player, string tracker)
        => _playTimeTrackingManager.TryGetPlayerData(player, out var playerData)
            && playerData.RolePlayTimePerServer.TryGetValue(server, out var serverData)
            && serverData.TryGetValue(tracker, out var playTime)
                ? playTime
                : TimeSpan.Zero;
}
