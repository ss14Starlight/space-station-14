using Content.Client.Inventory;
using Content.Shared._Moffstation.Strip.Components; // Moffstation
using Content.Shared.Cuffs.Components;
using Content.Shared.Ensnaring.Components;
using Content.Shared.Hands;
using Content.Shared.Interaction.Components; // Moffstation
using Content.Shared.Inventory; // Moffstation
using Content.Shared.Inventory.Events;
using Content.Shared.Strip;
using Content.Shared.Strip.Components;
using Robust.Client.GameObjects; // Moffstation
using Robust.Shared.Map; // Moffstation
using Robust.Shared.Prototypes; // Moffstation

namespace Content.Client.Strip;

/// <summary>
///     This is the client-side stripping system, which just triggers UI updates on events.
/// </summary>
public sealed class StrippableSystem : SharedStrippableSystem
{
    // Moffstation - Begin - Obscuring Virtual Entities are unique per item in the strip UI
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    [ViewVariables]
    private static readonly EntProtoId HiddenSlotEntId = "StrippingHiddenEntity";
    // Moffstation - End

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StrippableComponent, CuffedStateChangeEvent>(OnCuffStateChange);
        SubscribeLocalEvent<StrippableComponent, DidEquipEvent>(UpdateUi);
        SubscribeLocalEvent<StrippableComponent, DidUnequipEvent>(UpdateUi);
        SubscribeLocalEvent<StrippableComponent, DidEquipHandEvent>(UpdateUi);
        SubscribeLocalEvent<StrippableComponent, DidUnequipHandEvent>(UpdateUi);
        SubscribeLocalEvent<StrippableComponent, EnsnaredChangedEvent>(UpdateUi);
    }

    private void OnCuffStateChange(EntityUid uid, StrippableComponent component, ref CuffedStateChangeEvent args)
    {
        UpdateUi(uid, component);
    }

    public void UpdateUi(EntityUid uid, StrippableComponent? component = null, EntityEventArgs? args = null)
    {
        if (!TryComp(uid, out UserInterfaceComponent? uiComp))
            return;

        foreach (var ui in uiComp.ClientOpenInterfaces.Values)
        {
            if (ui is StrippableBoundUserInterface stripUi)
                stripUi.DirtyMenu();
        }
    }

    // Moffstation - Begin - Obscuring Virtual Entities are unique per item in the strip UI
    public EntityUid? GetHidingEntityOrNull(EntityUid entity, SlotDefinition? slotDefinition, EntityUid? viewer)
    {
        // If the slot itself hides its contents, use the generic slot hiding entity.
        if (slotDefinition != null &&
            IsStripHidden(slotDefinition, viewer))
            return Spawn(HiddenSlotEntId, MapCoordinates.Nullspace);

        if (HasComp<BypassInteractionChecksComponent>(viewer) ||
            !TryComp<HideInStripMenuComponent>(entity, out var hideInStripMenu))
            return null;

        switch (hideInStripMenu.Strategy)
        {
            case HideInStripMenuWithDynamicStrategy:
                var ev = new GetHideInStripMenuEntityEvent(MapCoordinates.Nullspace);
                RaiseLocalEvent(entity, ref ev, broadcast: false);
                return ev.Entity;
            case HideInStripMenuWithEntityStrategy entStrat:
                return Spawn(entStrat.Prototype, MapCoordinates.Nullspace);
            case HideInStripMenuWithSyntheticEntityStrategy synthStrat:
                var spawned = Spawn(null, MapCoordinates.Nullspace);

                var sprite = new Entity<SpriteComponent?>(spawned, AddComp<SpriteComponent>(spawned));
                foreach (var layerData in synthStrat.Sprite)
                {
                    _sprite.AddLayer(sprite, layerData, index: null);
                }

                var meta = MetaData(spawned);
                _meta.SetEntityName(spawned, Loc.GetString(synthStrat.Name), meta);
                _meta.SetEntityDescription(spawned, Loc.GetString(synthStrat.Description), meta);

                return spawned;
        }

        return null;
    }
    // Moffstation - End
}
