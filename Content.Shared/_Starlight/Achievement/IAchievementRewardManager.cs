using Robust.Shared.Player;

namespace Content.Shared._Starlight.Achievement;

public interface IAchievementRewardManager
{
    bool HasReward(ICommonSession? session, AchievementRewardType rewardType, string rewardId);
    IReadOnlyList<string> GetSourceAchievements(AchievementRewardType rewardType, string rewardId);
    IReadOnlyList<AchievementReward> GrantRewards(ICommonSession session, string achievementId);
}
