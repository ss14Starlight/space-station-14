using Content.Server.Actions;
using Content.Server.Popups;
using Content.Shared._Starlight.Administration.Components;
using Content.Shared._Starlight.Administration.Events;
using Content.Shared.Eye;
using Content.Shared.Ghost;
using Content.Shared.Tag;
using Robust.Server.GameObjects;

namespace Content.Server._Starlight.Administration.Systems;

public sealed partial class AdminGhostSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private VisibilitySystem _visibility = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdminGhostComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AdminGhostComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ToggleAGhostHideEvent>(OnToggleAGhostHide);
        SubscribeLocalEvent<AdminGhostComponent, GetVisMaskEvent>(OnGhostVis);
    }

    private void OnMapInit(Entity<AdminGhostComponent> entity, ref MapInitEvent args) =>
        _actions.AddAction(entity.Owner, ref entity.Comp.ToggleAGhostHideActionEntity,
            entity.Comp.ToggleAGhostHideActionId);

    private void OnShutdown(Entity<AdminGhostComponent> entity, ref ComponentShutdown args) =>
        _actions.RemoveAction(entity.Owner, entity.Comp.ToggleAGhostHideActionEntity);

    private void OnToggleAGhostHide(ToggleAGhostHideEvent args)
    {
        if (!TryComp<AdminGhostComponent>(args.Performer, out var aghostComp))
            return;
        var popupLocId = !aghostComp.HiddenFromNonAdminGhosts
            ? "ghost-gui-aghost-toggle-ghost-visibility-popup-on"
            : "ghost-gui-aghost-toggle-ghost-visibility-popup-off";
        if (!aghostComp.HiddenFromNonAdminGhosts)
        {
            _visibility.RemoveLayer(args.Performer, (int)VisibilityFlags.Normal, false);
            _visibility.RemoveLayer(args.Performer, (int)VisibilityFlags.Ghost, false);
            _visibility.AddLayer(args.Performer, (int)VisibilityFlags.Admin, false);
            aghostComp.HiddenFromNonAdminGhosts = true;
        }
        else
        {
            _visibility.RemoveLayer(args.Performer, (int)VisibilityFlags.Admin, false);
            if(TryComp<GhostComponent>(args.Performer, out var ghost) && ghost.AlwaysVisible)
                _visibility.AddLayer(args.Performer, (int)VisibilityFlags.Normal, false);
            else _visibility.AddLayer(args.Performer, (int)VisibilityFlags.Ghost, false);
            aghostComp.HiddenFromNonAdminGhosts = false;
        }
        _visibility.RefreshVisibility(args.Performer);
        _popup.PopupEntity(Loc.GetString(popupLocId), args.Performer, args.Performer);
        args.Handled = true;
    }

    private void OnGhostVis(Entity<AdminGhostComponent> entity, ref GetVisMaskEvent args)
    {
        if (entity.Comp.LifeStage <= ComponentLifeStage.Running)
            args.VisibilityMask |= (int)VisibilityFlags.Ghost | (int)VisibilityFlags.Net |
                                   (int)VisibilityFlags.NullSpace | (int)VisibilityFlags.Admin;
    }
}
