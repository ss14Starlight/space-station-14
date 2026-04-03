using Content.Server._NullLink.Helpers;
using Content.Server._NullLink.PlayerData;
using Content.Shared._Starlight.Achievement;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Starlight.NullLink;

namespace Content.Server._Starlight.Achievement;

public sealed class AchievementSystem : EntitySystem
{
    [Dependency] private readonly INullLinkPlayerManager _nullLinkPlayers = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private readonly Dictionary<Guid, Dictionary<string, double>> _progressByPlayer = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.Disconnected)
                EnsureProgress(session.UserId);
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        _progressByPlayer.Clear();
    }

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

    public double AddProgress(ICommonSession session, string progressType, double amount = 1)
        => AddProgress(session.UserId, progressType, amount);

    public double AddProgress(Guid userId, string progressType, double amount = 1)
    {
        var progress = EnsureProgress(userId);
        var value = progress.GetValueOrDefault(progressType) + amount;
        progress[progressType] = value;
        return value;
    }

    public async ValueTask<double> AddProgressAndCheck(ICommonSession session, string progressType, double amount = 1)
    {
        var value = AddProgress(session, progressType, amount);
        await CheckProgressAchievements(session, progressType);
        return value;
    }

    public async ValueTask<double> AddProgressAndCheck(Guid userId, string progressType, double amount = 1)
    {
        var value = AddProgress(userId, progressType, amount);
        await CheckProgressAchievements(userId, progressType);
        return value;
    }

    public double AddProgress(EntityUid uid, string progressType, double amount = 1)
    {
        if (!_playerManager.TryGetSessionByEntity(uid, out var session))
            return 0;

        return AddProgress(session, progressType, amount);
    }

    public double GetProgress(ICommonSession session, string progressType)
        => GetProgress(session.UserId, progressType);

    public double GetProgress(Guid userId, string progressType)
    {
        if (_progressByPlayer.TryGetValue(userId, out var progress)
            && progress.TryGetValue(progressType, out var value))
            return value;

        return 0;
    }

    public void ResetProgress(ICommonSession session, string? progressType = null)
        => ResetProgress(session.UserId, progressType);

    public void ResetProgress(Guid userId, string? progressType = null)
    {
        var progress = EnsureProgress(userId);
        if (string.IsNullOrEmpty(progressType))
        {
            progress.Clear();
            return;
        }

        progress.Remove(progressType);
    }

    public async ValueTask<bool> TryUnlockAtProgress(ICommonSession session, string achievementId, string progressType, double requiredProgress, string? characterName = null)
    {
        if (GetProgress(session, progressType) < requiredProgress)
            return false;

        return await TryUnlockAchievement(session, achievementId, characterName);
    }

    public async ValueTask CheckProgressAchievements(ICommonSession session, string progressType, string? characterName = null)
    {
        foreach (var achievement in _prototypeManager.EnumeratePrototypes<AchievementPrototype>())
        {
            if (!achievement.IsRelevantForProgress(progressType)
                || !achievement.AreRequirementsMet(type => GetProgress(session, type)))
                continue;

            await TryUnlockAchievement(session, achievement.ID, characterName);
        }
    }

    public async ValueTask CheckProgressAchievements(Guid userId, string progressType, string? characterName = null)
    {
        if (!_playerManager.TryGetSessionById(userId, out var session))
            return;

        await CheckProgressAchievements(session, progressType, characterName);
    }

    public async ValueTask<bool> TryUnlockAtProgress(Guid userId, string achievementId, string progressType, double requiredProgress, string? characterName = null)
    {
        if (GetProgress(userId, progressType) < requiredProgress)
            return false;

        if (!_playerManager.TryGetSessionById(userId, out var session))
            return false;

        return await TryUnlockAchievement(session, achievementId, characterName);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        switch (e.NewStatus)
        {
            case SessionStatus.Connected:
            case SessionStatus.InGame:
                EnsureProgress(e.Session.UserId);
                break;
            case SessionStatus.Disconnected:
                _progressByPlayer.Remove(e.Session.UserId);
                break;
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        AddProgress(ev.Player, AchievementProgressKeys.SpawnCount);
        AddProgress(ev.Player, ev.LateJoin ? AchievementProgressKeys.SpawnLateJoinCount : AchievementProgressKeys.SpawnRoundStartCount);

        CheckProgressAchievements(ev.Player, AchievementProgressKeys.SpawnCount)
            .AsTask()
            .FireAndForget();
        CheckProgressAchievements(ev.Player, ev.LateJoin ? AchievementProgressKeys.SpawnLateJoinCount : AchievementProgressKeys.SpawnRoundStartCount)
            .AsTask()
            .FireAndForget();

        if (!string.IsNullOrEmpty(ev.JobId))
        {
            var progressType = AchievementProgressKeys.SpawnJob(ev.JobId);
            AddProgress(ev.Player, progressType);
            CheckProgressAchievements(ev.Player, progressType)
                .AsTask()
                .FireAndForget();
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (ev.NewMobState != MobState.Dead || ev.OldMobState == MobState.Dead)
            return;

        AddProgress(ev.Target, AchievementProgressKeys.MobDeathCount);
        CheckProgressAchievements(ev.Target, AchievementProgressKeys.MobDeathCount)
            .AsTask()
            .FireAndForget();
    }

    private Dictionary<string, double> EnsureProgress(Guid userId)
    {
        if (_progressByPlayer.TryGetValue(userId, out var progress))
            return progress;

        progress = [];
        _progressByPlayer[userId] = progress;
        return progress;
    }

    private string GetCharacterName(ICommonSession session)
    {
        if (session.AttachedEntity is { } attached && TryComp<MetaDataComponent>(attached, out var meta))
            return meta.EntityName;

        return session.Name;
    }
}
