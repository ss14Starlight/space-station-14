using System.Threading.Tasks;
using Content.Shared._NullLink;
using Starlight.NullLink.Event;

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

        // Authoritative manager notifies the client; skipNullLink semantics via TrySetResources (no outbound NullLink).
        _playerResourcesManager.TrySetResources(playerData.Session, playerData.Resources);
        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateResource(ResourceChangedEvent ev)
    {
        if (!_resourcesEnabled
            || !_playerById.TryGetValue(ev.Player, out var playerData))
            return ValueTask.CompletedTask;

        playerData.Resources[ev.Resource] = ev.NewAmount;

        _playerResourcesManager.TrySetResource(playerData.Session, ev.Resource, ev.NewAmount, skipNullLink: true);
        return ValueTask.CompletedTask;
    }
}
