using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Robust.Shared.Player;
using Starlight.NullLink.Event;

namespace Content.Server._NullLink.PlayerData;
public interface INullLinkPlayerManager
{
    IEnumerable<ICommonSession> Mentors { get; }

    string GetDiscordAuthUrl(string customState);
    void Initialize();
    void Shutdown();
    ValueTask SyncPlayTime(PlayerServerPlayTimesSyncEvent playTimesSync);
    ValueTask SyncRoles(PlayerRolesSyncEvent ev);
    bool TryGetPlayerData(Guid userId, [NotNullWhen(true)] out PlayerData? playerData);
    ValueTask UpdateRoles(RolesChangedEvent ev);
}