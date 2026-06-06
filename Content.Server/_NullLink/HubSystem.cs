using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Maps;
using Content.Server._NullLink.Core;
using Content.Server._NullLink.Helpers;
using Content.Shared._NullLink;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Starlight.NullLink;
using NLServer = Starlight.NullLink.Server;
using NLServerInfo = Starlight.NullLink.ServerInfo;
using Content.Server.GameTicking;
using Content.Server.Administration.Managers;
using Content.Server._NullLink.PlayerData;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Players.PlayTimeTracking;

namespace Content.Server._NullLink;

public sealed partial class HubSystem : EntitySystem, IServerObserver, IServerInfoObserver
{
    private const int MaxEventsPerTick = 2;

    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IActorRouter _actors = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private INullLinkPlayerManager _playerRoles = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IGameMapManager _gameMapManager = default!;
    [Dependency] private GameTicker _gameTicker = default!;

    private ISawmill _sawmill = default!;

    private static readonly TimeSpan _grainDelay = TimeSpan.FromSeconds(180);
    private static readonly TimeSpan _subLifetime = TimeSpan.FromSeconds(16);
    private TimeSpan? _lastResubscribe;

    private readonly Dictionary<ICommonSession, TimeSpan> _subscriptions = [];
    private readonly List<ICommonSession> _toRemove = [];
    private bool _processingResubscribe = false;

    private ConcurrentDictionary<string, NullLink.Server> _serverData = [];
    private ConcurrentDictionary<string, NullLink.ServerInfo> _serverInfoData = [];
    private readonly ConcurrentQueue<NullLink.UpdateEvent> _updateEvents = [];

    public override void Initialize()
    {
        _sawmill = _logManager.GetSawmill("Hub");
        base.Initialize();
        InitializeServer();
        InitializeServerInfo();

        SubscribeNetworkEvent<NullLink.Subscribe>(OnSubscribe);
        SubscribeNetworkEvent<NullLink.Resubscribe>(OnResubscribe);
        SubscribeNetworkEvent<NullLink.Unsubscribe>(OnUnsubscribe);

        _actors.OnConnected += OnNullLinkConnected;
    }

    public override void Shutdown() => _actors.OnConnected -= OnNullLinkConnected;

    private void OnNullLinkConnected()
    {
        _lastResubscribe = null;
        TryUpdateServer();
        TryUpdateServerInfo();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_actors.Enabled) return;

        SendUpdate();

        if (_processingResubscribe || (_timing.RealTime - _lastResubscribe) < _grainDelay) return;
        _processingResubscribe = true;
        Resubscribe();
    }

    private void Resubscribe() => Pipe.RunInBackground(async () =>
    {
        if (!_actors.TryGetGrain<IHubGrain>(0, out var hub)
            || !_actors.TryCreateObjectReference<IServerObserver>(this, out var serverObjectReference)
            || !_actors.TryCreateObjectReference<IServerInfoObserver>(this, out var serverInfoObjectReference))
        {
            _processingResubscribe = false;
            return;
        }

        if (_lastResubscribe == null)
        {
            var serverData = await hub.GetAndSubscribe(serverObjectReference!);
            _serverData = new(serverData.Select(x => KeyValuePair.Create(x.Key, Map(x.Value))));

            var serverInfoData = await hub.GetAndSubscribe(serverInfoObjectReference!);
            _serverInfoData = new(serverInfoData.Select(x => KeyValuePair.Create(x.Key, Map(x.Value))));
        }
        else
        {
            await hub.Resubscribe(serverObjectReference!);
            await hub.Resubscribe(serverInfoObjectReference!);
        }

        _processingResubscribe = false;
        _lastResubscribe = _timing.RealTime;

    }, ex =>
    {
        _processingResubscribe = false;
        _sawmill.Log(LogLevel.Warning, ex, "Failed to resubscribe to server data.");
        _lastResubscribe = null;
    });

    private void SendUpdate()
    {
        var processed = 0;
        while (processed < MaxEventsPerTick && _updateEvents.TryDequeue(out var ev))
        {
            foreach (var (session, lastSeen) in _subscriptions)
                if (session.State.Status == SessionStatus.Disconnected || _timing.RealTime - lastSeen > _subLifetime)
                    _toRemove.Add(session);
                else
                    RaiseNetworkEvent(ev, session);

            processed++;
        }

        foreach (var dead in _toRemove)
            _subscriptions.Remove(dead);
        _toRemove.Clear();
    }

    private void OnSubscribe(NullLink.Subscribe _, EntitySessionEventArgs args)
    {
        _subscriptions[args.SenderSession] = _timing.RealTime;
        RaiseNetworkEvent(new NullLink.ServerData
        {
            Servers = _serverData.ToDictionary(x => x.Key, x => x.Value),
            ServerInfo = _serverInfoData.ToDictionary(x => x.Key, x => x.Value)
        }, args.SenderSession);
        SendRequirements(args.SenderSession);
    }

    private void OnResubscribe(NullLink.Resubscribe _, EntitySessionEventArgs args)
    {
        _subscriptions[args.SenderSession] = _timing.RealTime;
        SendRequirements(args.SenderSession);
    }
    private void OnUnsubscribe(NullLink.Unsubscribe _, EntitySessionEventArgs args)
        => _subscriptions.Remove(args.SenderSession);

    public Task Updated(string key, NLServer value)
    {
        if (!value.IsAdultOnly == _isAdultOnly)
            return Task.CompletedTask;

        _serverData.AddOrUpdate(key, Map(value), (k, v) => Map(value));
        _updateEvents.Enqueue(new NullLink.AddOrUpdateServer
        {
            Key = key,
            Server = Map(value)
        });
        return Task.CompletedTask;
    }

    public Task Remove(string key)
    {
        _serverData.TryRemove(key, out _);
        _serverInfoData.TryRemove(key, out _);
        _updateEvents.Enqueue(new NullLink.RemoveServer
        {
            Key = key
        });
        return Task.CompletedTask;
    }

    public Task Updated(string key, NLServerInfo value)
    {
        _serverInfoData.AddOrUpdate(key, Map(value), (k, v) => Map(value));
        _updateEvents.Enqueue(new NullLink.AddOrUpdateServerInfo
        {
            Key = key,
            ServerInfo = Map(value)
        });
        return Task.CompletedTask;
    }

    private static NullLink.Server Map(NLServer server)
        => new()
        {
            Title = server.Title,
            Description = server.Description,
            Type = (NullLink.ServerType)server.Type,
            IsAdultOnly = server.IsAdultOnly,
            ConnectionString = server.ConnectionString,
            PanicBunkerMinAccountAge = server.PanicBunkerMinAccountAge,
            PanicBunkerMinOverallMinutes = server.PanicBunkerMinOverallMinutes
        };
    private static NullLink.ServerInfo Map(NLServerInfo info)
        => new()
        {
            Status = (NullLink.ServerStatus)info.Status,
            Players = info.Players,
            MaxPlayers = info.MaxPlayers,
            PanicBunkerActive = info.PanicBunkerActive,
            СurrentStateStartedAt = info.CurrentStateStartedAt
        };

    private Dictionary<string, bool> BuildRequirements(ICommonSession session)
    {
        var result = new Dictionary<string, bool>();

        var overall = _playTime.TryGetTrackerTime(session, PlayTimeTrackingShared.TrackerOverall, out var time)
            ? time!.Value
            : TimeSpan.Zero;

        foreach (var (key, server) in _serverData)
            if (server.PanicBunkerMinOverallMinutes is { } min)
                result[key] = overall.TotalMinutes < min;

        return result;
    }

    private void SendRequirements(ICommonSession session)
        => RaiseNetworkEvent(new NullLink.ServerRequirements
        {
            InsufficientPlaytime = BuildRequirements(session)
        }, session);
}
