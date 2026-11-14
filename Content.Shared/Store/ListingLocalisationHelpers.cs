using Content.Shared._Starlight.Store.Conditions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Store;

public static class ListingLocalisationHelpers
{
    /// <summary>
    /// ListingData's Name field can be either a localisation string or the actual entity's name.
    /// This function gets a localised name from the localisation string if it exists, and if not, it gets the entity's name.
    /// If neither a localised string exists, or an associated entity name, it will return the value of the "Name" field.
    /// Stock-limited listings keep that loc id and append the count with <c>store-listing-name-stock</c>.
    /// </summary>
    public static string GetLocalisedNameOrEntityName(ListingData listingData, IPrototypeManager prototypeManager)
    {
        var name = string.Empty;

        if (listingData.Name != null)
            name = Loc.GetString(listingData.Name);
        else if (listingData.ProductEntity != null)
            name = prototypeManager.Index(listingData.ProductEntity.Value).Name;

        if (!TryResolveStock(listingData, out var current, out var max, out var outOfStock, out _))
            return name;

        if (outOfStock)
            return Loc.GetString("store-listing-out-of-stock", ("name", name));

        return Loc.GetString("store-listing-name-stock", ("name", name), ("current", current), ("max", max));
    }

    /// <summary>
    /// ListingData's Description field can be either a localisation string or the actual entity's description.
    /// This function gets a localised description from the localisation string if it exists, and if not, it gets the entity's description.
    /// If neither a localised string exists, or an associated entity description, it will return the value of the "Description" field.
    /// </summary>
    public static string GetLocalisedDescriptionOrEntityDescription(ListingData listingData, IPrototypeManager prototypeManager)
    {
        var desc = string.Empty;

        if (listingData.Description != null)
            desc = Loc.GetString(listingData.Description);
        else if (listingData.ProductEntity != null)
            desc = prototypeManager.Index(listingData.ProductEntity.Value).Description;

        if (!TryResolveStock(listingData, out _, out _, out _, out var lastPurchaser) || string.IsNullOrEmpty(lastPurchaser))
            return desc;

        return Loc.GetString("store-listing-last-purchased", ("desc", desc), ("name", lastPurchaser));
    }

    /// <summary>
    /// Prefers the server stock tables. The client uses the stock fields copied onto the condition before the listing was sent.
    /// </summary>
    private static bool TryResolveStock(
        ListingData listingData,
        out int current,
        out int max,
        out bool outOfStock,
        out string? lastPurchaser)
    {
        if (StockLimitedListingCondition.TryGetTrackedStock(listingData.ID, out current, out max, out outOfStock, out lastPurchaser))
            return true;

        if (listingData.Conditions == null)
            return false;

        foreach (var condition in listingData.Conditions)
        {
            if (condition is not StockLimitedListingCondition stockCondition)
                continue;

            current = stockCondition.CurrentStock;
            max = stockCondition.StockLimit;
            outOfStock = current <= 0;
            lastPurchaser = stockCondition.LastPurchaser;
            return true;
        }

        return false;
    }
}
