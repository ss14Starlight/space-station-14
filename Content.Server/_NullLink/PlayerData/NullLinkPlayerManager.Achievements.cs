using System.Linq;
using System.Threading.Tasks;
using Content.Shared._Starlight.Achievement;
using Starlight.NullLink;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayerManager : INullLinkPlayerManager
{
    public async ValueTask<HashSet<Achievement>> GetUnlockedAchievements(Guid userId)
    {
        if (!_actors.TryGetServerGrain(out var serverGrain))
        {
            return TryGetCachedAchievements(userId, out var cachedAchievements) ? cachedAchievements : [];
        }

        try
        {
            var achievements = await serverGrain.GetUnlockedAchievements(userId);
            HashSet<Achievement> mergedAchievements = new (achievements);

            if (_playerById.TryGetValue(userId, out var playerData))
            {
                mergedAchievements = MergeAchievements(playerData.UnlockedAchievements, achievements);
                playerData.UnlockedAchievements = [.. mergedAchievements];
                playerData.AchievementCacheHydrated = true;
            }

            return mergedAchievements;
        }
        catch (Exception ex)
        {
            _sawmill.Error($"GetUnlockedAchievements failed for {userId}: {ex}");
            return TryGetCachedAchievements(userId, out var cached) ? cached : [];
        }
    }

    public bool HasAchievementUnlocked(Guid userId, string achievementId)
    {
        return _playerById.TryGetValue(userId, out var playerData)
            && playerData.AchievementCacheHydrated
            && TryGetCachedAchievements(userId, out var cached)
            && cached.Any(a => a.AchievementId == achievementId);
    }

    public async ValueTask<bool> HasAchievementUnlockedAsync(Guid userId, string achievementId)
    {
        if (_playerById.TryGetValue(userId, out var playerData)
            && playerData.AchievementCacheHydrated)
        {
            return playerData.UnlockedAchievements.Any(a => a.AchievementId == achievementId);
        }

        var achievements = await GetUnlockedAchievements(userId);
        return achievements.Any(a => a.AchievementId == achievementId);
    }

    public async ValueTask<bool> UnlockAchievement(Guid userId, string achievementId, string characterName)
    {
        if (!_actors.TryGetServerGrain(out var serverGrain))
        {
            _sawmill.Error($"UnlockAchievement failed for {userId}/{achievementId}: server grain is unavailable.");
            return false;
        }

        try
        {
            await serverGrain.UnlockAchievement(userId, achievementId, characterName);
            SetCachedAchievementUnlocked(userId, achievementId, characterName);
            return true;
        }
        catch (Exception ex)
        {
            _sawmill.Error($"UnlockAchievement grain call failed for {userId}/{achievementId}: {ex}");
            return false;
        }
    }

    public async ValueTask<bool> LockAchievement(Guid userId, string achievementId)
    {
        if (!_actors.TryGetServerGrain(out var serverGrain))
        {
            _sawmill.Error($"LockAchievement failed for {userId}/{achievementId}: server grain is unavailable.");
            return false;
        }

        try
        {
            await serverGrain.LockAchievement(userId, achievementId);
            SetCachedAchievementLocked(userId, achievementId);
            return true;
        }
        catch (Exception ex)
        {
            _sawmill.Error($"LockAchievement grain call failed for {userId}/{achievementId}: {ex}");
            return false;
        }
    }

    private bool TryGetCachedAchievements(Guid userId, out HashSet<Achievement> achievements)
    {
        if (_playerById.TryGetValue(userId, out var playerData))
        {
            achievements = new HashSet<Achievement>(playerData.UnlockedAchievements);
            return true;
        }

        achievements = [];
        return false;
    }

    private static HashSet<Achievement> MergeAchievements(IEnumerable<Achievement> localAchievements, IEnumerable<Achievement> remoteAchievements)
    {
        var merged = remoteAchievements.ToDictionary(achievement => achievement.AchievementId, achievement => achievement);

        foreach (var achievement in localAchievements)
        {
            if (!merged.TryGetValue(achievement.AchievementId, out var existing)
                || achievement.UnlockTime >= existing.UnlockTime)
            {
                merged[achievement.AchievementId] = achievement;
            }
        }

        return [.. merged.Values];
    }

    private void SetCachedAchievementUnlocked(Guid userId, string achievementId, string characterName)
    {
        if (!_playerById.TryGetValue(userId, out var playerData))
            return;

        var achievements = playerData.UnlockedAchievements.ToHashSet();
        achievements.RemoveWhere(achievement => achievement.AchievementId == achievementId);
        achievements.Add(new Achievement
        {
            AchievementId = achievementId,
            GrantingServer = _actors.Server ?? string.Empty,
            UnlockingCharacter = characterName,
            UnlockTime = DateTime.UtcNow,
        });
        playerData.UnlockedAchievements = [.. achievements];
        playerData.AchievementCacheHydrated = true;
    }

    private void SetCachedAchievementLocked(Guid userId, string achievementId)
    {
        if (!_playerById.TryGetValue(userId, out var playerData))
            return;

        var achievements = playerData.UnlockedAchievements.ToHashSet();
        achievements.RemoveWhere(achievement => achievement.AchievementId == achievementId);
        playerData.UnlockedAchievements = [.. achievements];
        playerData.AchievementCacheHydrated = true;
    }

    public void SendAchievementList(Guid userId)
    {
        if (!_playerById.TryGetValue(userId, out var playerData))
            return;

        var msg = new MsgAchievementList
        {
            UnlockedAchievements = playerData.UnlockedAchievements
                .Select(a => a.AchievementId)
                .ToHashSet(),
            Progress = new Dictionary<string, double>(playerData.AchievementProgress),
        };

        _netMgr.ServerSendMessage(msg, playerData.Session.Channel);
    }

    public void SendAchievementNotification(Guid userId, string achievementId)
    {
        if (!_playerById.TryGetValue(userId, out var playerData))
            return;

        var msg = new MsgAchievementNotification
        {
            AchievementId = achievementId,
        };

        _netMgr.ServerSendMessage(msg, playerData.Session.Channel);
    }
}
