using Content.Server._Starlight.NullSpace;
using Content.Server.DoAfter;
using Content.Shared._Starlight.NullSpace;
using Content.Shared._Starlight.Shadekin;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Teleportation.Components;

namespace Content.Server._Starlight.Shadekin;

public sealed partial class ShadekinSystem : EntitySystem
{
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    public void InitializeAbilities()
    {
        SubscribeLocalEvent<BrighteyeComponent, BrighteyePortalActionEvent>(OnPortalAction);
        SubscribeLocalEvent<BrighteyeComponent, BrighteyePhaseActionEvent>(OnPhaseAction);
        SubscribeLocalEvent<BrighteyeComponent, PhaseDoAfterEvent>(OnPhaseDoAfter);
    }

    private void OnPortalAction(EntityUid uid, BrighteyeComponent component, BrighteyePortalActionEvent args)
    {
        if (HasComp<NullSpaceComponent>(uid)) // No making portals while in nullspace!
        {
            args.Handled = true;
            return;
        }

        bool onStation = false;
        foreach (var station in _station.GetStations()) // Lets make sure the Portal **IS ON STATION!**
        {
            if (_station.GetLargestGrid(station) is not { } grid)
                continue;

            if (Transform(uid).GridUid != grid)
                continue;

            onStation = true;
        }

        if (!onStation)
        {
            args.Handled = true;
            return;
        }

        if (OnAttemptEnergyUse(uid, component, component.PortalCost))
        {
            _actionsSystem.RemoveAction(uid, component.PortalAction);

            EnsureComp<PortalTimeoutComponent>(uid); // Lets not teleport as soon we put down the portal, duh.

            var newportal = SpawnAtPosition(component.PortalShadekin, Transform(uid).Coordinates);
            if (TryComp<DarkPortalComponent>(newportal, out var portal))
                portal.Brighteye = uid;

            component.Portal = newportal;

            _alerts.ClearAlert(uid, component.PortalAlert);
        }

        args.Handled = true;
    }

    private void OnPhaseAction(EntityUid uid, BrighteyeComponent component, BrighteyePhaseActionEvent args)
    {
        int cost = component.PhaseCost;
        if (HasComp<NullSpaceComponent>(uid))
        {
            if (_nullspace.CanPhase(uid) && OnAttemptEnergyUse(uid, component))
                _nullspace.Phase(uid);

            args.Handled = true;
            return;
        }

        if (TryComp<ShadekinComponent>(uid, out var shadekin))
        {
            if (shadekin.CurrentState == ShadekinState.Extreme)
                return;
            else if (shadekin.CurrentState == ShadekinState.High)
                cost = component.MaxEnergy;
            else if (shadekin.CurrentState == ShadekinState.Annoying)
                cost *= 3;
            else if (shadekin.CurrentState == ShadekinState.Low)
                cost *= 2;
        }

        if (TryComp<PullerComponent>(uid, out var puller) && puller.Pulling is not null)
        {
            var doAfter = new PhaseDoAfterEvent()
            {
                Cost = cost,
            };

            _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, uid, TimeSpan.FromSeconds(10), doAfter, uid, puller.Pulling)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                BlockDuplicate = true
            });
        }
        else if (_nullspace.CanPhase(uid) && OnAttemptEnergyUse(uid, component, cost))
            _nullspace.Phase(uid);

        args.Handled = true;
    }

    private void OnPhaseDoAfter(EntityUid uid, BrighteyeComponent component, PhaseDoAfterEvent args)
    {
        if (!args.Args.Target.HasValue || args.Handled || args.Cancelled)
            return;

        if (!_nullspace.CanPhase(uid) || !OnAttemptEnergyUse(uid, component, args.Cost))
            return;

        EnsureComp<NullSpacePulledComponent>(args.Args.Target.Value);
        _nullspace.Phase(uid);

        args.Handled = true;
    }
}