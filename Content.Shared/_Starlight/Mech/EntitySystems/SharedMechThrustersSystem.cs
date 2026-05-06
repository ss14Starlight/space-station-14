using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared._Starlight.Mech.Components;
using Content.Shared.Actions;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Power;

namespace Content.Shared._Starlight.Mech.EntitySystems;

/// <summary>
/// Handles Mech thruster behavior
/// </summary>
public sealed partial class SharedMechThrustersSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechThrustersComponent, BeforePilotInsertEvent>(OnPilotEntering);
        SubscribeLocalEvent<MechThrustersComponent, BeforePilotEjectEvent>(OnPilotEjecting);
        SubscribeLocalEvent<MechThrustersComponent, GetPassiveChargeDrawRate>(OnGetDrawRate);
        SubscribeLocalEvent<MechThrustersComponent, MechToggleThrustersEvent>(OnMechToggleThrusters);
        SubscribeLocalEvent<MechThrustersComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<MechThrustersComponent, ChargeChangedEvent>(OnChargeChanged);
    }

    /// <summary>
    /// Adds thruster action to pilot
    /// </summary>
    private void OnPilotEntering(EntityUid uid, MechThrustersComponent comp, ref BeforePilotInsertEvent args)
        => _actions.AddAction(args.Pilot, ref comp.MechToggleThrustersActionEntity, comp.MechToggleThrustersAction, uid);

    /// <summary>
    /// Disables thrusters on pilot ejection
    /// </summary>
    private void OnPilotEjecting(EntityUid uid, MechThrustersComponent comp, ref BeforePilotEjectEvent args)
    {
        SetThrustersEnabled(uid, comp, false);
    }

    /// <summary>
    /// Passive charge draw handling method
    /// </summary>
    private void OnGetDrawRate(EntityUid uid, MechThrustersComponent comp, GetPassiveChargeDrawRate args)
        => args.CumulativeDrawRate += comp.ThrustersEnabled ? comp.DrawRate : 0f;

    /// <summary>
    /// Handles toggling thruster action
    /// </summary>
    private void OnMechToggleThrusters(EntityUid uid, MechThrustersComponent comp, MechToggleThrustersEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<MechComponent>(uid, out var mechComp))
            return;

        var xform = Transform(uid);
        // no jetpacking on grids
        if (xform.GridUid.HasValue && HasComp<GravityComponent>(xform.GridUid))
        {
            var msg = Loc.GetString("mech-thrusters-on-grid");
            var pilot = mechComp.PilotSlot.ContainedEntity;
            _popup.PopupClient(msg, uid, pilot, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        SetThrustersEnabled(uid, comp, !comp.ThrustersEnabled, mechComp);
    }

    /// <summary>
    /// Internal method for managing thruster state
    /// </summary>
    private void SetThrustersEnabled(EntityUid uid, MechThrustersComponent comp, bool enabled, MechComponent? mechComp = null)
    {
        // PilotSlot is not marked as nullable, but it totally can be bc EnsureContainer is called
        // on ComponentStartup and not ComponentInit
        // Usage of pattern sidesteps any warnings thankfully
        if (Resolve(uid, ref mechComp, false)
            && mechComp.PilotSlot is { ContainedEntity: not null }
            && comp.ThrustersEnabled != enabled)
        {
            var msg = enabled ? "mech-thrusters-enabled" : "mech-thrusters-disabled";
            _popup.PopupClient(Loc.GetString(msg), uid, mechComp.PilotSlot.ContainedEntity.Value);
        }

        comp.ThrustersEnabled = enabled;

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

    private void OnParentChanged(EntityUid uid, MechThrustersComponent comp, ref EntParentChangedMessage args)
    {
        if (args.Transform.GridUid.HasValue && HasComp<GravityComponent>(args.Transform.GridUid))
            SetThrustersEnabled(uid, comp, false);
    }

    private void OnChargeChanged(EntityUid uid, MechThrustersComponent comp, ref ChargeChangedEvent args)
    {
        if ((int)(args.CurrentCharge / args.MaxCharge * 100) == 0)
            SetThrustersEnabled(uid, comp, false);
    }
}
