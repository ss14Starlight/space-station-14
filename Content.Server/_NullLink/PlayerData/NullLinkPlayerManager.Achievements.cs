using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Content.Server._NullLink.Helpers;
using Content.Shared._Starlight.Achievement;
using Starlight.NullLink;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayerManager : INullLinkPlayerManager
{
    public async ValueTask<HashSet<Achievement>> GetUnlockedAchievements(Guid userId)
    {
        if (!_actors.TryGetServerGrain(out var serverGrain))
        {
            if (_playerById.TryGetValue(userId, out var fallbackData))
                fallbackData.AchievementCacheHydrated = true;

            return TryGetCachedAchievements(userId, out var cachedAchievements) ? cachedAchievements : [];
        }

        try
        {
            var achievements = await serverGrain.GetUnlockedAchievements(userId);

            if (_playerById.TryGetValue(userId, out var playerData))
            {
                playerData.UnlockedAchievements = [.. achievements];
                playerData.AchievementCacheHydrated = true;
            }

            return new HashSet<Achievement>(achievements);
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

    public bool UnlockAchievement(Guid userId, string achievementId, string characterName)
    {
        if (_playerById.TryGetValue(userId, out var playerData))
        {
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

        if (_actors.TryGetServerGrain(out var serverGrain))
        {
            serverGrain.UnlockAchievement(userId, achievementId, characterName)
                .FireAndForget(ex => _sawmill.Error($"UnlockAchievement grain call failed for {userId}/{achievementId}: {ex}"));
        }

        return true;
    }

    public bool LockAchievement(Guid userId, string achievementId)
    {
        if (_playerById.TryGetValue(userId, out var playerData))
        {
            var achievements = playerData.UnlockedAchievements.ToHashSet();
            achievements.RemoveWhere(achievement => achievement.AchievementId == achievementId);
            playerData.UnlockedAchievements = [.. achievements];
            playerData.AchievementCacheHydrated = true;
        }

        if (_actors.TryGetServerGrain(out var serverGrain))
        {
            serverGrain.LockAchievement(userId, achievementId)
                .FireAndForget(ex => _sawmill.Error($"LockAchievement grain call failed for {userId}/{achievementId}: {ex}"));
        }

        return true;
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
