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
using Content.Shared.Chemistry.Components;
using Robust.Shared.Prototypes;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;

namespace Content.Server._Starlight.Chemistry.ExternalContainerInjector;

/// <summary>
/// Server-side implementation of the external container injector system.
/// </summary>
public sealed partial class ExternalContainerInjectorSystem : SharedExternalContainerInjectorSystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly ReactiveSystem _reactiveSystem = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainers = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExternalContainerInjectorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SolutionChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<ExternalContainerInjectorComponent, EntInsertedIntoContainerMessage>(OnVialInserted);
        SubscribeLocalEvent<ExternalContainerInjectorComponent, EntRemovedFromContainerMessage>(OnVialRemoved);
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

        bool hasUseDelay = TryComp<UseDelayComponent>(uid, out var delayComp);
        if (hasUseDelay)
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

        // Update hypospray appearance to reflect the reduced solution
        UpdateHyposprayAppearance(entity);

        // Play injection sound
        PlayInjectSound(entity, user);

        // Add Cooldown
        if (hasUseDelay)
            _useDelay.TryResetDelay((uid, delayComp!));

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

    private void OnSolutionChanged(ref SolutionChangedEvent args)
    {
        var query = EntityQueryEnumerator<ExternalContainerInjectorComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (TryGetVialSolution((uid, component), out var solution, out var solutionEntity) &&
                solutionEntity.Owner == args.Solution.Owner)
            {
                UpdateHyposprayAppearance((uid, component));
            }
        }
    }

    private void UpdateHyposprayAppearance(Entity<ExternalContainerInjectorComponent> entity)
    {
        if (!TryComp<AppearanceComponent>(entity, out var appearance))
            return;

        if (!TryGetVialSolution(entity, out var solution, out _))
        {
            // No solution - show empty
            _appearance.SetData(entity, SolutionContainerVisuals.FillFraction, 0f, appearance);
            _appearance.SetData(entity, SolutionContainerVisuals.Color, Color.White, appearance);
            _appearance.SetData(entity, SolutionContainerVisuals.SolutionName, entity.Comp.VialSolutionName,
                appearance);
            return;
        }

        // Update with solution data
        _appearance.SetData(entity, SolutionContainerVisuals.FillFraction, solution?.FillFraction ?? 0, appearance);
        _appearance.SetData(entity, SolutionContainerVisuals.Color,
            solution?.GetColor(_prototypeManager) ?? Color.Transparent, appearance);
        _appearance.SetData(entity, SolutionContainerVisuals.SolutionName, entity.Comp.VialSolutionName, appearance);

        if (solution?.GetPrimaryReagentId() is { } reagent)
            _appearance.SetData(entity, SolutionContainerVisuals.BaseOverride, reagent.ToString(), appearance);
    }

    private bool TryGetVialSolution(Entity<ExternalContainerInjectorComponent> entity, out Solution? solution,
        out Entity<Content.Shared.Chemistry.Components.SolutionComponent> solutionEntity)
    {
        solution = null;
        solutionEntity = default;

        if (!_itemSlots.TryGetSlot(entity.Owner, entity.Comp.VialSlotId, out var slot) || !slot.HasItem ||
            slot.Item == null)
            return false;

        if (!_solutionContainers.TryGetSolution(slot.Item.Value, entity.Comp.VialSolutionName, out var vialSolution,
                out var vialSolutionComponent))
            return false;

        // Check if empty
        if (vialSolutionComponent.Volume == 0)
            return false;

        solution = vialSolutionComponent;
        solutionEntity = vialSolution.GetValueOrDefault();
        return true;
    }

    private void OnVialInserted(Entity<ExternalContainerInjectorComponent> entity, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == entity.Comp.VialSlotId)
        {
            UpdateHyposprayAppearance(entity);
        }
    }

    private void OnVialRemoved(Entity<ExternalContainerInjectorComponent> entity, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == entity.Comp.VialSlotId)
        {
            UpdateHyposprayAppearance(entity);
        }
    }
}