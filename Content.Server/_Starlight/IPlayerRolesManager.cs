using Content.Shared._Starlight;
using static Content.Server._Starlight.PlayerRolesManager;

namespace Content.Server._Starlight;

public interface IPlayerRolesManager : ISharedPlayersRoleManager
{
    IEnumerable<PlayerReg> Players { get; }

    void Initialize();
}
