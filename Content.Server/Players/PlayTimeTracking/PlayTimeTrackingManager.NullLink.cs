using Robust.Shared.Enums;
using Robust.Shared.Network;

namespace Content.Server.Players.PlayTimeTracking;

public sealed partial class PlayTimeTrackingManager
{
    private readonly Dictionary<NetUserId, Dictionary<string, TimeSpan>> _nullLinkPlayTime = [];

    public void EnrichWithNullLink(Dictionary<string, TimeSpan> playtime, Guid userId, Action? onApplied = null)
        => _task.RunOnMainThread(() =>
        {
            ApplyNullLinkPlayTime(playtime, new NetUserId(userId));
            onApplied?.Invoke();
        });

    public void ClearNullLinkPlayTime(Guid userId)
        => _task.RunOnMainThread(() => _nullLinkPlayTime.Remove(new NetUserId(userId)));

    private void ApplyNullLinkPlayTime(Dictionary<string, TimeSpan> playtime, NetUserId userId)
    {
        if (!_player.TryGetSessionById(userId, out var session) || session.Status == SessionStatus.Disconnected)
            return;

        _nullLinkPlayTime[userId] = playtime;

        if (_playTimeData.TryGetValue(session, out var data))
            RebuildMergedTrackerTimes(userId, data);
    }

    private void RebuildMergedTrackerTimes(NetUserId userId, PlayTimeData data)
    {
        var merged = _nullLinkPlayTime.TryGetValue(userId, out var nullLinked)
            ? new Dictionary<string, TimeSpan>(nullLinked)
            : [];

        foreach (var (tracker, time) in data.TrackerTimes)
        {
            if (merged.TryGetValue(tracker, out var nullLinkedTime))
                merged[tracker] = time + nullLinkedTime;
            else
                merged[tracker] = time;
        }

        data.MergedTrackerTimes = merged;
    }
}
