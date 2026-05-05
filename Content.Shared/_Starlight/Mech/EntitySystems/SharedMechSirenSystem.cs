using Content.Shared._Starlight.Mech.Components;
using Content.Shared.Actions;
using Content.Shared.Mech;

namespace Content.Shared._Starlight.Mech.EntitySystems;

public abstract partial class SharedMechSirenSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechSirenComponent, BeforePilotInsertEvent>(OnPilotInserted);
        SubscribeLocalEvent<MechSirenComponent, MechToggleSirensEvent>(OnMechToggleSirens);
    }

    private void OnPilotInserted(EntityUid uid, MechSirenComponent comp, ref BeforePilotInsertEvent args)
    {
        _actions.AddAction(args.Pilot, ref comp.MechToggleSirenActionEntity, comp.MechToggleSirenAction, uid);
    }

    private void OnMechToggleSirens(EntityUid uid, MechSirenComponent component, MechToggleSirensEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        component.Toggled = !component.Toggled;

        _actions.SetToggled(component.MechToggleSirenActionEntity, component.Toggled);

        _appearance.SetData(uid, MechVisualLayers.Siren, component.Toggled);

        Dirty(uid, component);
    }
}
