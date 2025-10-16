namespace Content.Server._Starlight.Paper.Actions;

public sealed partial class ActionClearSigners : OnSignAction
{
    public override bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target)
    {
        component.Signers = [];
        return false;
    }

    public override void ResolveIoC(){}
}