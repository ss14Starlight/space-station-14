using System.Threading.Tasks;
using Content.Server._NullLink.Helpers;
using Content.Server._NullLink.PlayerData;
using Content.Server.Chat;
using Content.Server.Nuke;
using Content.Shared._Starlight.Antags.Vampires;
using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Achievement;
using Content.Shared.GameTicking;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Smoking;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Achievement;

public sealed class AchievementSystem : EntitySystem
{
    [Dependency] private readonly INullLinkPlayerManager _nullLinkPlayers = default!;
    [Dependency] private readonly IAchievementRewardManager _achievementRewards = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private static readonly TimeSpan AchievementHydrationRetryDelay = TimeSpan.FromSeconds(3);
    private readonly Dictionary<Guid, Dictionary<string, double>> _roundProgress = [];
    private readonly HashSet<Guid> _achievementFetchInFlight = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<VampireComponent, VampireBloodDrankEvent>(OnVampireBloodDrank);
        SubscribeLocalEvent<NukeExplodedEvent>(OnNukeExploded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status == SessionStatus.Disconnected)
                continue;

            QueueAchievementHydration(session);
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    #region Achievement Management
    private bool HasAchievementUnlocked(ICommonSession session, string achievementId)
        => _nullLinkPlayers.HasAchievementUnlocked(session.UserId, achievementId);

    public ValueTask<bool> HasAchievementUnlockedAsync(ICommonSession session, string achievementId)
        => _nullLinkPlayers.HasAchievementUnlockedAsync(session.UserId, achievementId);

    private ValueTask<bool> UnlockAchievement(ICommonSession session, string achievementId, string? characterName = null)
        => _nullLinkPlayers.UnlockAchievement(session.UserId, achievementId, characterName ?? GetCharacterName(session));

    private ValueTask<bool> LockAchievement(ICommonSession session, string achievementId)
        => _nullLinkPlayers.LockAchievement(session.UserId, achievementId);

    public async ValueTask<bool> TryUnlockAchievementAsync(ICommonSession session, string achievementId, string? characterName = null)
    {
        if (await HasAchievementUnlockedAsync(session, achievementId))
            return false;

        var result = await UnlockAchievement(session, achievementId, characterName);
        if (result)
        {
            _achievementRewards.GrantRewards(session, achievementId);
            _nullLinkPlayers.SendAchievementNotification(session.UserId, achievementId);
            _nullLinkPlayers.SendAchievementList(session.UserId);
        }

        return result;
    }

    public async ValueTask<bool> TryLockAchievementAsync(ICommonSession session, string achievementId)
    {
        if (!await HasAchievementUnlockedAsync(session, achievementId))
            return false;

        var result = await LockAchievement(session, achievementId);
        if (result)
            _nullLinkPlayers.SendAchievementList(session.UserId);

        return result;
    }
    #endregion

    #region Progress Management
    public double AddProgress(ICommonSession session, string progressType, double amount = 1)
        => AddProgress(session.UserId, progressType, amount);

    public double AddProgress(Guid userId, string progressType, double amount = 1)
    {
        AddRoundProgress(userId, progressType, amount);
        var value = _nullLinkPlayers.AddAchievementProgress(userId, progressType, amount);
        _nullLinkPlayers.SendAchievementList(userId);
        return value;
    }

    public double AddProgressAndCheck(ICommonSession session, string progressType, double amount = 1)
    {
        var value = AddProgress(session, progressType, amount);
        CheckProgressAchievementsAsync(session, progressType)
            .AsTask()
            .FireAndForget();
        return value;
    }

    public double AddProgressAndCheck(Guid userId, string progressType, double amount = 1)
    {
        var value = AddProgress(userId, progressType, amount);
        CheckProgressAchievementsAsync(userId, progressType)
            .AsTask()
            .FireAndForget();
        return value;
    }

    public double AddProgressAndCheck(EntityUid uid, string progressType, double amount = 1)
    {
        if (!_playerManager.TryGetSessionByEntity(uid, out var session))
            return 0;

        return AddProgressAndCheck(session, progressType, amount);
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
        return _nullLinkPlayers.GetCachedAchievementProgress(userId, progressType);
    }

    public void ResetProgress(ICommonSession session, string? progressType = null)
        => ResetProgress(session.UserId, progressType);

    public void ResetProgress(Guid userId, string? progressType = null)
    {
        _nullLinkPlayers.ResetAchievementProgress(userId, progressType);
        _nullLinkPlayers.SendAchievementList(userId);
    }

    public async ValueTask<bool> TryUnlockAtProgressAsync(ICommonSession session, string achievementId, string progressType, double requiredProgress, string? characterName = null)
    {
        if (GetProgress(session, progressType) < requiredProgress)
            return false;

        return await TryUnlockAchievementAsync(session, achievementId, characterName);
    }

    public void CheckProgressAchievements(ICommonSession session, string progressType, string? characterName = null)
        => CheckProgressAchievementsAsync(session, progressType, characterName)
            .AsTask()
            .FireAndForget();

    public async ValueTask CheckProgressAchievementsAsync(ICommonSession session, string progressType, string? characterName = null)
    {
        foreach (var achievement in _prototypeManager.EnumeratePrototypes<AchievementPrototype>())
        {
            if (!achievement.IsRelevantForProgress(progressType)
                || !achievement.AreRequirementsMet((type, perRound) => perRound
                    ? GetRoundProgress(session.UserId, type)
                    : GetProgress(session, type)))
                continue;

            await TryUnlockAchievementAsync(session, achievement.ID, characterName);
        }
    }

    public void CheckProgressAchievements(Guid userId, string progressType, string? characterName = null)
        => CheckProgressAchievementsAsync(userId, progressType, characterName)
            .AsTask()
            .FireAndForget();

    public async ValueTask CheckProgressAchievementsAsync(Guid userId, string progressType, string? characterName = null)
    {
        if (!_playerManager.TryGetSessionById(new NetUserId(userId), out var session))
            return;

        await CheckProgressAchievementsAsync(session, progressType, characterName);
    }

    public async ValueTask<bool> TryUnlockAtProgressAsync(Guid userId, string achievementId, string progressType, double requiredProgress, string? characterName = null)
    {
        if (GetProgress(userId, progressType) < requiredProgress)
            return false;

        if (!_playerManager.TryGetSessionById(new NetUserId(userId), out var session))
            return false;

        return await TryUnlockAchievementAsync(session, achievementId, characterName);
    }
    #endregion

    #region Event Handlers
    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        switch (e.NewStatus)
        {
            case SessionStatus.Connected:
                QueueAchievementHydration(e.Session);
                break;
            case SessionStatus.InGame:
                if (_nullLinkPlayers.TryGetPlayerData(e.Session.UserId, out var playerData)
                    && playerData.AchievementCacheHydrated)
                {
                    _nullLinkPlayers.SendAchievementList(e.Session.UserId);
                }
                else
                {
                    QueueAchievementHydration(e.Session);
                }
                break;
            case SessionStatus.Disconnected:
                _achievementFetchInFlight.Remove(e.Session.UserId);
                break;
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        AddProgress(ev.Player, AchievementProgressKeys.SpawnCount);
        AddProgress(ev.Player, ev.LateJoin ? AchievementProgressKeys.SpawnLateJoinCount : AchievementProgressKeys.SpawnRoundStartCount);

        CheckProgressAchievements(ev.Player, AchievementProgressKeys.SpawnCount);
        CheckProgressAchievements(ev.Player, ev.LateJoin ? AchievementProgressKeys.SpawnLateJoinCount : AchievementProgressKeys.SpawnRoundStartCount);

        if (!string.IsNullOrEmpty(ev.JobId))
        {
            var progressType = AchievementProgressKeys.SpawnJob(ev.JobId);
            AddProgress(ev.Player, progressType);
            CheckProgressAchievements(ev.Player, progressType);
        }
    }

    private void OnVampireBloodDrank(EntityUid uid, VampireComponent _, VampireBloodDrankEvent ev)
        => AddProgressAndCheck(uid, AchievementProgressKeys.VampireBloodDrank, ev.Amount);

    private void OnNukeExploded(NukeExplodedEvent ev)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } uid)
                continue;

            if (!TryComp<MobStateComponent>(uid, out var mobState)
                || mobState.CurrentState == MobState.Dead)
                continue;

            if (ev.OwningStation != null && Transform(uid).GridUid != ev.OwningStation)
                continue;

            if (!_inventory.TryGetSlotEntity(uid, "mask", out var maskItem)
                || !TryComp<SmokableComponent>(maskItem, out var smokable)
                || smokable.State != SmokableState.Lit)
                continue;

            TryUnlockAchievementAsync(session, "oppenheimer")
                .AsTask()
                .FireAndForget();
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _roundProgress.Clear();
    }
    #endregion

    #region Round Progress
    private double AddRoundProgress(Guid userId, string progressType, double amount)
    {
        if (!_roundProgress.TryGetValue(userId, out var progress))
            _roundProgress[userId] = progress = [];

        progress.TryGetValue(progressType, out var current);
        return progress[progressType] = current + amount;
    }

    public double GetRoundProgress(Guid userId, string progressType)
    {
        if (_roundProgress.TryGetValue(userId, out var progress)
            && progress.TryGetValue(progressType, out var value))
            return value;

        return 0;
    }
    #endregion

    #region Helpers

    private string GetCharacterName(ICommonSession session)
    {
        if (session.AttachedEntity is { } attached && TryComp<MetaDataComponent>(attached, out var meta))
            return meta.EntityName;

        return session.Name;
    }

    private void QueueAchievementHydration(ICommonSession session)
    {
        if (session.Status == SessionStatus.Disconnected)
            return;

        if (_nullLinkPlayers.TryGetPlayerData(session.UserId, out var playerData)
            && playerData.AchievementCacheHydrated)
        {
            _nullLinkPlayers.SendAchievementList(session.UserId);
            return;
        }

        if (!_achievementFetchInFlight.Add(session.UserId))
            return;

        HydrateAchievementsAsync(session.UserId)
            .FireAndForget();
    }

    private async Task HydrateAchievementsAsync(Guid userId)
    {
        try
        {
            await _nullLinkPlayers.GetUnlockedAchievements(userId);
        }
        finally
        {
            _achievementFetchInFlight.Remove(userId);
        }

        if (!_nullLinkPlayers.TryGetPlayerData(userId, out var playerData))
            return;

        if (playerData.Session.Status == SessionStatus.Disconnected)
            return;

        if (playerData.AchievementCacheHydrated)
        {
            _nullLinkPlayers.SendAchievementList(userId);
            return;
        }

        Timer.Spawn(AchievementHydrationRetryDelay, () =>
        {
            if (!_playerManager.TryGetSessionById(new NetUserId(userId), out var retrySession))
                return;

            if (retrySession.Status == SessionStatus.Disconnected)
                return;

            QueueAchievementHydration(retrySession);
        });
    }
    #endregion
}
