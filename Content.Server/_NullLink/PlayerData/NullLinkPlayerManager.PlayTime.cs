using System.Linq;
using System.Threading.Tasks;
using Content.Shared._NullLink;
using Robust.Shared.Player;
using Starlight.NullLink.Event;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayerManager : INullLinkPlayerManager
{
    public ValueTask SyncPlayTime(PlayerServerPlayTimesSyncEvent ev)
    {
        if (!_playerById.TryGetValue(ev.Player, out var playerData))
            return ValueTask.CompletedTask;

        var newPlayTimes = new Dictionary<string, Dictionary<string, TimeSpan>>(StringComparer.OrdinalIgnoreCase);

        foreach (var serverPlayTime in ev.ServerPlayTimes)
            newPlayTimes[serverPlayTime.Key] = serverPlayTime.Value.ToDictionary(x => x.Tracker, x => x.Time);

        playerData.RolePlayTimePerServer = newPlayTimes;

        SendPlayerPlayTime(playerData.Session, playerData.RolePlayTimePerServer);

        var mergedRoles = new Dictionary<string, TimeSpan>();

        if (!string.IsNullOrEmpty(_server) &&
            _serverPlaytimeRecognition?.Recognition.TryGetValue(_server, out var servers) is true)
        {
            // Local DB already has this server's playtime — only import recognized *other* servers.
            var selfKey = string.IsNullOrEmpty(_serverPlaytimeRecognition.ID)
                ? null
                : $"{_serverPlaytimeRecognition.ID}.{_server}";

            foreach (var server in servers)
            {
                if (selfKey is not null &&
                    string.Equals(server, selfKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!playerData.RolePlayTimePerServer.TryGetValue(server, out var rolesForServer))
                    continue;

                foreach (var (tracker, time) in rolesForServer)
                {
                    if (mergedRoles.ContainsKey(tracker))
                        mergedRoles[tracker] += time;
                    else
                        mergedRoles[tracker] = time;
                }
            }
        }
        else if (!string.IsNullOrEmpty(_server))
        {
            _sawmill.Warning(
                $"NullLink playtime recognition miss for server '{_server}' " +
                $"(project prototype loaded={_serverPlaytimeRecognition is not null}); " +
                "cross-server playtime will not enrich job requirements.");
        }

        _playTimeTrackingManager.EnrichWithNullLink(mergedRoles, ev.Player);
        return ValueTask.CompletedTask;
    }

    private void SendPlayerPlayTime(ICommonSession session, Dictionary<string, Dictionary<string, TimeSpan>> rolePlayTimePerServer)
        => _netMgr.ServerSendMessage(new MsgUpdatePlayerPlayTime
        {
            RolePlayTimePerServer = rolePlayTimePerServer
        }, session.Channel);

    private void UpdateProject(string obj)
    {
        // Match ActorRouter / Hub: project ids are uppercase prototype ids (e.g. SOL).
        obj = obj.ToUpperInvariant();
        if (!_proto.TryIndex<ServerPlaytimeRecognitionPrototype>(obj, out var serverPlaytimeRecognition))
        {
            _serverPlaytimeRecognition = null;
            return;
        }

        _serverPlaytimeRecognition = serverPlaytimeRecognition;
    }

    private void UpdateServer(string obj) => _server = string.IsNullOrWhiteSpace(obj) ? obj : obj.ToLowerInvariant();
}
