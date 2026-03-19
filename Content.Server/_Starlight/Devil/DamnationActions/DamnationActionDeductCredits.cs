using Content.Shared.Starlight;
using Content.Server.Administration.Managers;
using Content.Shared._Starlight.Devil.DamnationActions;

namespace Content.Server._Starlight.Devil.DamnationActions;

public sealed partial class DamnationActionDeductCredits : DamnationAction
{
    [DataField]
    int Amount = 5000;

    private IPlayerRolesManager _playerRoles = default!;

    public override bool Action(Entity<DamnedComponent> victim)
    {
        /*if (_playerRoles.GetPlayerData(victim) is not PlayerData playerData
            || playerData.Balance < Amount
            || Amount <= 0) return false;
        
        playerData.Balance -= Amount;
        return true;*/

        return false;
    }

    public override void ResolveIoC()
    {
        base.ResolveIoC();
        _playerRoles = IoCManager.Resolve<IPlayerRolesManager>();
    }
}