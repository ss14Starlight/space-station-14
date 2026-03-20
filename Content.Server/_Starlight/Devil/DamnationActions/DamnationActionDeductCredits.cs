using Content.Shared.Starlight;
using Content.Server.Administration.Managers;
using Content.Shared._Starlight.Devil.DamnationActions;
using Content.Shared._NullLink;
using Robust.Shared.Toolshed.Commands.Math;

namespace Content.Server._Starlight.Devil.DamnationActions;

public sealed partial class DamnationActionDeductCredits : DamnationAction
{
    [DataField]
    int Amount = 5000;

    private ISharedNullLinkPlayerResourcesManager _playerResources = default!;

    public override bool Action(Entity<DamnedComponent> victim)
    {
        if (!_playerResources.TryGetResource(victim.Owner, "credits", out var balance) || balance < Amount || balance < 0)
            return false;
        
        return _playerResources.TryUpdateResource(victim.Owner, "credits", -Amount);
    }

    public override void ResolveIoC()
    {
        base.ResolveIoC();
        _playerResources = IoCManager.Resolve<ISharedNullLinkPlayerResourcesManager>();
    }
}