using System.Linq;
using Content.Shared._Starlight.Achievement;
using Robust.Shared.Player;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayerManager
{
    public bool HasReward(ICommonSession? session, AchievementRewardType rewardType, string rewardId)
    {
        if (session == null)
            return false;

        foreach (var achievementId in GetSourceAchievements(rewardType, rewardId))
        {
            if (HasAchievementUnlocked(session.UserId, achievementId))
                return true;
        }

        return false;
    }

    public IReadOnlyList<string> GetSourceAchievements(AchievementRewardType rewardType, string rewardId)
    {
        var result = new List<string>();

        foreach (var achievement in _proto.EnumeratePrototypes<AchievementPrototype>())
        {
            if (achievement.Rewards.Any(reward => reward.Type == rewardType && reward.ID == rewardId))
                result.Add(achievement.ID);
        }

        return result;
    }

    public IReadOnlyList<AchievementReward> GrantRewards(ICommonSession session, string achievementId)
    {
        _ = session;

        if (!_proto.TryIndex<AchievementPrototype>(achievementId, out var achievement))
            return [];

        return achievement.Rewards;
    }
}
