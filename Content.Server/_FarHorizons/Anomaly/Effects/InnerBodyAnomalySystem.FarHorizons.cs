using System.Linq;
using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using static Robust.Shared.Prototypes.EntityPrototype;

namespace Content.Server.Anomaly.Effects;

public sealed partial class InnerBodyAnomalySystem
{
    [Dependency] private readonly ActionGrantSystem _actionGrant = default!;

    private void AddComponentsCarefully(EntityUid target, ComponentRegistry components)
    {
        foreach (var comp in components)
        {
            switch(comp.Key) 
            {
                case "ActionGrant":
                    if (comp.Value.Component is ActionGrantComponent actionGrantComp)
                        HandleActionGrantAdd(target, actionGrantComp);
                    break;
                default: 
                    EntityManager.AddComponent(target, comp.Value);
                    break;
            }
        }
    }

    private void RemoveComponentsCarefully(EntityUid target, ComponentRegistry components)
    {
        foreach (var comp in components)
        {
            switch(comp.Key) 
            {
                case "ActionGrant":
                    if (comp.Value.Component is ActionGrantComponent actionGrantComp)
                        HandleActionGrantRemove(target, actionGrantComp);
                    break;
                default: 
                    EntityManager.RemoveComponent(target, comp.Value.Component);
                    break;
            }
        }
    }

    private void HandleActionGrantAdd(EntityUid target, ActionGrantComponent component)
    {
        if (!TryComp<ActionGrantComponent>(target, out var comp))
        {
            EntityManager.AddComponent(target, component);
            return;
        }

        _actionGrant.AddActions((target, comp), component.Actions);
    }

    private void HandleActionGrantRemove(EntityUid target, ActionGrantComponent component)
    {
        if (!TryComp<ActionGrantComponent>(target, out var comp))
        {
            EntityManager.AddComponent(target, component);
            return;
        }

        _actionGrant.RemoveActions((target, comp), component.Actions);
    }
}