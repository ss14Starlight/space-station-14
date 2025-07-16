using Content.Shared._Starlight.Chemistry.ExternalContainerInjector;
using Robust.Server.Audio;
using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Hypospray.Events;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Forensics;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Chemistry.ExternalContainerInjector;

/// <summary>
/// Server-side implementation of the external container injector system.
/// </summary>
public sealed class ExternalContainerInjectorSystem : SharedExternalContainerInjectorSystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly ReactiveSystem _reactiveSystem = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainers = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExternalContainerInjectorComponent, AfterInteractEvent>(OnAfterInteract);
    }

    public void OnAfterInteract(Entity<ExternalContainerInjectorComponent> entity, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        args.Handled = TryUseInjector(entity, args.Target.Value, args.User);
    }

    private bool TryUseInjector(Entity<ExternalContainerInjectorComponent> entity, EntityUid target,
        EntityUid user)
    {
        return TryDoInject(entity, target, user);
    }

    /// <summary>
    /// Injects a solution into a target.
    /// </summary>
    public bool TryDoInject(Entity<ExternalContainerInjectorComponent> entity, EntityUid target, EntityUid user)
    {
        var (uid, component) = entity;

        if (!EligibleEntity(target, EntityManager, component))
            return false;

        if (TryComp<UseDelayComponent>(uid, out var delayComp))
        {
            if (_useDelay.IsDelayed((uid, delayComp)))
                return false;
        }

        string? msgFormat = null;

        // Self event
        var selfEvent = new SelfBeforeHyposprayInjectsEvent(user, entity.Owner, target);
        RaiseLocalEvent(user, selfEvent);

        if (selfEvent.Cancelled)
        {
            _popup.PopupEntity(
                Loc.GetString(selfEvent.InjectMessageOverride ?? "hypospray-cant-inject",
                    ("owner", Identity.Entity(target, EntityManager))), target, user);
            return false;
        }

        target = selfEvent.TargetGettingInjected;

        if (!EligibleEntity(target, EntityManager, component))
            return false;

        // Target event
        var targetEvent = new TargetBeforeHyposprayInjectsEvent(user, entity.Owner, target);
        RaiseLocalEvent(target, targetEvent);

        if (targetEvent.Cancelled)
        {
            _popup.PopupEntity(
                Loc.GetString(targetEvent.InjectMessageOverride ?? "hypospray-cant-inject",
                    ("owner", Identity.Entity(target, EntityManager))), target, user);
            return false;
        }

        target = targetEvent.TargetGettingInjected;

        if (!EligibleEntity(target, EntityManager, component))
            return false;

        // The target event gets priority for the overriden message.
        if (targetEvent.InjectMessageOverride != null)
            msgFormat = targetEvent.InjectMessageOverride;
        else if (selfEvent.InjectMessageOverride != null)
            msgFormat = selfEvent.InjectMessageOverride;
        else if (target == user)
            msgFormat = "hypospray-component-inject-self-message";

        // Get solution from inserted vial
        if (!TryGetVialSolution(entity, out var vialSolution, out var vialSolutionEntity))
        {
            _popup.PopupEntity(Loc.GetString("hypospray-component-empty-message"), target, user);
            return true;
        }

        if (!_solutionContainers.TryGetInjectableSolution(target, out var targetSoln, out var targetSolution) ||
            targetSolution == null)
        {
            _popup.PopupEntity(
                Loc.GetString("hypospray-cant-inject", ("target", Identity.Entity(target, EntityManager))), target,
                user);
            return false;
        }

        var realTransferAmount = FixedPoint2.Min(component.TransferAmount, targetSolution.AvailableVolume);

        if (realTransferAmount <= 0)
        {
            _popup.PopupEntity(Loc.GetString("hypospray-component-transfer-already-full-message", ("owner", target)),
                target, user);
            return true;
        }

        // Move units from vial solution to target solution
        var removedSolution = _solutionContainers.SplitSolution(vialSolutionEntity, realTransferAmount);

        if (!targetSolution.CanAddSolution(removedSolution))
            return true;
        _reactiveSystem.DoEntityReaction(target, removedSolution, ReactionMethod.Injection);
        _solutionContainers.TryAddSolution(targetSoln.Value, removedSolution);

        // Play injection sound
        PlayInjectSound(entity, user);

        // Show injection feedback
        if (target != user)
        {
            _popup.PopupEntity(
                Loc.GetString(msgFormat ?? "hypospray-component-inject-other-message", ("other", target)), target,
                user);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("hypospray-component-feel-prick-message"), target, target);
        }

        var ev = new TransferDnaEvent { Donor = target, Recipient = uid };
        RaiseLocalEvent(target, ref ev);

        // same LogType as syringes...
        _adminLogger.Add(LogType.ForceFeed,
            $"{EntityManager.ToPrettyString(user):user} injected {EntityManager.ToPrettyString(target):target} with a solution {SharedSolutionContainerSystem.ToPrettyString(removedSolution):removedSolution} using a {EntityManager.ToPrettyString(uid):using}");

        return true;
    }

    private void PlayInjectSound(Entity<ExternalContainerInjectorComponent> entity, EntityUid user)
    {
        // This is a hack to prevent the sound from playing multiple times.
        if (!_timing.IsFirstTimePredicted)
            return;

        _audio.PlayPvs(entity.Comp.InjectSound, user);
    }
}