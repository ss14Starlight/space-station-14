using Robust.Shared.Network;
using Starlight.NullLink;

namespace Content.Server.Database;

public sealed class ServerRoleUnbanDef
{
    public int BanId { get; }

    public NetUserId? UnbanningAdmin { get; }

    public DateTimeOffset UnbanTime { get; }

    public ServerRoleUnbanDef(int banId, NetUserId? unbanningAdmin, DateTimeOffset unbanTime)
    {
        BanId = banId;
        UnbanningAdmin = unbanningAdmin;
        UnbanTime = unbanTime;
    }
}

#region Starlight

public static class RoleUnbanDefExtensions
{
    public static AdminUnban ToNullLink(this ServerRoleUnbanDef serverRoleUnban)
        => new(serverRoleUnban.BanId, serverRoleUnban.UnbanningAdmin, serverRoleUnban.UnbanTime);
}

#endregion