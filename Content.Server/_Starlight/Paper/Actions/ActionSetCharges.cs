namespace Content.Server._Starlight.Paper.Actions;

public sealed partial class ActionSetCharges : OnSignAction
{
    /// <summary>
    /// how many charges should the paper have?
    /// </summary>
    [DataField]
    public int Charges = 1;
    
    public override bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target)
    {
        component.Charges = Charges;
        return false;
    }

    public override void ResolveIoC(){}
}