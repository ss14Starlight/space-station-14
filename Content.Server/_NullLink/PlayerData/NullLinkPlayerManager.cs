using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._NullLink.Core;
using Content.Server._NullLink.Helpers;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._NullLink;
using Content.Shared.NullLink.CCVar;
using Robust.Server.Player;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayerManager : INullLinkPlayerManager
{
    [Dependency] private readonly IActorRouter _actors = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IServerNetManager _netMgr = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTimeTrackingManager = default!;
    [Dependency] private readonly ISharedNullLinkPlayerResourcesManager _playerResourcesManager = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly ITaskManager _taskManager = default!;

    private readonly ConcurrentDictionary<Guid, PlayerData> _playerById = [];
    private readonly ConcurrentDictionary<Guid, ICommonSession> _mentors = [];
    private ISawmill _sawmill = default!;
    private RoleRequirementPrototype? _mentorReq;
    private TitleBuilderPrototype? _builder;
    private ServerPlaytimeRecognitionPrototype? _serverPlaytimeRecognition;
    private string? _server;

    private bool _resourcesEnabled = false;

    public IEnumerable<ICommonSession> Mentors => _mentors.Values;
    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("NullLink player data");
        _netMgr.RegisterNetMessage<MsgUpdatePlayerRoles>();
        _netMgr.RegisterNetMessage<MsgUpdatePlayerPlayTime>();
        _netMgr.RegisterNetMessage<MsgUpdatePlayerResources>();
        _playerManager.PlayerStatusChanged += PlayerStatusChanged;
        InitializeLinking();
        _cfg.OnValueChanged(NullLinkCCVars.RoleReqMentors, UpdateMentors, true);
        _cfg.OnValueChanged(NullLinkCCVars.AdminRankBuilder, UpdateAdminBuilder, true);
        _cfg.OnValueChanged(NullLinkCCVars.TitleBuild, UpdateTitleBuilder, true);
        _cfg.OnValueChanged(NullLinkCCVars.Project, UpdateProject, true);
        _cfg.OnValueChanged(NullLinkCCVars.Server, UpdateServer, true);
        _cfg.OnValueChanged(NullLinkCCVars.ResourcesEnabled, UpdateResources, true);

        _actors.OnConnected += OnNullLinkConnected;
    }

    private void UpdateResources(bool obj) => _resourcesEnabled = obj;

    private void OnNullLinkConnected()
    {
        if (!_actors.TryGetServerGrain(out var serverGrain))
            return;

        foreach (var player in _playerById)
            _ = serverGrain.PlayerConnected(player.Key);
    }

    public void Shutdown()
    {
        _actors.OnConnected -= OnNullLinkConnected;
        _playerManager.PlayerStatusChanged -= PlayerStatusChanged;
        _playerById.Clear();
    }

    public bool TryGetPlayerData(Guid userId, [NotNullWhen(true)] out PlayerData? playerData)
        => _playerById.TryGetValue(userId, out playerData);

    private void PlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        switch (e.NewStatus)
        {
            case SessionStatus.Zombie:
            case SessionStatus.Connecting:
                break;
            case SessionStatus.Connected:
                var state = new PlayerData
                {
                    Session = e.Session,
                };
                if (!_playerById.TryAdd(e.Session.UserId, state))
                    _sawmill.Error($"Failed to add player with UserId {e.Session.UserId} to playerById dictionary.");
                if (_actors.TryGetServerGrain(out var serverGrain))
                    serverGrain.PlayerConnected(e.Session.UserId)
                        .FireAndForget(err=> _sawmill.Error($"PlayerConnected dispatch failed: {err}"));
                SendPlayerRoles(e.Session, state.Roles);
                break;
            case SessionStatus.InGame:
                break;
            case SessionStatus.Disconnected:
                if (_actors.TryGetServerGrain(out var serverGrain2))
                    serverGrain2.PlayerDisconnected(e.Session.UserId)
                        .FireAndForget(err => _sawmill.Error($"PlayerDisconnected dispatch failed: {err}"));
                _playerById.Remove(e.Session.UserId, out _);
                _mentors.Remove(e.Session.UserId, out _);
                break;
            default:
                break;
        }
    }

    private void UpdateMentors(string obj)
    {
        if(_mentorReq?.ID == obj)
            return; 

        _mentors.Clear();
        if (!_proto.TryIndex<RoleRequirementPrototype>(obj, out var mentorReq))
            return;
        _mentorReq = mentorReq;

        Pipe.RunInBackground(async () =>
        {
            foreach (var player in _playerById)
            {
                if (_mentorReq?.Roles.Any(player.Value.Roles.Contains) != true)
                    continue;
                _mentors.TryAdd(player.Key, player.Value.Session);
            }
        });
    }

    private void MentorCheck(Guid player, PlayerData playerData)
    {
        if (_mentorReq?.Roles.Any(playerData.Roles.Contains) == true)
            _mentors.TryAdd(player, playerData.Session);
        else
            _mentors.Remove(player, out _);
    }
}
