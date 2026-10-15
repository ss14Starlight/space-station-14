using Content.Shared.Armor;
using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Clothing.EntitySystems;

public sealed partial class AllowSuitStorageExamineSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnExamined(Entity<AllowSuitStorageComponent> ent, ref ExaminedEvent args)
    {
        var component = ent.Comp;

        if (!HasComp<ClothingComponent>(ent))
            return;

        if (TryGetSuitSlotExamine(component, out var msg))
        {
            args.PushMarkup(msg.ToMarkup());
        }
    }

    private bool TryGetSuitSlotExamine(AllowSuitStorageComponent component, out FormattedMessage msg)
    {
        msg = new FormattedMessage();
        if (component.Whitelist.Components == null || component.Whitelist.Components.Length == 0)
            return false;
        
        var supportedItems = GetSupportedItemsString(component.Whitelist.Components);
        var st = Loc.GetString("allow-suit-storage-examine-text", ("itemList", supportedItems));
        msg.AddMarkupOrThrow(st);

        return true;
    }

    private string GetSupportedItemsString(string[] whitelistComponentString)
    {
        var allowsGasTank = whitelistComponentString.Contains("GasTank");
        var allowsCoolingUnit = whitelistComponentString.Contains("CoolingUnit");
        var allowsItem = whitelistComponentString.Contains("Item");

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
