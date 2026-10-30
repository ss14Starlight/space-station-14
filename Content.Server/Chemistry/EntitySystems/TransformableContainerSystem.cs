using Content.Server.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.NameModifier.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Server.Chemistry.EntitySystems;

public sealed partial class TransformableContainerSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionsSystem = default!;
    [Dependency] private MetaDataSystem _metadataSystem = default!;
    [Dependency] private NameModifierSystem _nameMod = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransformableContainerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TransformableContainerComponent, SolutionChangedEvent>(OnSolutionChange);
        SubscribeLocalEvent<TransformableContainerComponent, RefreshNameModifiersEvent>(OnRefreshNameModifiers);
    }

    private void OnMapInit(Entity<TransformableContainerComponent> entity, ref MapInitEvent args)
    {
        var meta = MetaData(entity.Owner);
        if (string.IsNullOrEmpty(entity.Comp.InitialDescription))
        {
            entity.Comp.InitialDescription = meta.EntityDescription;
        }
    }

    private void OnSolutionChange(Entity<TransformableContainerComponent> entity, ref SolutionChangedEvent args)
    {
        if (!_solutionsSystem.TryGetFitsInDispenser(entity.Owner, out _, out var solution))
            return;

        //Transform container into initial state when emptied
        if (entity.Comp.CurrentReagent != null && solution.Contents.Count == 0)
        {
            CancelTransformation(entity);
        }

        //the biggest reagent in the solution decides the appearance
        var reagentId = solution.GetPrimaryReagentId();

        //If biggest reagent didn't change - don't change anything at all
        if (entity.Comp.CurrentReagent != null && entity.Comp.CurrentReagent == reagentId?.Prototype)
        {
            return;
        }

        //Only reagents with spritePath property can change appearance of transformable containers!
        if (!string.IsNullOrWhiteSpace(reagentId?.Prototype)
            && _prototypeManager.TryIndex(reagentId.Value.Prototype, out ReagentPrototype? proto))
        {
            entity.Comp.CurrentReagent = proto;
            entity.Comp.Transformed = true;
            // Starlight-edit: replaces the reagent description update so chameleon glass can display its disguise.
            RefreshDisplayedDescription(entity.Owner);
        }

        _nameMod.RefreshNameModifiers(entity.Owner);
    }

    private void OnRefreshNameModifiers(Entity<TransformableContainerComponent> entity, ref RefreshNameModifiersEvent args)
    {
        if (GetDisplayedReagent(entity) is { } currentReagent) // Starlight-edit: honor chameleon glass disguise
        {
            args.AddModifier("transformable-container-component-glass", priority: -1, ("reagent", currentReagent.LocalizedName));
        }
    }

    private void CancelTransformation(Entity<TransformableContainerComponent> entity)
    {
        entity.Comp.CurrentReagent = null;
        entity.Comp.Transformed = false;

        _nameMod.RefreshNameModifiers(entity.Owner);
        // Starlight-edit: replaces the initial description reset so chameleon glass keeps its disguise when emptied.
        RefreshDisplayedDescription(entity.Owner);
    }
}
