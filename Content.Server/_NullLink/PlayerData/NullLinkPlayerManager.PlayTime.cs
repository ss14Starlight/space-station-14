using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Server._NullLink.Core;
using Content.Server._NullLink.Helpers;
using Content.Server.Database;
using Content.Shared._NullLink;
using Content.Shared.NullLink.CCVar;
using Content.Shared.Starlight;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Starlight.NullLink;
using Starlight.NullLink.Event;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayerManager : INullLinkPlayerManager
{
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
        if (!_proto.TryIndex<ServerPlaytimeRecognitionPrototype>(obj, out var serverPlaytimeRecognition))
            return;

        _serverPlaytimeRecognition = serverPlaytimeRecognition;
    }

    private void UpdateServer(string obj) => _server = obj;
}
