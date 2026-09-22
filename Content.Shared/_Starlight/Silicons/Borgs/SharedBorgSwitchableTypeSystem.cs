using Content.Shared._Afterlight.Silicons.Borgs;
using Content.Shared._NullLink;
using Content.Shared._Starlight.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Silicons.Borgs;

public abstract partial class SharedBorgSwitchableTypeSystem
{
    [Dependency] private ISharedNullLinkPlayerResourcesManager _playerResources = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;

    /// <summary>
    /// Applies a borg type picked by a player.
    /// </summary>
    public bool TrySelectBorgType(Entity<BorgSwitchableTypeComponent> ent, ProtoId<BorgTypePrototype> borgType)
    {
        if (ent.Comp.SelectedBorgType is { } selected && selected != BorgChassisResetSystem.UnselectedType)
            return false;

        if (!Prototypes.HasIndex(borgType) || borgType == BorgChassisResetSystem.UnselectedType)
            return false;

        if (TryComp<BorgSwitchableSubtypeComponent>(ent, out var subtypeComp) && subtypeComp.BorgSubtype is { } borgSubtype)
        {
            if (!Prototypes.TryIndex(borgSubtype, out var subtypePrototype))
                return false;

            if (subtypePrototype.TryComp<BorgSubtypeDefinitionComponent>(out var subtype, _componentFactory)
                && subtype.Price is not null and > 0)
            {
                if (!_playerResources.TryGetResource(ent.Owner, "credits", out var balance) || balance < subtype.Price)
                    return false;

                _playerResources.TryUpdateResource(ent.Owner, "credits", -subtype.Price.Value);
            }
        }

        SelectBorgModule(ent, borgType);

        return true;
    }
}
