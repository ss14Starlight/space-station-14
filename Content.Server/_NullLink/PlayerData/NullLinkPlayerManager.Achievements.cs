using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Starlight.NullLink;
using Starlight.NullLink.Event;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayerManager : INullLinkPlayerManager
{
    public async ValueTask<HashSet<Achievement>> GetUnlockedAchievements(Guid userId)
    {
        if (!_actors.TryGetServerGrain(out var serverGrain))
            return TryGetCachedAchievements(userId, out var cachedAchievements) ? cachedAchievements : [];

        var achievements = await serverGrain.GetUnlockedAchievements(userId);
        var achievementsCopy = new HashSet<Achievement>(achievements);

        if (_playerById.TryGetValue(userId, out var playerData))
            playerData.UnlockedAchievements = achievementsCopy;

        return achievementsCopy;
    }

    public async ValueTask<bool> HasAchievementUnlocked(Guid userId, string achievementId)
    {
        if (_actors.TryGetServerGrain(out var serverGrain))
            return await serverGrain.HasAchievementUnlocked(userId, achievementId);

        return TryGetCachedAchievements(userId, out var achievements)
            && achievements.Any(achievement => achievement.AchievementId == achievementId);
    }

    public async ValueTask<bool> UnlockAchievement(Guid userId, string achievementId, string characterName)
    {
        if (!_actors.TryGetServerGrain(out var serverGrain))
            return false;

        try
        {
            await serverGrain.UnlockAchievement(userId, achievementId, characterName);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"UnlockAchievement failed for {userId}/{achievementId}: {ex}");
            return false;
        }

        if (_playerById.TryGetValue(userId, out var playerData))
        {
            playerData.UnlockedAchievements.RemoveWhere(achievement => achievement.AchievementId == achievementId);
            playerData.UnlockedAchievements.Add(new Achievement
            {
                AchievementId = achievementId,
                GrantingServer = _actors.Server ?? string.Empty,
                UnlockingCharacter = characterName,
                UnlockTime = DateTime.UtcNow,
            });
        }

        return true;
    }

    public async ValueTask<bool> LockAchievement(Guid userId, string achievementId)
    {
        if (!_actors.TryGetServerGrain(out var serverGrain))
            return false;

        try
        {
            await serverGrain.LockAchievement(userId, achievementId);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"LockAchievement failed for {userId}/{achievementId}: {ex}");
            return false;
        }

        if (_playerById.TryGetValue(userId, out var playerData))
            playerData.UnlockedAchievements.RemoveWhere(achievement => achievement.AchievementId == achievementId);

        return true;
    }

    public ValueTask SyncAchievements(PlayerAchievementsSyncEvent ev)
    {
        if (!_playerById.TryGetValue(ev.Player, out var playerData))
            return ValueTask.CompletedTask;

        playerData.UnlockedAchievements = [.. ev.Achievements];
        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAchievementUnlocked(AchievementUnlockedEvent ev)
    {
        if (!_playerById.TryGetValue(ev.Player, out var playerData))
            return ValueTask.CompletedTask;

        playerData.UnlockedAchievements.RemoveWhere(achievement => achievement.AchievementId == ev.Achievement.AchievementId);
        playerData.UnlockedAchievements.Add(ev.Achievement);
        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAchievementLocked(AchievementLockedEvent ev)
    {
        if (!_playerById.TryGetValue(ev.Player, out var playerData))
            return ValueTask.CompletedTask;

        playerData.UnlockedAchievements.RemoveWhere(achievement => achievement.AchievementId == ev.AchievementId);
        return ValueTask.CompletedTask;
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
}
