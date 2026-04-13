using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Robust.Shared.Player;
using Starlight.NullLink;
using Starlight.NullLink.Event;

namespace Content.Server._NullLink.PlayerData;
public interface INullLinkPlayerManager
{
    IEnumerable<ICommonSession> Mentors { get; }

    string GetDiscordAuthUrl(string customState);
    void Initialize();
    void Shutdown();
    ValueTask SyncPlayTime(PlayerServerPlayTimesSyncEvent playTimesSync);
    ValueTask SyncRoles(PlayerRolesSyncEvent ev);
    ValueTask SyncResources(PlayerResourcesSyncEvent ev);
    bool TryGetPlayerData(Guid userId, [NotNullWhen(true)] out PlayerData? playerData);
    ValueTask UpdateRoles(RolesChangedEvent ev);
    ValueTask UpdateResource(ResourceChangedEvent ev);
    ValueTask<HashSet<Achievement>> GetUnlockedAchievements(Guid userId);
    bool HasAchievementUnlocked(Guid userId, string achievementId);
    ValueTask<bool> HasAchievementUnlockedAsync(Guid userId, string achievementId);
    ValueTask<bool> UnlockAchievement(Guid userId, string achievementId, string characterName);
    ValueTask<bool> LockAchievement(Guid userId, string achievementId);
    ValueTask<Dictionary<string, double>> GetAchievementProgress(Guid userId);
    double GetCachedAchievementProgress(Guid userId, string key);
    double AddAchievementProgress(Guid userId, string key, double amount);
    void ResetAchievementProgress(Guid userId, string? key = null);
    void SendAchievementList(Guid userId);
    void SendAchievementNotification(Guid userId, string achievementId);
}
