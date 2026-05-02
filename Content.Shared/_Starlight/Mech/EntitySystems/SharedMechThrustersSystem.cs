using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared._Starlight.Mech.Components;
using Content.Shared.Actions;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;

namespace Content.Shared._Starlight.Mech.EntitySystems;

/// <summary>
/// Handles Mech thruster behavior
/// </summary>
// TODO: move to shared plz
public sealed partial class SharedMechThrustersSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechThrustersComponent, BeforePilotInsertEvent>(OnPilotEntering);
        SubscribeLocalEvent<MechThrustersComponent, GetPassiveChargeDrawRate>(OnGetDrawRate);
        SubscribeLocalEvent<MechThrustersComponent, MechToggleThrustersEvent>(OnMechToggleThrusters);
        SubscribeLocalEvent<MechThrustersComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<GravityChangedEvent>(OnGravityChanged);
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

        var xform = Transform(uid);
        // no jetpacking on grids
        if (xform.GridUid.HasValue && HasComp<GravityComponent>(xform.GridUid))
            return;

        args.Handled = true;

        SetThrustersEnabled(uid, comp, !comp.ThrustersEnabled);
    }

    private void SetThrustersEnabled(EntityUid uid, MechThrustersComponent comp, bool enabled)
    {
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

    private void OnGravityChanged(ref GravityChangedEvent args)
    {
        var gridIndex = args.ChangedGridIndex;
        var thrusterQuery = EntityQueryEnumerator<MechThrustersComponent>();
        while (thrusterQuery.MoveNext(out var uid, out var comp))
        {
            var xform = Transform(uid);
            if (xform.GridUid != gridIndex)
                continue;

            if (args.HasGravity && comp.ThrustersEnabled)
                SetThrustersEnabled(uid, comp, false);
        }
    }
}
