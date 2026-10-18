using Content.Shared.Clothing.Components;
using Content.Shared.Verbs;

namespace Content.Shared.Armor;

public abstract partial class SharedArmorSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnDetailedExamine(EntityUid ent, AllowSuitStorageComponent component, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!HasComp<ClothingComponent>(ent))
            return;

        if (component.Whitelist.Components == null || component.Whitelist.Components.Length == 0)
            return;

        var supportedItems = GetSupportedSuitSlotItemString(component.Whitelist.Components);

        _examine.AddHoverExamineVerb(
            args,
            component,
            Loc.GetString("allow-suit-storage-examine-verb"),
            Loc.GetString("allow-suit-storage-examine-description", ("item", supportedItems)),
            "/Textures/Interface/VerbIcons/buckle.svg.192dpi.png"
        );
    }

    private string GetSupportedSuitSlotItemString(string[] whitelistComponentStrings)
    {
        var allowsGasTank = whitelistComponentStrings.Contains("GasTank");
        var allowsCoolingUnit = whitelistComponentStrings.Contains("CoolingUnit");
        var allowsItem = whitelistComponentStrings.Contains("Item");

        if (allowsItem)
            return Loc.GetString("allow-suit-storage-item-gas-tank-cooling-unit-weapon");
        else if (allowsGasTank && allowsCoolingUnit)
            return Loc.GetString("allow-suit-storage-item-gas-tank-cooling-unit");
        else if (allowsGasTank)
            return Loc.GetString("allow-suit-storage-item-gas-tank");
        else if (allowsCoolingUnit)
            return Loc.GetString("allow-suit-storage-item-cooling-unit");
        else
            return Loc.GetString("allow-suit-storage-item-something");
    }
}
