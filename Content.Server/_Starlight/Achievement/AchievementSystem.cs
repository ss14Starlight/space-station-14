using Content.Server._NullLink.PlayerData;
using Robust.Shared.Player;
using Starlight.NullLink;

namespace Content.Server._Starlight.Achievement;

public sealed class AchievementSystem : EntitySystem
{
    [Dependency] private readonly INullLinkPlayerManager _nullLinkPlayers = default!;

    public ValueTask<HashSet<Achievement>> GetUnlockedAchievements(ICommonSession session)
        => _nullLinkPlayers.GetUnlockedAchievements(session.UserId);

    public ValueTask<bool> HasAchievementUnlocked(ICommonSession session, string achievementId)
        => _nullLinkPlayers.HasAchievementUnlocked(session.UserId, achievementId);

    public ValueTask UnlockAchievement(ICommonSession session, string achievementId, string? characterName = null)
        => _nullLinkPlayers.UnlockAchievement(session.UserId, achievementId, characterName ?? GetCharacterName(session));

    public ValueTask LockAchievement(ICommonSession session, string achievementId)
        => _nullLinkPlayers.LockAchievement(session.UserId, achievementId);

    public async ValueTask<bool> TryUnlockAchievement(ICommonSession session, string achievementId, string? characterName = null)
    {
        if (!await HasAchievementUnlocked(session, achievementId))
        {
            await UnlockAchievement(session, achievementId, characterName);
            return true;
        }

        return false;
    }

    public async ValueTask<bool> TryLockAchievement(ICommonSession session, string achievementId)
    {
        if (await HasAchievementUnlocked(session, achievementId))
        {
            await LockAchievement(session, achievementId);
            return true;
        }

        return false;
    }

    private string GetCharacterName(ICommonSession session)
    {
        if (session.AttachedEntity is { } attached && TryComp<MetaDataComponent>(attached, out var meta))
            return meta.EntityName;

        return session.Name;
    }
}
