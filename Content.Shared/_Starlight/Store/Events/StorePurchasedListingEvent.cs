using Content.Shared.Store;

namespace Content.Shared._Starlight.Store.Events;

public record struct StorePurchasedListingEvent(EntityUid Purchaser, ListingData Listing, EntityUid? Item, EntityUid? Action);
