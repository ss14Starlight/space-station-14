using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Chemistry.ExternalContainerInjector;

/// <summary>
/// System for Injectors that use solutions from inserted vials instead of internal solutions.
/// </summary>
public abstract class SharedExternalContainerInjectorSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainers = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExternalContainerInjectorComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ExternalContainerInjectorComponent, GetVerbsEvent<ActivationVerb>>(AddActivationVerb);
    }

    private void AddActivationVerb(Entity<ExternalContainerInjectorComponent> entity,
        ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || entity.Comp.InjectOnly)
            return;

        var (_, component) = entity;
        var user = args.User;
        
        var activationVerb = new ActivationVerb()
        {
            Text = Loc.GetString("hypospray-verb-mode-label"),
            Act = () => { ToggleMode(entity, user); }
        };
        args.Verbs.Add(activationVerb);
    }

    private void ToggleMode(Entity<ExternalContainerInjectorComponent> entity, EntityUid user)
    {
        SetMode(entity, !entity.Comp.OnlyAffectsMobs);

        if (!_timing.IsFirstTimePredicted)
            return;

        string msg = (entity.Comp.OnlyAffectsMobs && entity.Comp.CanContainerDraw)
            ? "hypospray-verb-mode-inject-mobs-only"
            : "hypospray-verb-mode-inject-all";
        _popup.PopupClient(Loc.GetString(msg), entity, user);
    }

    public void SetMode(Entity<ExternalContainerInjectorComponent> entity, bool onlyAffectsMobs)
    {
        if (entity.Comp.OnlyAffectsMobs == onlyAffectsMobs)
            return;

        entity.Comp.OnlyAffectsMobs = onlyAffectsMobs;
        Dirty(entity);
    }

    private void OnUseInHand(Entity<ExternalContainerInjectorComponent> entity, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        ToggleMode(entity, user);
    }

    /// <summary>
    /// Gets the solution from the inserted vial, if any.
    /// </summary>
    public bool TryGetVialSolution(Entity<ExternalContainerInjectorComponent> entity, out Solution? solution,
        out Entity<SolutionComponent> solutionEntity)
    {
        solution = null;
        solutionEntity = default;

        if (!_itemSlots.TryGetSlot(entity.Owner, entity.Comp.VialSlotId, out var slot) || !slot.HasItem ||
            slot.Item == null)
            return false;

        if (!_solutionContainers.TryGetSolution(slot.Item.Value, entity.Comp.VialSolutionName, out var vialSolution,
                out var vialSolutionComponent))
            return false;

        solution = vialSolutionComponent;
        solutionEntity = vialSolution.GetValueOrDefault();
        return true;
    }

    protected bool EligibleEntity(EntityUid entity, IEntityManager entMan, ExternalContainerInjectorComponent component)
    {
        return component.OnlyAffectsMobs
            ? entMan.HasComponent<SolutionContainerManagerComponent>(entity) &&
              entMan.HasComponent<MobStateComponent>(entity)
            : entMan.HasComponent<SolutionContainerManagerComponent>(entity);
    }
}