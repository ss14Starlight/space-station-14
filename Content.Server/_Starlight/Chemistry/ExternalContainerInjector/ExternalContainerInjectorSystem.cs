using Content.Shared._Starlight.Chemistry.ExternalContainerInjector;
using Robust.Server.Audio;
using Content.Server.Body.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
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
        // Check if we can draw blood from the target (only if vial is empty and has space)
        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            if (TryGetVialSolution(entity, out var vialSolution, out var vialSolutionEntity))
            {
                // Only allow blood drawing if the vial is empty and has space
                if (vialSolution != null && vialSolution.AvailableVolume > 0)
                {
                    return TryDrawBlood(entity, (target, bloodstream), user);
                }
            }
        }

        // if target is a container, try to draw from the container if allowed
        if (entity.Comp.CanContainerDraw
            && !EligibleEntity(target, EntityManager, entity)
            && _solutionContainers.TryGetDrawableSolution(target, out var drawableSolution, out _))
        {
            return TryDraw(entity, target, drawableSolution, user);
        }

        return TryDoInject(entity, target, user);
    }


    /// <summary>
    /// Draws blood from a target's bloodstream into the vial.
    /// </summary>
    private bool TryDrawBlood(Entity<ExternalContainerInjectorComponent> entity, Entity<BloodstreamComponent> target,
        EntityUid user)
    {
        // Get solution from inserted vial
        if (!TryGetVialSolution(entity, out var vialSolution, out var vialSolutionEntity) || vialSolution == null)
            return false;

        var realTransferAmount = FixedPoint2.Min(entity.Comp.TransferAmount, vialSolution.AvailableVolume);

        // Draw blood from the bloodstream
        if (!_solutionContainers.ResolveSolution(target.Owner, target.Comp.BloodSolutionName,
                ref target.Comp.BloodSolution))
        {
            _popup.PopupEntity(
                Loc.GetString("injector-component-cannot-draw-message",
                    ("target", Identity.Entity(target, EntityManager))), entity.Owner, user);
            return false;
        }

        // Split blood from the bloodstream
        var removedSolution = _solutionContainers.SplitSolution(target.Comp.BloodSolution.Value, realTransferAmount);
        if (!_solutionContainers.TryAddSolution(vialSolutionEntity, removedSolution))
            return false;

        _popup.PopupEntity(
            Loc.GetString("injector-component-draw-success-message", ("amount", removedSolution.Volume),
                ("target", Identity.Entity(target, EntityManager))), entity.Owner, user);

        // Handle DNA transfer
        var ev = new TransferDnaEvent { Donor = target, Recipient = entity.Owner };
        RaiseLocalEvent(target, ref ev);

        return true;
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

    private bool TryDraw(Entity<ExternalContainerInjectorComponent> entity, EntityUid target,
        Entity<SolutionComponent>? targetSolution, EntityUid user)
    {
        if (targetSolution == null || targetSolution.Value.Comp == null)
            return false;
        var targetEntity = targetSolution.Value;
        var targetComp = targetEntity.Comp;
        // Get solution from inserted vial
        if (!TryGetVialSolution(entity, out var vialSolution, out var vialSolutionEntity) || vialSolution == null)
            return false;

        // Check that the vial has available volume
        if (vialSolution.AvailableVolume <= 0)
        {
            _popup.PopupEntity(
                Loc.GetString("injector-component-cannot-draw-message",
                    ("target", Identity.Entity(target, EntityManager))), entity.Owner, user);
            return false;
        }

        // Calculate transfer amount
        var realTransferAmount = FixedPoint2.Min(entity.Comp.TransferAmount, targetComp.Solution.Volume,
            vialSolution.AvailableVolume);
        if (realTransferAmount <= 0)
        {
            _popup.PopupEntity(
                Loc.GetString("injector-component-target-is-empty-message",
                    ("target", Identity.Entity(target, EntityManager))), entity.Owner, user);
            return false;
        }

        // Get DrawableSolutionComponent
        if (!TryComp<DrawableSolutionComponent>(target, out var drawableComp) || drawableComp == null)
            return false;

        // Draw from the target solution into the vial
        var drawableEntity = new Entity<DrawableSolutionComponent?>(target, drawableComp);
        var removedSolution = _solutionContainers.Draw(drawableEntity, targetEntity, realTransferAmount);
        if (!_solutionContainers.TryAddSolution(vialSolutionEntity, removedSolution))
            return false;

        _popup.PopupEntity(
            Loc.GetString("injector-component-draw-success-message", ("amount", removedSolution.Volume),
                ("target", Identity.Entity(target, EntityManager))), entity.Owner, user);

        // Optionally: log the action (like the regular injector)
        _adminLogger.Add(LogType.ForceFeed,
            $"{EntityManager.ToPrettyString(user):user} drew {removedSolution.Volume}u from {EntityManager.ToPrettyString(target):target} into vial {EntityManager.ToPrettyString(vialSolutionEntity):vial}");

        // Optionally: handle DNA transfer, etc.
        var ev = new TransferDnaEvent { Donor = target, Recipient = entity.Owner };
        RaiseLocalEvent(target, ref ev);

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