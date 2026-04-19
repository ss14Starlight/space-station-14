using Robust.Shared.Network;
using Starlight.NullLink;

namespace Content.Server.Database;

public sealed class ServerRoleUnbanDef
{
    public int BanId { get; }

    public NetUserId? UnbanningAdmin { get; }

    public DateTimeOffset UnbanTime { get; }

    public string? ProjectName { get; }

    public string? ServerName { get; }

    public ServerRoleUnbanDef(int banId, NetUserId? unbanningAdmin, DateTimeOffset unbanTime, string? projectName = null, string? serverName = null)
    {
        BanId = banId;
        UnbanningAdmin = unbanningAdmin;
        UnbanTime = unbanTime;
        ProjectName = projectName;
        ServerName = serverName;
    }
}

#region Starlight

public static class RoleUnbanDefExtensions
{
    public static AdminUnban ToNullLink(this ServerRoleUnbanDef serverRoleUnban)
        => new(serverRoleUnban.BanId, serverRoleUnban.UnbanningAdmin, serverRoleUnban.UnbanTime, serverRoleUnban.ProjectName, serverRoleUnban.ServerName);
}

#endregion
