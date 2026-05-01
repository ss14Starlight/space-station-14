using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared._Starlight.Mech.Components;
using Content.Shared.Actions;
using Content.Shared.Movement.Components;
using Content.Shared.Power;

namespace Content.Server._Starlight.Mech.EntitySystems;

/// <summary>
/// Handles Mech thruster behavior
/// </summary>
public sealed partial class MechThrustersSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechThrustersComponent, BeforePilotInsertEvent>(OnPilotEntering);
        SubscribeLocalEvent<MechThrustersComponent, BeforePilotEjectEvent>(OnPilotEjecting);
    }

    // This can probably go in shared
    private void OnPilotEntering(EntityUid uid, MechThrustersComponent comp, ref BeforePilotInsertEvent args)
    {

    }

    private void OnPilotEjecting(EntityUid uid, MechThrustersComponent comp, ref BeforePilotEjectEvent args)
    {

    }

    private void OnMechToggleThrusters(EntityUid uid, MechThrustersComponent comp, MechToggleThrustersEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<MechThrustersComponent>(uid, out var mechComp))
            return;

        args.Handled = true;

        comp.ThrustersEnabled = !comp.ThrustersEnabled;

        _actions.SetToggled(comp.MechToggleThrustersActionEntity, comp.ThrustersEnabled);

        if (comp.ThrustersEnabled)
        {
            AddComp<CanMoveInAirComponent>(uid);
            AddComp<MovementAlwaysTouchingComponent>(uid);
        }
        else
        {
            RemComp<CanMoveInAirComponent>(uid);
            RemComp<MovementAlwaysTouchingComponent>(uid);
        }

        Dirty(uid, comp);
    }
}
