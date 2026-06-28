using Robust.Shared.Player;

namespace Content.Shared._Starlight;

public interface ISharedPlayersRoleManager
{

    PlayerData? GetPlayerData(EntityUid uid);
    PlayerData? GetPlayerData(ICommonSession session);
}
