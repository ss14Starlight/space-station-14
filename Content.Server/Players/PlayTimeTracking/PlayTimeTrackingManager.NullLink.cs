using Robust.Shared.Network;

namespace Content.Server.Players.PlayTimeTracking;

public sealed partial class PlayTimeTrackingManager
{
    public void EnrichWithNullLink(Dictionary<string, TimeSpan> playtime, Guid userId, Action? onApplied = null)
        => _task.RunOnMainThread(() =>
        {
            ApplyNullLinkPlayTime(playtime, userId);
            onApplied?.Invoke();
        });

    private void ApplyNullLinkPlayTime(Dictionary<string, TimeSpan> playtime, Guid userId)
    {
        if (!_player.TryGetSessionById(new NetUserId(userId), out var session))
            return;

        if (!_playTimeData.TryGetValue(session, out var data))
            return;

        var merged = new Dictionary<string, TimeSpan>(playtime);
        foreach (var (tracker, time) in data.TrackerTimes)
        {
            if (merged.TryGetValue(tracker, out var nullinked))
                merged[tracker] = time + nullinked;
            else
                merged[tracker] = time;
        }
        data.MergedTrackerTimes = merged;
    }
}
