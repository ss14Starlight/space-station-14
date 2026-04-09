using Content.Shared._Starlight.Achievement;
using Robust.Shared.Network;

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

public sealed class ClientAchievementManager : IClientAchievementManager
{
    [Dependency] private readonly IClientNetManager _netMgr = default!;

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
        UnlockedAchievements = message.UnlockedAchievements;
        Progress = message.Progress;
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
}
