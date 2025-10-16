using System.Linq;
using Content.Shared.Fax.Components;
using Content.Shared.Paper;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Paper;

public sealed class ActionsOnSignSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActionsOnSignComponent, PaperSignedEvent>(OnPaperSigned);
        SubscribeLocalEvent<ActionsOnSignComponent, MapInitEvent>(OnMapInit);
    }
    
    private void OnMapInit(EntityUid uid, ActionsOnSignComponent comp, MapInitEvent init)
    {
        if (comp.KeepFaxable) 
            return;
        RemComp<FaxableObjectComponent>(uid); //cause this breaks shit like infinite antags
    }

    private void OnPaperSigned(EntityUid uid, ActionsOnSignComponent component, PaperSignedEvent args)
    {
        if (component.Charges <= 0)
            return;
        var target = args.Signer;
        if (component.Signers.Contains(target))
            return;
        component.Charges--;
        component.Signers.Add(target);

        var proto = _prototypeManager.Index<OnSignActionsPrototype>(component.OnSignActionProto.Id);
        if (component.Instant)
        {
            PerformActions(proto, uid,component, [target]);
        }
        else if (component.Charges == 0)
        {
            PerformActions(proto, uid, component,  component.Signers.AsEnumerable());
        }
    }

    private void PerformActions(OnSignActionsPrototype proto, EntityUid paper, ActionsOnSignComponent component, IEnumerable<EntityUid> targets)
    {
        foreach (var action in proto.Actions)
        {
            if (!action.IoCInjected)
            {
                action.ResolveIoC();
                action.IoCInjected = true;
            }
            
            if (action.TargetsPaper)
            {
                if (action.Action(paper, component, paper))
                    return;
            }
            else
            {
                foreach (var target in targets)
                {
                    if (action.Action(paper, component, target))
                        return;
                }
            }
        }
    }
}