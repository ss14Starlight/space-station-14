using Content.Server._Starlight.NullSpace;
using Content.Shared._Starlight.NullSpace;
using Content.Shared._Starlight.Shadekin;
using Content.Shared.DoAfter;
using Content.Shared.Ensnaring.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Teleportation.Components;
using Content.Shared.Trigger;
using Robust.Shared.Map.Components;

namespace Content.Server._Starlight.Shadekin;

public sealed partial class ShadekinSystem : EntitySystem
{
    public void InitializeAbilities()
    {
        SubscribeLocalEvent<BrighteyeComponent, BrighteyePortalActionEvent>(OnPortalAction);
        SubscribeLocalEvent<BrighteyeComponent, BrighteyePhaseActionEvent>(OnPhaseAction);
        SubscribeLocalEvent<BrighteyeComponent, BrighteyeDarkTrapActionEvent>(OnDarkTrapAction);
        SubscribeLocalEvent<BrighteyeComponent, BrighteyeCreateShadeActionEvent>(OnCreateShadeAction);
        SubscribeLocalEvent<BrighteyeComponent, BrighteyeShadeSkipActionEvent>(OnShadeskipAction);
        SubscribeLocalEvent<BrighteyeComponent, PhaseDoAfterEvent>(OnPhaseDoAfter);

        SubscribeLocalEvent<DarkTrapComponent, TriggerEvent>(DarkTrapOnTrigger);
    }

    #region  Shadeskip

    private void OnShadeskipAction(EntityUid uid, BrighteyeComponent component, BrighteyeShadeSkipActionEvent args)
    {
        int cost = component.ShadeSkipCost;
        if (HasComp<NullSpaceComponent>(uid))
            return;

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

        if (OnAttemptEnergyUse(uid, component, cost))
        {
            _stunSystem.TryUpdateStunDuration(args.Target, component.ShadeSkipStunAmount);
            _stunSystem.TryKnockdown(args.Target, component.ShadeSkipStunAmount, force: true);
            _status.TryAddStatusEffectDuration(args.Target, "StatusEffectTemporaryBlindness", component.ShadeSkipStunAmount);
            _status.TryAddStatusEffectDuration(args.Target, "StatusEffectTheDark", TimeSpan.FromSeconds(70));

            args.Handled = true;
        }
    }

    #endregion

    #region Create Shade

    private void OnCreateShadeAction(EntityUid uid, BrighteyeComponent component, BrighteyeCreateShadeActionEvent args)
    {
        if (OnAttemptEnergyUse(uid, component, component.CreateShadeCost))
        {
            var shadegen = SpawnAttachedTo("ShadekinShadegen", Transform(uid).Coordinates);
            _transform.SetParent(shadegen, uid);

            args.Handled = true;
        }
    }

    #endregion

    #region DarkTrap

    private void OnDarkTrapAction(EntityUid uid, BrighteyeComponent component, BrighteyeDarkTrapActionEvent args)
    {
        if (HasComp<NullSpaceComponent>(uid))
            return;

        if (!HasComp<MapGridComponent>(Transform(uid).GridUid)) // Trap need to be on a grid! duh!
            return;

        // DarkTraps can only be spawned in the dark!
        if (TryComp<ShadekinComponent>(uid, out var shadekin))
            if (shadekin.CurrentState != ShadekinState.Dark)
            {
                _popup.PopupEntity(Loc.GetString("shadekin-too-bright"), uid, uid, PopupType.MediumCaution);
                return;
            }

        if (OnAttemptEnergyUse(uid, component, component.DarkTrapCost))
        {
            SpawnAtPosition(component.ShadekinTrap, Transform(uid).Coordinates);
            args.Handled = true;
        }
    }

    private void DarkTrapOnTrigger(Entity<DarkTrapComponent> ent, ref TriggerEvent args)
    {
        if (args.User is null)
            return;

        var darknet = Spawn(ent.Comp.DarkNet);
        if (TryComp<EnsnaringComponent>(darknet, out var ensnaringComp) && _ensnareable.TryEnsnare(args.User.Value, darknet, ensnaringComp))
        {
            _popup.PopupEntity(Loc.GetString("shadekinTrap-trigger", ("user", args.User.Value)), args.User.Value, PopupType.LargeCaution);
            if (TryComp<DarkTrapComponent>(darknet, out var darktrapcomp))
            {
                _stunSystem.TryUpdateStunDuration(args.User.Value, darktrapcomp.StunAmount);
                _stunSystem.TryKnockdown(args.User.Value, darktrapcomp.StunAmount, force: true);
                _status.TryAddStatusEffectDuration(args.User.Value, "StatusEffectTheDark", TimeSpan.FromSeconds(70));
            }

            _audio.PlayPvs(ensnaringComp.EnsnareSound, args.User.Value);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("shadekinTrap-trigger-fail"), args.User.Value, PopupType.MediumCaution);
            QueueDel(darknet);
        }
    }

    #endregion
    #region Portal

    private void OnPortalAction(EntityUid uid, BrighteyeComponent component, BrighteyePortalActionEvent args)
    {
        if (HasComp<NullSpaceComponent>(uid)) // No making portals while in nullspace!
        {
            args.Handled = true;
            return;
        }

        if (component.PortalNeedStation)
        {
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
        }

        if (OnAttemptEnergyUse(uid, component, component.PortalCost))
        {
            SpawnTheDark();

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

    #endregion
    #region  Phase

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

    #endregion
}
