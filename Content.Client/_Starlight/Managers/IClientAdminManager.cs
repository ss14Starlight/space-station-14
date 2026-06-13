using Content.Shared.Starlight;

namespace Content.Client._Starlight.Managers;

public interface IClientPlayerRolesManager
{
    event Action PlayerStatusUpdated;

    PlayerData? GetPlayerData();

    void Initialize();
}
