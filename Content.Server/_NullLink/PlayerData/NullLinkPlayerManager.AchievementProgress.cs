using System.Threading.Tasks;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayerManager : INullLinkPlayerManager
{
    public ValueTask<Dictionary<string, double>> GetAchievementProgress(Guid userId)
    {
        return ValueTask.FromResult(
            TryGetCachedProgress(userId, out var cached) ? cached : new Dictionary<string, double>());
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

        return playerData.AchievementProgress.AddOrUpdate(key, amount, (_, existing) => existing + amount);
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

        playerData.AchievementProgress.TryRemove(key, out _);
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
