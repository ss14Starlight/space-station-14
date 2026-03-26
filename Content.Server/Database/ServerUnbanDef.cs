using Robust.Shared.Network;
using Starlight.NullLink;

namespace Content.Server.Database
{
    public sealed class ServerUnbanDef
    {
        public int BanId { get; }

        public NetUserId? UnbanningAdmin { get; }

        public DateTimeOffset UnbanTime { get; }

        public ServerUnbanDef(int banId, NetUserId? unbanningAdmin, DateTimeOffset unbanTime)
        {
            BanId = banId;
            UnbanningAdmin = unbanningAdmin;
            UnbanTime = unbanTime;
        }
    }

    #region Starlight

    public static class UnbanDefExtensions
    {
        public static AdminUnban ToNullLink(this ServerUnbanDef serverUnban) 
            => new(serverUnban.BanId, serverUnban.UnbanningAdmin, serverUnban.UnbanTime);
    }

    #endregion
}