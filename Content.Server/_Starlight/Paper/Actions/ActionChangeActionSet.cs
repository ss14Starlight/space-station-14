using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Paper.Actions;

public sealed partial class ActionChangeActionSet : OnSignAction
{
    /// <summary>
    /// what action set should this paper switch to.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<OnSignActionsPrototype> Actions = default;
    
    public override bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target)
    {
        component.OnSignActionProto = Actions;
        return false;
    }

    public override void ResolveIoC(){}
}