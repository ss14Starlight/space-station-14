using Content.Shared._Starlight.NullSpace.Components;
using Content.Shared._Starlight.Shadekin.Components;
using Content.Shared.DoAfter;
using Content.Shared.Ensnaring.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Teleportation.Components;
using Content.Shared.Trigger;
using Robust.Shared.Map.Components;

namespace Content.Shared._Starlight.Shadekin;

public sealed partial class ShadekinSystem
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

    private void OnShadeskipAction(Entity<BrighteyeComponent> ent, ref BrighteyeShadeSkipActionEvent args)
    {
        var cost = ent.Comp.ShadeSkipCost;
        if (HasComp<NullSpaceComponent>(ent))
            return;

        if (TryComp<ShadekinComponent>(ent, out var shadekin))
        {
            if (shadekin.CurrentState == ShadekinState.Extreme)
                return;

            if (shadekin.CurrentState == ShadekinState.High)
                cost = ent.Comp.MaxEnergy;
            else if (shadekin.CurrentState == ShadekinState.Annoying)
                cost *= 3;
            else if (shadekin.CurrentState == ShadekinState.Low)
                cost *= 2;
        }

        if (!OnAttemptEnergyUse(ent, ent.Comp, cost))
            return;

        _stunSystem.TryUpdateStunDuration(args.Target, ent.Comp.ShadeSkipStunAmount);
        _stunSystem.TryKnockdown(args.Target, ent.Comp.ShadeSkipStunAmount, force: true);
        _status.TryAddStatusEffectDuration(args.Target, "StatusEffectTemporaryBlindness", ent.Comp.ShadeSkipStunAmount);
        _status.TryAddStatusEffectDuration(args.Target, "StatusEffectTheDark", TimeSpan.FromSeconds(70));

        args.Handled = true;
    }

    #endregion

    #region Create Shade

    private void OnCreateShadeAction(Entity<BrighteyeComponent> ent, ref BrighteyeCreateShadeActionEvent args)
    {
        if (!OnAttemptEnergyUse(ent, ent.Comp, ent.Comp.CreateShadeCost))
            return;

        var shadegen = PredictedSpawnAttachedTo("ShadekinShadegen", Transform(ent).Coordinates);
        _transform.SetParent(shadegen, ent);

        args.Handled = true;
    }

    #endregion

    #region DarkTrap

    private void OnDarkTrapAction(Entity<BrighteyeComponent> ent, ref BrighteyeDarkTrapActionEvent args)
    {
        if (HasComp<NullSpaceComponent>(ent))
            return;

        if (!HasComp<MapGridComponent>(Transform(ent).GridUid)) // Trap need to be on a grid! duh!
            return;

        // DarkTraps can only be spawned in the dark!
        if (TryComp<ShadekinComponent>(ent, out var shadekin))
            if (shadekin.CurrentState != ShadekinState.Dark)
            {
                _popup.PopupClient(Loc.GetString("shadekin-too-bright"), ent, ent, PopupType.MediumCaution);
                return;
            }

        if (OnAttemptEnergyUse(ent, ent.Comp, ent.Comp.DarkTrapCost))
        {
            PredictedSpawnAtPosition(ent.Comp.ShadekinTrap, Transform(ent).Coordinates);
            args.Handled = true;
        }
    }

    private void DarkTrapOnTrigger(Entity<DarkTrapComponent> ent, ref TriggerEvent args)
    {
        if (args.User is null)
            return;

        var darknet = EntityManager.PredictedSpawn(ent.Comp.DarkNet);
        if (TryComp<EnsnaringComponent>(darknet, out var ensnaringComp) && _ensnareable.TryEnsnare(args.User.Value, darknet, ensnaringComp))
        {
            _popup.PopupPredicted(Loc.GetString("shadekinTrap-trigger", ("user", args.User.Value)), args.User.Value, args.User.Value, PopupType.LargeCaution);
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
            _popup.PopupPredicted(Loc.GetString("shadekinTrap-trigger-fail"), args.User.Value, args.User.Value, PopupType.MediumCaution);
            PredictedQueueDel(darknet);
        }
    }

    #endregion
    #region Portal

    private void OnPortalAction(Entity<BrighteyeComponent> ent, ref BrighteyePortalActionEvent args)
    {
        if (HasComp<NullSpaceComponent>(ent)) // No making portals while in nullspace!
        {
            args.Handled = true;
            return;
        }

        if (ent.Comp.PortalNeedStation)
        {
            bool onStation = false;
            foreach (var station in _station.GetStations()) // Lets make sure the Portal **IS ON STATION!**
            {
                if (_station.GetLargestGrid(station) is not { } grid)
                    continue;

                if (Transform(ent).GridUid != grid)
                    continue;

                onStation = true;
            }

            if (!onStation)
            {
                args.Handled = true;
                return;
            }
        }

        if (OnAttemptEnergyUse(ent, ent.Comp, ent.Comp.PortalCost))
        {
            SpawnTheDark();

            _actionsSystem.RemoveAction(ent.Owner, ent.Comp.PortalAction);

            EnsureComp<PortalTimeoutComponent>(ent); // Lets not teleport as soon we put down the portal, duh.

            var newPortal = PredictedSpawnAtPosition(ent.Comp.PortalShadekin, Transform(ent).Coordinates);
            if (TryComp<DarkPortalComponent>(newPortal, out var portal))
                portal.Brighteye = ent;

            ent.Comp.Portal = newPortal;

            _alerts.ClearAlert(ent.Owner, ent.Comp.PortalAlert);
        }

        args.Handled = true;
    }

    #endregion
    #region  Phase

    private void OnPhaseAction(Entity<BrighteyeComponent> ent, ref BrighteyePhaseActionEvent args)
    {
        var cost = ent.Comp.PhaseCost;
        if (HasComp<NullSpaceComponent>(ent))
        {
            if (_nullspace.CanPhase(ent) && OnAttemptEnergyUse(ent, ent.Comp))
                _nullspace.Phase(ent);

            args.Handled = true;
            return;
        }

        if (TryComp<ShadekinComponent>(ent, out var shadekin))
        {
            if (shadekin.CurrentState == ShadekinState.Extreme)
                return;

            if (shadekin.CurrentState == ShadekinState.High)
                cost = ent.Comp.MaxEnergy;
            else if (shadekin.CurrentState == ShadekinState.Annoying)
                cost *= 3;
            else if (shadekin.CurrentState == ShadekinState.Low)
                cost *= 2;
        }

        if (TryComp<PullerComponent>(ent, out var puller) && puller.Pulling is not null)
        {
            var doAfter = new PhaseDoAfterEvent
            {
                Cost = cost,
            };

            _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, TimeSpan.FromSeconds(10), doAfter, ent, puller.Pulling)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                BlockDuplicate = true
            });
        }
        else if (_nullspace.CanPhase(ent) && OnAttemptEnergyUse(ent, ent.Comp, cost))
            _nullspace.Phase(ent);

        args.Handled = true;
    }

    private void OnPhaseDoAfter(Entity<BrighteyeComponent> ent, ref PhaseDoAfterEvent args)
    {
        if (!args.Args.Target.HasValue || args.Handled || args.Cancelled)
            return;

        if (!_nullspace.CanPhase(ent) || !OnAttemptEnergyUse(ent, ent.Comp, args.Cost))
            return;

        EnsureComp<NullSpacePulledComponent>(args.Args.Target.Value);
        _nullspace.Phase(ent);

        args.Handled = true;
    }

    #endregion
}
