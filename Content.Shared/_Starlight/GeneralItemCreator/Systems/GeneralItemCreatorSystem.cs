using Content.Shared.Actions;
using Content.Shared._Starlight.GeneralItemCreator.Components;

namespace Content.Shared._Starlight.GeneralItemCreator.Systems;

/// <summary>
/// Handles predicting that the action exists, creating items is done serverside.
/// </summary>
public abstract partial class SharedGeneralItemCreatorSystem : EntitySystem
{
    [Dependency] private ActionContainerSystem _actionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GeneralItemCreatorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GeneralItemCreatorComponent, GetItemActionsEvent>(OnGetActions);
    }

    private void OnMapInit(Entity<GeneralItemCreatorComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        // test funny dont mind me
        if (string.IsNullOrEmpty(comp.Action))
            return;

        _actionContainer.EnsureAction(uid, ref comp.ActionEntity, comp.Action);
        Dirty(uid, comp);
    }

    private void OnGetActions(Entity<GeneralItemCreatorComponent> ent, ref GetItemActionsEvent args)
    {
        args.AddAction(ent.Comp.ActionEntity);
    }
}
