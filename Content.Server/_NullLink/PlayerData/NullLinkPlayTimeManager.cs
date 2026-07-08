using Content.Shared._NullLink;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayTimeManager : INullLinkPlayTimeManager
{
    [Dependency] private NullLinkPlayerManager _playTimeTrackingManager = default!;

    public TimeSpan GetPlayTime(string server, Guid player, string tracker)
        => _playTimeTrackingManager.TryGetPlayerData(player, out var playerData)
            && playerData.RolePlayTimePerServer.TryGetValue(server, out var serverData)
            && serverData.TryGetValue(tracker, out var playTime)
                ? playTime
                : TimeSpan.Zero;
}
