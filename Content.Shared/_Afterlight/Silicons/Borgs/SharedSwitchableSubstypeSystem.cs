using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Afterlight.Silicons.Borgs;

/// <summary>
/// Shared behaviour for borg switchable subtype logic.
/// Essentially a reimplementation of <see cref="SharedBorgSwitchableTypeSystem"/> specifically for cosmetic functions.
/// </summary>
public abstract partial class SharedBorgSwitchableSubtypeSystem : EntitySystem
{
    [Dependency] private InteractionPopupSystem _interactionPopup = default!;
    [Dependency] private SharedBorgSwitchableTypeSystem _switchableType = default!; // Starlight
    [Dependency] protected IPrototypeManager Prototypes = default!;
    [Dependency] protected IComponentFactory ComponentFactory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BorgSwitchableSubtypeComponent, MapInitEvent>(OnMapInit); // make sure that our subtype is selected first
        SubscribeLocalEvent<BorgSwitchableSubtypeComponent, AfterBorgTypeSelectEvent>(OnBorgTypeSelect);

        Subs.BuiEvents<BorgSwitchableTypeComponent>(BorgSwitchableTypeUiKey.SelectBorgType,
            sub =>
            {
                sub.Event<BorgSelectSubtypeMessage>(SelectSubtypeMessageHandler);
            });

        base.Initialize();
    }

    private void OnMapInit(Entity<BorgSwitchableSubtypeComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.BorgSubtype != null)
        {
            SelectBorgSubtype(ent);
        }
    }

    private void OnBorgTypeSelect(Entity<BorgSwitchableSubtypeComponent> ent, ref AfterBorgTypeSelectEvent args)
    {
        if (!ent.Comp.BorgSubtype.HasValue)
            return;

        Dirty(ent);
        SelectBorgSubtype(ent);
    }

    protected virtual void SelectBorgSubtype(Entity<BorgSwitchableSubtypeComponent> ent)
    {
        UpdateEntityAppearance(ent);
    }

    private void UpdateEntityAppearance(Entity<BorgSwitchableSubtypeComponent> entity)
    {
        if (!Prototypes.TryIndex(entity.Comp.BorgSubtype, out var subtypePrototype))
            return;

        UpdateEntityAppearance(entity, subtypePrototype);
    }

    protected virtual void UpdateEntityAppearance(Entity<BorgSwitchableSubtypeComponent> entity,
        EntityPrototype borgSubtypePrototype)
    {
        if (!borgSubtypePrototype.TryGetComponent<BorgSubtypeDefinitionComponent>(out var borgSubtype, ComponentFactory))
            return;

        if (TryComp(entity, out InteractionPopupComponent? popup))
        {
            _interactionPopup.SetInteractSuccessString((entity.Owner, popup), borgSubtype.PetSuccessString);
            _interactionPopup.SetInteractFailureString((entity.Owner, popup), borgSubtype.PetFailureString);
        }

        if (TryComp(entity, out FootstepModifierComponent? footstepModifier))
        {
            footstepModifier.FootstepSoundCollection = borgSubtype.FootstepCollection;
        }
    }

    #region Starlight
    private void SelectSubtypeMessageHandler(Entity<BorgSwitchableTypeComponent> ent, ref BorgSelectSubtypeMessage args)
    {
        if (args.Subtype is { } requested && !Prototypes.HasIndex(requested))
            return;

        if (!TryComp<BorgSwitchableSubtypeComponent>(ent, out var subtypeComp))
        {
            _switchableType.TrySelectBorgType(ent, args.BorgType);
            return;
        }

        var previous = subtypeComp.BorgSubtype;
        subtypeComp.BorgSubtype = args.Subtype;

        if (!_switchableType.TrySelectBorgType(ent, args.BorgType))
        {
            subtypeComp.BorgSubtype = previous;
            return;
        }

        Dirty(ent.Owner, subtypeComp);
    }
    #endregion
}
