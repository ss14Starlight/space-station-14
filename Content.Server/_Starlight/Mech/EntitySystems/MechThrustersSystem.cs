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
        SubscribeLocalEvent<MechThrustersComponent, GetPassiveChargeDrawRate>(OnGetDrawRate);
        SubscribeLocalEvent<MechThrustersComponent, MechToggleThrustersEvent>(OnMechToggleThrusters);
    }

    // This can probably go in shared
    private void OnPilotEntering(EntityUid uid, MechThrustersComponent comp, ref BeforePilotInsertEvent args)
        => _actions.AddAction(args.Pilot, ref comp.MechToggleThrustersActionEntity, comp.MechToggleThrustersAction, uid);

    private void OnGetDrawRate(EntityUid uid, MechThrustersComponent comp, GetPassiveChargeDrawRate args)
        => args.CumulativeDrawRate += comp.ThrustersEnabled ? comp.DrawRate : 0f;

    private void OnMechToggleThrusters(EntityUid uid, MechThrustersComponent comp, MechToggleThrustersEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<MechComponent>(uid, out _))
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
