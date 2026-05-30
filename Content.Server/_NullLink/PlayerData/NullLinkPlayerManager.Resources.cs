using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using Content.Shared._NullLink;
using Robust.Shared.Player;
using Starlight.NullLink.Event;
using Content.Shared.NullLink.CCVar;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayerManager : INullLinkPlayerManager
{
    public ValueTask SyncResources(PlayerResourcesSyncEvent ev)
    {
        if (!_resourcesEnabled
            || !_playerById.TryGetValue(ev.Player, out var playerData))
            return ValueTask.CompletedTask;
        playerData.Resources.Clear();

        foreach (var resource in ev.Resources)
            playerData.Resources[resource.Key] = resource.Value;

        SendPlayerResources(playerData.Session, playerData.Resources);
        _playerResourcesManager.TrySetResources(playerData.Session, playerData.Resources);
        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateResource(ResourceChangedEvent ev)
    {
        if (!_resourcesEnabled
            || !_playerById.TryGetValue(ev.Player, out var playerData))
            return ValueTask.CompletedTask;
        playerData.Resources[ev.Resource] = ev.NewAmount;

        SendPlayerResources(playerData.Session, playerData.Resources);
        _playerResourcesManager.TrySetResources(playerData.Session, playerData.Resources);
        return ValueTask.CompletedTask;
    }

    private void SendPlayerResources(ICommonSession session, Dictionary<string, double> resources)
        => _netMgr.ServerSendMessage(new MsgUpdatePlayerResources
        {
            Resources = resources
        }, session.Channel);
}
