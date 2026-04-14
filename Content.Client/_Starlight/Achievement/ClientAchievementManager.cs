using Content.Shared._Starlight.Achievement;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Client._Starlight.Achievement;

public interface IClientAchievementManager
{
    HashSet<string> UnlockedAchievements { get; }
    Dictionary<string, double> Progress { get; }

    event Action? AchievementsUpdated;
    event Action<string>? AchievementUnlocked;

    void Initialize();
    bool IsUnlocked(string achievementId);
    double GetProgress(string key);
}

public sealed class ClientAchievementManager : IClientAchievementManager, IAchievementRewardManager
{
    [Dependency] private readonly IClientNetManager _netMgr = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public HashSet<string> UnlockedAchievements { get; private set; } = [];
    public Dictionary<string, double> Progress { get; private set; } = [];

    public event Action? AchievementsUpdated;
    public event Action<string>? AchievementUnlocked;

    public void Initialize()
    {
        _netMgr.RegisterNetMessage<MsgAchievementList>(OnAchievementList);
        _netMgr.RegisterNetMessage<MsgAchievementNotification>(OnAchievementNotification);
    }

    private void OnAchievementList(MsgAchievementList message)
    {
        UnlockedAchievements = new HashSet<string>(message.UnlockedAchievements);
        Progress = new Dictionary<string, double>(message.Progress);
        AchievementsUpdated?.Invoke();
    }

    private void OnAchievementNotification(MsgAchievementNotification message)
    {
        UnlockedAchievements.Add(message.AchievementId);
        AchievementUnlocked?.Invoke(message.AchievementId);
        AchievementsUpdated?.Invoke();
    }

    public bool IsUnlocked(string achievementId) => UnlockedAchievements.Contains(achievementId);

    public double GetProgress(string key)
        => Progress.TryGetValue(key, out var value) ? value : 0;

    public bool HasReward(ICommonSession? session, AchievementRewardType rewardType, string rewardId)
    {
        if (session == null)
            return false;

        foreach (var achievementId in GetSourceAchievements(rewardType, rewardId))
        {
            if (IsUnlocked(achievementId))
                return true;
        }

        return false;
    }

    public IReadOnlyList<string> GetSourceAchievements(AchievementRewardType rewardType, string rewardId)
    {
        var result = new List<string>();

        foreach (var achievement in _prototype.EnumeratePrototypes<AchievementPrototype>())
        {
            if (achievement.Rewards.Any(reward => reward.Type == rewardType && reward.ID == rewardId))
                result.Add(achievement.ID);
        }

        return result;
    }

    public IReadOnlyList<AchievementReward> GrantRewards(ICommonSession _, string achievementId)
    {

        if (!_prototype.TryIndex<AchievementPrototype>(achievementId, out var achievement))
            return [];

        return achievement.Rewards;
    }
}
