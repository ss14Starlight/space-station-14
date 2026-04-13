using System.Collections.Immutable;
using System.Linq;
using Robust.Shared.Player;
using Starlight.NullLink.Event;

namespace Content.Server._NullLink.PlayerData;

public sealed class PlayerData
{
    public string? Title { get; set; }
    public required ICommonSession Session { get; init; }
    public ImmutableHashSet<ulong> Roles { get; set; } = [];
    public Dictionary<string, double> Resources = [];
    public Dictionary<string, Dictionary<string, TimeSpan>> RolePlayTimePerServer { get; set; } = [];
    public ulong DiscordId { get; set; }

    public void SyncRoles(PlayerRolesSyncEvent ev) => Roles = [.. ev.Roles];

    public void UpdateRoles(RolesChangedEvent ev)
    {
        var roles = Roles.ToHashSet();
        roles.ExceptWith(ev.Remove);
        roles.UnionWith(ev.Add);
        Roles = [.. roles];
    }
}
