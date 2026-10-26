using Content.Shared.Revolutionary.Components;
using Content.Server.Store.Systems;
using Content.Shared.Implants;
using Content.Shared.Store.Components;
using Content.Shared.FixedPoint;
using Content.Server.Popups;
using Content.Shared.Popups;
using Content.Shared.IdentityManagement;
using Content.Server.RoundEnd;
using Content.Shared._Starlight.Implants.Components;
using Content.Server.GameTicking.Rules;
using Content.Shared._Starlight.Revolutionary.Components;
using Content.Shared._Starlight.Store.Conditions;
using Content.Server._Starlight.Revolutionary.Components;
using Content.Shared.Store;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Implants;
public sealed partial class USSPUplinkSystem : EntitySystem
{
    [Dependency] private StoreSystem _storeSystem = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private RevolutionaryRuleSystem _rev = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StoreBuyFinishedEvent>(OnStoreBuyFinished);
        SubscribeLocalEvent<USSPUplinkImplantComponent, ImplantImplantedEvent>(OnImplantImplanted);
        SubscribeLocalEvent<RemoteStoreComponent, AfterActivatableUIOpenEvent>(OnRemoteUiOpen);
        SubscribeLocalEvent<RoundEndSystemChangedEvent>(OnRoundEnd);
    }

    /// <summary>
    /// Handles the event when a round ends.
    /// Resets all stock-limited listings in the uplink catalog.
    /// </summary>
    private void OnRoundEnd(RoundEndSystemChangedEvent ev)
        // Reset all stock-limited listings in the uplink catalog
        => ResetUplinkStocks();

    /// <summary>
    /// Resets all stock-limited listings in the uplink catalog.
    /// This is called when a round ends to ensure each new round starts with fresh stock counts.
    /// </summary>
    private void ResetUplinkStocks()
    {
        // Use reflection to access the private static dictionaries in StockLimitedListingCondition
        var type = typeof(StockLimitedListingCondition);

        // Get the _stockCounts dictionary
        var stockCountsField = type.GetField("_stockCounts",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Get the _stockLimits dictionary
        var stockLimitsField = type.GetField("_stockLimits",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Get the _lastPurchasers dictionary
        var lastPurchasersField = type.GetField("_lastPurchasers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Get the _outOfStock dictionary
        var outOfStockField = type.GetField("_outOfStock",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Get the _modifiedListings dictionary
        var modifiedListingsField = type.GetField("_modifiedListings",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Reset the dictionaries
        if (stockCountsField?.GetValue(null) is Dictionary<string, int> stockCounts)
            stockCounts.Clear();

        if (stockLimitsField?.GetValue(null) is Dictionary<string, int> stockLimits)
            stockLimits.Clear();

        if (lastPurchasersField?.GetValue(null) is Dictionary<string, string> lastPurchasers)
            lastPurchasers.Clear();

        if (outOfStockField?.GetValue(null) is Dictionary<string, bool> outOfStock)
            outOfStock.Clear();

        if (modifiedListingsField?.GetValue(null) is Dictionary<string, bool> modifiedListings)
            modifiedListings.Clear();

        // Update all uplink UIs to show the reset stock counts
        _storeSystem.UpdateAllUSSPUplinkUIs();
    }

    /// <summary>
    /// Points an implanted uplink at the head revolutionary's nullspace store.
    /// A head uses their own store. A converted revolutionary uses the head who converted them.
    /// </summary>
    private void OnImplantImplanted(EntityUid uid, USSPUplinkImplantComponent component, ref ImplantImplantedEvent args)
    {
        EntityUid? head = null;
        if (HasComp<HeadRevolutionaryComponent>(args.Implanted))
            head = args.Implanted;
        else if (TryComp<RevolutionaryConverterComponent>(args.Implanted, out var converter))
            head = converter.ConverterUid;

        if (head == null || !HasComp<HeadRevolutionaryComponent>(head.Value))
            return;

        var store = _rev.EnsureHeadStore(head.Value);
        if (TryComp<RemoteStoreComponent>(uid, out var remote))
            _storeSystem.SetRemoteStore(uid, store);

        // Set this head revolutionary as the owner
        var ownerComp = EnsureComp<USSPUplinkOwnerComponent>(uid);
        ownerComp.OwnerUid = head;

        if (HasComp<HeadRevolutionaryComponent>(args.Implanted))
        {
            // Update the head revolutionary's implant component to point to this implant
            var implantComponent = EnsureComp<HeadRevolutionaryImplantComponent>(args.Implanted);
            implantComponent.ImplantUid = uid;
        }

        if (!TryComp<StoreComponent>(store, out var storeComp))
            return;

        var telebonds = storeComp.Balance.GetValueOrDefault("Telebond", FixedPoint2.Zero);
        var conversions = storeComp.Balance.GetValueOrDefault("Conversion", FixedPoint2.Zero);
        _popup.PopupEntity(Loc.GetString("ussp-uplink-implanted", ("telebonds", telebonds), ("converted", 0), ("conversions", conversions)),
            args.Implanted, args.Implanted, PopupType.Medium);
    }

    /// <summary>
    /// Radios open through ActivatableUI. Bind an unbound radio to the user's head store, then push that store's state.
    /// </summary>
    private void OnRemoteUiOpen(Entity<RemoteStoreComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        if (!TryComp<ActivatableUIComponent>(ent, out var activatable) || activatable.Key is not { } key || !key.Equals(StoreUiKey.Key))
            return;

        if (ent.Comp.Store == null)
            TryBindRevolutionaryStore(ent, args.User);

        if (_storeSystem.GetRemoteStore(ent.AsNullable()) is not { } store)
            return;

        _storeSystem.UpdateUserInterface(args.User, store.Owner, store.Comp);
    }

    private void TryBindRevolutionaryStore(Entity<RemoteStoreComponent> ent, EntityUid user)
    {
        EntityUid? head = null;
        if (HasComp<HeadRevolutionaryComponent>(user))
            head = user;
        else if (TryComp<RevolutionaryConverterComponent>(user, out var converter))
            head = converter.ConverterUid;

        if (head == null || !HasComp<HeadRevolutionaryComponent>(head.Value))
            return;

        _storeSystem.SetRemoteStore(ent.AsNullable(), _rev.EnsureHeadStore(head.Value));
    }

    /// <summary>
    /// Handles the event when a purchase is made from a store.
    /// Restores any spent Conversion currency since it's a global headrev progression score, not a spendable currency.
    /// Also updates all uplinks when a stock-limited item is purchased.
    /// </summary>
    private void OnStoreBuyFinished(ref StoreBuyFinishedEvent args)
    {
        // Get the store component
        if (!TryComp<StoreComponent>(args.StoreUid, out var store))
            return;

        // Check if this store uses Conversion currency
        if (store.CurrencyWhitelist.Contains("Conversion"))
        {
            // Check if Conversion was spent in this purchase
            foreach (var (currency, amount) in args.PurchasedItem.Cost)
            {
                if (currency != "Conversion" || amount <= FixedPoint2.Zero)
                    continue;

                // Add the Conversion currency back
                var currencyToAdd = new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> { { "Conversion", amount } };
                _storeSystem.TryAddCurrency(currencyToAdd, args.StoreUid);
                break;
            }
        }

        // Check if this is a stock-limited listing
        if (args.PurchasedItem.Conditions == null || !store.CurrencyWhitelist.Contains("Telebond"))
            return;

        foreach (var condition in args.PurchasedItem.Conditions)
        {
            if (condition is not StockLimitedListingCondition)
                continue;

            // If this is a stock-limited listing, update all uplinks
            _storeSystem.UpdateAllUSSPUplinkUIs();
            break;
        }
    }
}
