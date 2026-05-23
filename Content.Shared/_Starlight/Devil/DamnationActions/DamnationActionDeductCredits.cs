using Content.Shared._NullLink;

namespace Content.Shared._Starlight.Devil.DamnationActions;

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
