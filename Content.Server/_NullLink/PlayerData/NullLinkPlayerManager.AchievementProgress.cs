using System.Threading.Tasks;
using Content.Server._NullLink.Helpers;
using Starlight.NullLink;
using Starlight.NullLink.Event;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayerManager : INullLinkPlayerManager
{
    public async ValueTask<Dictionary<string, double>> GetAchievementProgress(Guid userId)
    {
        if (!_actors.TryGetServerGrain(out var serverGrain))
            return TryGetCachedProgress(userId, out var cached) ? cached : [];

        var progress = await serverGrain.GetAchievementProgress(userId);
        var copy = new Dictionary<string, double>(progress);

        if (_playerById.TryGetValue(userId, out var playerData))
            playerData.AchievementProgress = copy;

        return copy;
    }

    public double GetCachedAchievementProgress(Guid userId, string key)
    {
        if (_playerById.TryGetValue(userId, out var playerData)
            && playerData.AchievementProgress.TryGetValue(key, out var value))
            return value;

        return 0;
    }

    public double AddAchievementProgress(Guid userId, string key, double amount)
    {
        if (!_playerById.TryGetValue(userId, out var playerData))
            return 0;

        var value = playerData.AchievementProgress.GetValueOrDefault(key) + amount;
        playerData.AchievementProgress[key] = value;

        if (_actors.TryGetServerGrain(out var serverGrain))
            serverGrain.SetAchievementProgress(userId, key, value)
                .FireAndForget(err => _sawmill.Error($"SetAchievementProgress failed for {userId}/{key}: {err}"));

        return value;
    }

    public void ResetAchievementProgress(Guid userId, string? key = null)
    {
        if (!_playerById.TryGetValue(userId, out var playerData))
            return;

        if (string.IsNullOrEmpty(key))
        {
            playerData.AchievementProgress.Clear();
            return;
        }

        playerData.AchievementProgress.Remove(key);
    }

    public ValueTask SyncAchievementProgress(PlayerAchievementProgressSyncEvent ev)
    {
        if (!_playerById.TryGetValue(ev.Player, out var playerData))
            return ValueTask.CompletedTask;

        playerData.AchievementProgress = new Dictionary<string, double>(ev.Progress);
        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAchievementProgressChanged(AchievementProgressChangedEvent ev)
    {
        if (!_playerById.TryGetValue(ev.Player, out var playerData))
            return ValueTask.CompletedTask;

        playerData.AchievementProgress[ev.ProgressType] = ev.Value;
        return ValueTask.CompletedTask;
    }

    private bool TryGetCachedProgress(Guid userId, out Dictionary<string, double> progress)
    {
        if (_playerById.TryGetValue(userId, out var playerData))
        {
            progress = new Dictionary<string, double>(playerData.AchievementProgress);
            return true;
        }

        progress = [];
        return false;
    }
}
