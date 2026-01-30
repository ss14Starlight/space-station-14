using Content.Shared.Whitelist;

namespace Content.Server._Starlight.Paper.Actions;

public sealed partial class ActionWhitelistCheck : OnSignAction
{
    /// <summary>
    /// A Whitelist of whos signatures are valid
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist = null;

    /// <summary>
    /// A Whitelist of whos signatures are invalid
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist = null;
    
    private EntityWhitelistSystem _whitelistSystem = default!;
    
    public override bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target)
    {
        if (!_whitelistSystem.CheckBoth(target, Blacklist, Whitelist))
        {
            component.Signers.Remove(target); //if they cant sign it we remove them from the list and refund the charge they used.
            component.Charges += 1;
            return true;
        }
        return false;
    }

    public override void ResolveIoC()
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        _whitelistSystem = entMan.System<EntityWhitelistSystem>();
    }
}