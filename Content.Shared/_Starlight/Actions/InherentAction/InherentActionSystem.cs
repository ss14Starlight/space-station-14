using Content.Shared.Actions;

namespace Content.Shared._Starlight.Actions.InherentAction;

public abstract class SharedInherentActionSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InherentActionComponent, MapInitEvent>(OnStartup);
        SubscribeLocalEvent<InherentActionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<InherentActionComponent, GetItemActionsEvent>(OnGetItemActions);
    }

    private void OnGetItemActions(Entity<InherentActionComponent> ent, ref GetItemActionsEvent args)
    {
        if (ent.Comp.IsEquipment)
            args.AddAction(ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnStartup(EntityUid uid, InherentActionComponent component, MapInitEvent args)
    {
        if (component.IsEquipment)
        {
            if (_actionContainer.EnsureAction(uid, ref component.ActionEntity, out var action, component.Action))
                _action.SetEntityIcon((component.ActionEntity.Value, action), uid);
        }
        else
            _action.AddAction(uid, ref component.ActionEntity, component.Action);

        Dirty(uid, component);
    }

    private void OnShutdown(EntityUid uid, InherentActionComponent component, ComponentShutdown args)
    {
        if (Deleted(uid) || component.ActionEntity is null)
            return;

        if (component.IsEquipment)
        {
            _actionContainer.RemoveAction(component.ActionEntity.Value);
        }
        else
            _action.RemoveAction((uid, null), component.ActionEntity);
    }
}
