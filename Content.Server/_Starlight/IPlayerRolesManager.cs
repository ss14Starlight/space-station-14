using Content.Shared.Starlight;
using static Content.Server.Starlight.PlayerRolesManager;

namespace Content.Server.Administration.Managers;

public interface IPlayerRolesManager : ISharedPlayersRoleManager
{
    IEnumerable<PlayerReg> Players { get; }

    void Initialize();
}
