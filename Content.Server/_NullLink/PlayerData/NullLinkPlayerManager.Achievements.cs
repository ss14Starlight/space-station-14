using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Starlight.NullLink;

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

    public async ValueTask UnlockAchievement(Guid userId, string achievementId, string characterName)
    {
        if (!_actors.TryGetServerGrain(out var serverGrain))
            return;

        await serverGrain.UnlockAchievement(userId, achievementId, characterName);

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
    }

    public async ValueTask LockAchievement(Guid userId, string achievementId)
    {
        if (!_actors.TryGetServerGrain(out var serverGrain))
            return;

        await serverGrain.LockAchievement(userId, achievementId);

        if (_playerById.TryGetValue(userId, out var playerData))
            playerData.UnlockedAchievements.RemoveWhere(achievement => achievement.AchievementId == achievementId);
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
