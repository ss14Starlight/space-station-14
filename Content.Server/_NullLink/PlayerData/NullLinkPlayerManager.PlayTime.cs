using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._NullLink;
using Robust.Shared.Player;
using Starlight.NullLink.Event;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayerManager : INullLinkPlayerManager
{
    private static readonly TimeSpan PlayTimeSyncTimeout = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _playTimeSynced = [];

    private void InitializePlayTime()
        => _userDb.AddOnLoadPlayer(WaitForPlayTimeSync);

    private async Task WaitForPlayTimeSync(ICommonSession session, CancellationToken cancel)
    {
        if (!_actors.Enabled || !_actors.TryGetServerGrain(out _))
            return;

        var synced = GetPlayTimeSynced(session.UserId);

        try
        {
            await synced.Task.WaitAsync(PlayTimeSyncTimeout, cancel);
        }
        catch (TimeoutException)
        {
            _sawmill.Warning($"NullLink playtime for {session} did not arrive within {PlayTimeSyncTimeout.TotalSeconds}s, loading preferences with local playtime only.");
        }
    }

    private TaskCompletionSource GetPlayTimeSynced(Guid player)
        => _playTimeSynced.GetOrAdd(player, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

    public ValueTask SyncPlayTime(PlayerServerPlayTimesSyncEvent ev)
    {
        if (!_playerById.TryGetValue(ev.Player, out var playerData))
            return ValueTask.CompletedTask;

        var newPlayTimes = new Dictionary<string, Dictionary<string, TimeSpan>>();

        foreach (var serverPlayTime in ev.ServerPlayTimes)
            newPlayTimes[serverPlayTime.Key] = serverPlayTime.Value.ToDictionary(x => x.Tracker, x => x.Time);

        playerData.RolePlayTimePerServer = newPlayTimes;

        SendPlayerPlayTime(playerData.Session, playerData.RolePlayTimePerServer);

        var mergedRoles = new Dictionary<string, TimeSpan>();

        if (_server is not null && _serverPlaytimeRecognition?.Recognition.TryGetValue(_server, out var servers) is true)
        {
            foreach (var server in servers)
            {
                if (playerData.RolePlayTimePerServer.TryGetValue(server, out var rolesForServer))
                {
                    foreach (var (tracker, time) in rolesForServer)
                    {
                        if (mergedRoles.ContainsKey(tracker))
                            mergedRoles[tracker] += time;
                        else
                            mergedRoles[tracker] = time;
                    }
                }
            }
        }

        var synced = GetPlayTimeSynced(ev.Player);
        _playTimeTrackingManager.EnrichWithNullLink(mergedRoles, ev.Player, () => synced.TrySetResult());
        return ValueTask.CompletedTask;
    }

    private void SendPlayerPlayTime(ICommonSession session, Dictionary<string, Dictionary<string, TimeSpan>> rolePlayTimePerServer)
        => _netMgr.ServerSendMessage(new MsgUpdatePlayerPlayTime
        {
            RolePlayTimePerServer = rolePlayTimePerServer
        }, session.Channel);

    private void UpdateProject(string obj)
    {
        if (!_proto.TryIndex<ServerPlaytimeRecognitionPrototype>(obj, out var serverPlaytimeRecognition))
            return;

        _serverPlaytimeRecognition = serverPlaytimeRecognition;
    }

    private void UpdateServer(string obj) => _server = obj;
}
