using Content.Shared.Actions;
using Content.Shared.Examine;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Toggleable;

namespace Content.Shared._Starlight.CoolingUnit;

public abstract partial class SharedCoolingUnitSystem : EntitySystem
{
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CoolingUnitComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<CoolingUnitComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CoolingUnitComponent, ToggleActionEvent>(OnActionToggle);
    }

    private void OnGetActions(EntityUid uid, CoolingUnitComponent component, GetItemActionsEvent args)
    {
        args.AddAction(ref component.ToggleActionEntity, component.ToggleAction);
        Dirty(uid, component);
    }

    private void OnExamined(EntityUid uid, CoolingUnitComponent component, ExaminedEvent args)
    {
        if (_itemToggle.IsActivated(uid))
            args.PushMarkup(Loc.GetString("coolingunit-on-examine"));
        else
            args.PushMarkup(Loc.GetString("coolingunit-off-examine"));
    }

    private void OnActionToggle(Entity<CoolingUnitComponent> entity, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        _itemToggle.Toggle(entity.Owner, args.Performer);
        args.Handled = true;
    }
}
