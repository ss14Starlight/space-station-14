using System.Linq;
using Content.Server.Actions;
using Content.Server.Administration.Logs;
using Content.Server.Stack;
using Content.Server.Store.Components;
using Content.Shared.Actions;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Implants.Components;
using Content.Shared.Mind;
using Content.Shared.Mindshield.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.PDA.Ringer;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Server._Starlight.Language;
using Content.Server._Starlight.Statistics;
using Content.Shared._Starlight.Store.Events;
using Content.Shared._Starlight.Store.Conditions;
using Content.Server._Starlight.Revolutionary;
using Content.Server.GameTicking.Rules;


namespace Content.Server.Store.Systems;

public sealed partial class StoreSystem
{
    [Dependency] private IAdminLogManager _admin = default!;
    [Dependency] private RoundStatisticsSystem _roundStatistics = default!; // Starlight
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private ActionUpgradeSystem _actionUpgrade = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private RevSupplyRiftSystem _revSupplyRift = default!; // Starlight
    [Dependency] private LanguageSystem _languageSystem = default!; //Starlight
    [Dependency] private NpcFactionSystem _npcFaction = default!;

    private void InitializeUi()
    {
        SubscribeLocalEvent<StoreComponent, StoreRequestUpdateInterfaceMessage>(OnRequestUpdate);
        SubscribeLocalEvent<StoreComponent, StoreBuyListingMessage>(OnBuyRequest);
        SubscribeLocalEvent<StoreComponent, StoreRequestWithdrawMessage>(OnRequestWithdraw);
        SubscribeLocalEvent<StoreComponent, StoreRequestRefundMessage>(OnRequestRefund);
        SubscribeLocalEvent<StoreComponent, RefundEntityDeletedEvent>(OnRefundEntityDeleted);
        SubscribeLocalEvent<RemoteStoreComponent, StoreRequestUpdateInterfaceMessage>((e, c, ev) =>
            RemoteStoreRelay((e, c), ev));
        SubscribeLocalEvent<RemoteStoreComponent, StoreBuyListingMessage>((e, c, ev) =>
            RemoteStoreRelay((e, c), ev));
        SubscribeLocalEvent<RemoteStoreComponent, StoreRequestWithdrawMessage>((e, c, ev) =>
            RemoteStoreRelay((e, c), ev));
        SubscribeLocalEvent<RemoteStoreComponent, StoreRequestRefundMessage>((e, c, ev) =>
            RemoteStoreRelay((e, c), ev));
        SubscribeLocalEvent<RemoteStoreComponent, RefundEntityDeletedEvent>((e, c, ev) =>
            RemoteStoreRelay((e, c), ev));
    }

    private void RemoteStoreRelay(Entity<RemoteStoreComponent> entity, object ev)
    {
        if (entity.Comp.Store == null || !TryComp<StoreComponent>(entity.Comp.Store, out _))
            return;

        RaiseLocalEvent(entity.Comp.Store.Value, ev);
    }

    private void OnRefundEntityDeleted(Entity<StoreComponent> ent, ref RefundEntityDeletedEvent args)
    {
        ent.Comp.BoughtEntities.Remove(args.Uid);
    }

    /// <summary>
    /// Toggles the store Ui open and closed
    /// </summary>
    /// <param name="user">the person doing the toggling</param>
    /// <param name="storeEnt">the store being toggled</param>
    /// <param name="component"></param>
    public override void ToggleUi(EntityUid user, EntityUid storeEnt, StoreComponent? component = null, EntityUid? remoteAccess = null, RemoteStoreComponent? remoteComponent = null)
    {
        if (!Resolve(storeEnt, ref component))
            return;

        // Starlight: reject a remote that is missing or pointed at a different store.
        if (remoteAccess != null && (!Resolve(remoteAccess.Value, ref remoteComponent, false) || remoteComponent.Store != storeEnt))
            return;

        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        var uiEntity = remoteAccess ?? storeEnt;
        if (!_ui.TryToggleUi(uiEntity, StoreUiKey.Key, actor.PlayerSession))
            return;

        UpdateUserInterface(user, storeEnt, component);
    }

    /// <summary>
    /// STARLIGHT: Updates the user interface for a store and refreshes the listings
    /// </summary>
    /// <param name="user">The person who if opening the store ui. Listings are filtered based on this.</param>
    /// <param name="store">The store entity itself</param>
    /// <param name="component">The store component being refreshed.</param>
    public override void UpdateUserInterface(EntityUid? user, EntityUid store, StoreComponent? component = null)
    {
        if (!Resolve(store, ref component))
            return;

        // STARLIGHT: Check if a rift has been destroyed and update the listing accordingly
        // This ensures the rift listing remains unavailable even when the UI is refreshed
        _revSupplyRift.CheckRiftDestroyedAndUpdateListing((store, component));

        //this is the person who will be passed into logic for all listing filtering.
        if (user != null) //if we have no "buyer" for this update, then don't update the listings
        {
            component.LastAvailableListings = GetAvailableListings(component.AccountOwner ?? user.Value, store, component)
                .ToHashSet();
        }

        //dictionary for all currencies, including 0 values for currencies on the whitelist
        Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> allCurrency = new();
        foreach (var supported in component.CurrencyWhitelist)
        {
            allCurrency.Add(supported, FixedPoint2.Zero);

            if (component.Balance.TryGetValue(supported, out var value))
                allCurrency[supported] = value;
        }

        // TODO: if multiple users are supposed to be able to interact with a single BUI & see different
        // stores/listings, this needs to use session specific BUI states.

        // only tell operatives to lock their uplink if it can be locked
        var showFooter = HasComp<RingerUplinkComponent>(store) || RemoteHasRinger(store);

        var state = new StoreUpdateState(component.LastAvailableListings, allCurrency, showFooter, component.RefundAllowed, component.Grid); // Starlight
        _ui.SetUiState(store, StoreUiKey.Key, state);
        UpdateRemoteStores(store, state);
    }

    /// <summary>
    /// Pushes a store state to remotes that currently have the store UI open.
    /// </summary>
    public override void UpdateRemoteStores(EntityUid store, StoreUpdateState state)
    {
        var query = EntityQueryEnumerator<RemoteStoreComponent, UserInterfaceComponent>();
        while (query.MoveNext(out var uid, out var remote, out var ui))
        {
            if (remote.Store != store)
                continue;

            // Starlight: closed remotes do not need a state push.
            if (!_ui.IsUiOpen((uid, ui), StoreUiKey.Key))
                continue;

            _ui.SetUiState((uid, ui), StoreUiKey.Key, state);
        }
    }

    private bool RemoteHasRinger(EntityUid store)
    {
        var query = EntityQueryEnumerator<RemoteStoreComponent, RingerUplinkComponent>();
        while (query.MoveNext(out _, out var remote, out _))
        {
            if (remote.Store == store)
                return true;
        }

        return false;
    }

    private void OnRequestUpdate(EntityUid uid, StoreComponent component, StoreRequestUpdateInterfaceMessage args)
    {
        UpdateUserInterface(args.Actor, uid, component);
    }

    private void BeforeActivatableUiOpen(EntityUid uid, StoreComponent component, BeforeActivatableUIOpenEvent args)
    {
        UpdateUserInterface(args.User, uid, component);
    }

    /// <summary>
    /// Handles whenever a purchase was made.
    /// </summary>
    private void OnBuyRequest(EntityUid uid, StoreComponent component, StoreBuyListingMessage msg)
    {
        var listing = component.FullListingsCatalog.FirstOrDefault(x => x.ID.Equals(msg.Listing.Id));

        if (listing == null) //make sure this listing actually exists
        {
            Log.Debug("listing does not exist");
            return;
        }

        var buyer = msg.Actor;

        // STARLIGHT: Raise an event to allow other systems to potentially cancel this purchase
        var purchaseAttemptEvent = new StorePurchaseAttemptEvent(listing.ID, uid, buyer);
        RaiseLocalEvent(ref purchaseAttemptEvent);

        // STARLIGHT: If the event handler requested cancellation, cancel the purchase
        if (purchaseAttemptEvent.Cancel)
            return;

        //verify that we can actually buy this listing and it wasn't added
        if (!ListingHasCategory(listing, component.Categories))
            return;

        //condition checking because why not
        if (listing.Conditions != null)
        {
            var args = new ListingConditionArgs(component.AccountOwner ?? GetBuyerMind(buyer), uid, listing, EntityManager);
            var conditionsMet = listing.Conditions.All(condition => condition.Condition(args));

            if (!conditionsMet)
                return;
        }

        // Starlight: Check if the listing is unavailable (e.g., out of stock)
        // We still want to show the listing in the UI, but prevent purchase
        if (listing.Unavailable)
            return;

        //check that we have enough money
        var cost = listing.Cost;
        foreach (var (currency, amount) in cost)
        {
            if (!component.Balance.TryGetValue(currency, out var balance) || balance < amount)
            {
                return;
            }
        }

        if (!IsOnStartingMap(uid, component))
            DisableRefund(uid, component);

        //subtract the cash
        foreach (var (currency, amount) in cost)
        {
            component.Balance[currency] -= amount;

            component.BalanceSpent.TryAdd(currency, FixedPoint2.Zero);

            component.BalanceSpent[currency] += amount;
        }

        //spawn entity
        if (listing.ProductEntity != null)
        {
            var product = Spawn(listing.ProductEntity, Transform(buyer).Coordinates);
            LinkPurchasedRemotes(uid, product);
            _hands.PickupOrDrop(buyer, product);

            HandleRefundComp(uid, component, product);

            var xForm = Transform(product);

            if (xForm.ChildCount > 0)
            {
                var childEnumerator = xForm.ChildEnumerator;
                while (childEnumerator.MoveNext(out var child))
                {
                    component.BoughtEntities.Add(child);
                }
            }
        }

        //give action
        if (!string.IsNullOrWhiteSpace(listing.ProductAction))
        {
            EntityUid? actionId;
            // I guess we just allow duplicate actions?
            // Allow duplicate actions and just have a single list buy for the buy-once ones.
            if (listing.ApplyToMob || !_mind.TryGetMind(buyer, out var mind, out _))
                actionId = _actions.AddAction(buyer, listing.ProductAction);
            else
                actionId = _actionContainer.AddAction(mind, listing.ProductAction);

            // Add the newly bought action entity to the list of bought entities
            // And then add that action entity to the relevant product upgrade listing, if applicable
            if (actionId != null)
            {
                HandleRefundComp(uid, component, actionId.Value);

                if (listing.ProductUpgradeId != null)
                {
                    foreach (var upgradeListing in component.FullListingsCatalog)
                    {
                        if (upgradeListing.ID == listing.ProductUpgradeId)
                        {
                            upgradeListing.ProductActionEntity = actionId.Value;
                            break;
                        }
                    }
                }
            }
        }

        if (listing is { ProductUpgradeId: not null, ProductActionEntity: not null })
        {
            if (listing.ProductActionEntity != null)
            {
                component.BoughtEntities.Remove(listing.ProductActionEntity.Value);
            }

            if (!_actionUpgrade.TryUpgradeAction(listing.ProductActionEntity, out var upgradeActionId))
            {
                if (listing.ProductActionEntity != null)
                    HandleRefundComp(uid, component, listing.ProductActionEntity.Value);

                return;
            }

            listing.ProductActionEntity = upgradeActionId;

            if (upgradeActionId != null)
                HandleRefundComp(uid, component, upgradeActionId.Value);
        }

        if (listing.ProductLanguage != null) //Starlight-start
        {
            var language = _languageSystem.GetLanguagePrototype(listing.ProductLanguage);
            if  (language != null) _languageSystem.AddLanguage(buyer, language);
        } // Starlight-end

        if (listing.ProductEvent != null)
        {
            if (!listing.RaiseProductEventOnUser)
                RaiseLocalEvent(listing.ProductEvent);
            else
                RaiseLocalEvent(buyer, listing.ProductEvent);
        }

        if (listing.DisableRefund)
        {
            component.RefundAllowed = false;
        }

        //log dat shit.
        // Starlight: Get the resolved name without any placeholders
        var resolvedName = ListingLocalisationHelpers.GetLocalisedNameOrEntityName(listing, _proto);

        // Remove any stock count or "Out of Stock" text for the log
        if (resolvedName.Contains(" ("))
        {
            resolvedName = resolvedName.Substring(0, resolvedName.IndexOf(" ("));
        }

        var logImpact = LogImpact.Low;
        var logExtraInfo = "";
        if (component.ExpectedFaction?.Count > 0 && !_npcFaction.IsMemberOfAny(buyer, component.ExpectedFaction))
        {
            logImpact = LogImpact.High;
            logExtraInfo = ", but was not from an expected faction";

            if (HasComp<MindShieldComponent>(buyer))
            {
                logImpact = LogImpact.Extreme;
                logExtraInfo += " while also possessing a mindshield";
            }
        }

        _admin.Add(LogType.StorePurchase,
            logImpact,
            $"{ToPrettyString(buyer):player} purchased listing \"{resolvedName}\" from {ToPrettyString(uid)}{logExtraInfo}."); // Starlight

        listing.PurchaseAmount++; //track how many times something has been purchased
        var soundTarget = msg.SoundSource is { } soundSource ? GetEntity(soundSource) : uid;
        _audio.PlayEntity(component.BuySuccessSound, msg.Actor, soundTarget); //cha-ching!

        // STARTLIGHT START: Check if this listing has a StockLimitedListingCondition
        if (listing.Conditions != null)
        {
            foreach (var condition in listing.Conditions)
            {
                if (condition is StockLimitedListingCondition stockCondition)
                {
                    // Get the buyer's name
                    var buyerName = "Unknown";
                    buyerName = MetaData(buyer).EntityName;

                    // Update the stock count and last purchaser
                    StockLimitedListingCondition.OnItemPurchased(listing.ID, buyerName, stockCondition.StockLimit);
                    break;
                }
            }
        }

        // STARLIGHT END

        var buyFinished = new StoreBuyFinishedEvent
        {
            PurchasedItem = listing,
            StoreUid = uid
        };
        RaiseLocalEvent(ref buyFinished);

        // STARLIGHT: Raise an event to notify other systems that a purchase was completed
        var purchaseCompletedEvent = new StorePurchaseCompletedEvent(listing.ID, uid, buyer);
        RaiseLocalEvent(ref purchaseCompletedEvent);

        UpdateUserInterface(buyer, uid, component);

        // STARLIGHT START: If this was a stock-limited item, update all USSP uplink UIs
        if (listing.Conditions != null)
        {
            foreach (var condition in listing.Conditions)
            {
                if (condition is StockLimitedListingCondition)
                {
                    UpdateAllUSSPUplinkUIs();
                    break;
                }
            }
        }

        _roundStatistics.RecordStorePurchase(component.Name.Id, listing.ID, listing.IsCostModified); // Starlight
    }

    /// <summary>
    /// Updates all USSP uplink UIs to ensure they show the latest stock counts and last purchaser information.
    /// </summary>
    public void UpdateAllUSSPUplinkUIs()
    {
        foreach (var uid in EntityManager.System<RevolutionaryRuleSystem>().EnumerateHeadStores())
        {
            if (!TryComp<StoreComponent>(uid, out var storeComp))
                continue;

            var buyer = storeComp.AccountOwner ?? uid;
            UpdateUserInterface(buyer, uid, storeComp);
        }
    }

    /// <summary>
    /// Purchased radios and implanter contents are windows onto the store that sold them.
    /// </summary>
    private void LinkPurchasedRemotes(EntityUid store, EntityUid product)
    {
        if (TryComp<RemoteStoreComponent>(product, out var remote))
            SetRemoteStore(product, store);

        if (!TryComp(product, out TransformComponent? xform))
            return;

        var children = xform.ChildEnumerator;
        while (children.MoveNext(out var child))
            LinkPurchasedRemotes(store, child);
    }

    /// <summary>
    /// Handles dispensing the currency you requested to be withdrawn.
    /// </summary>
    /// <remarks>
    /// This would need to be done should a currency with decimal values need to use it.
    /// not quite sure how to handle that
    /// </remarks>
    private void OnRequestWithdraw(EntityUid uid, StoreComponent component, StoreRequestWithdrawMessage msg)
    {
        if (msg.Amount <= 0)
            return;

        //make sure we have enough cash in the bank and we actually support this currency
        if (!component.Balance.TryGetValue(msg.Currency, out var currentAmount) || currentAmount < msg.Amount)
            return;

        //make sure a malicious client didn't send us random shit
        if (!_proto.TryIndex<CurrencyPrototype>(msg.Currency, out var proto))
            return;

        //we need an actually valid entity to spawn. This check has been done earlier, but just in case.
        if (proto.Cash == null || !proto.CanWithdraw)
            return;

        var buyer = msg.Actor;

        FixedPoint2 amountRemaining = msg.Amount;
        var coordinates = Transform(buyer).Coordinates;

        var sortedCashValues = proto.Cash.Keys.OrderByDescending(x => x).ToList();
        foreach (var value in sortedCashValues)
        {
            var cashId = proto.Cash[value];
            var amountToSpawn = (int) MathF.Floor((float) (amountRemaining / value));
            var ents = _stack.SpawnMultipleAtPosition(cashId, amountToSpawn, coordinates);
            if (ents.FirstOrDefault() is {} ent)
                _hands.PickupOrDrop(buyer, ent);
            amountRemaining -= value * amountToSpawn;
        }

        // STARLIGHT: Play sound effect when withdrawing telebonds
        if (msg.Currency == "Telebond")
        {
            var soundPath = new SoundPathSpecifier("/Audio/Machines/diagnoser_printing.ogg");
            _audio.PlayPvs(soundPath, uid, AudioParams.Default.WithMaxDistance(3f).WithVolume(5f));
        }

        component.Balance[msg.Currency] -= msg.Amount;
        UpdateUserInterface(buyer, uid, component);
    }

    private void OnRequestRefund(EntityUid uid, StoreComponent component, StoreRequestRefundMessage args)
    {
        // TODO: Remove guardian/holopara

        if (args.Actor is not { Valid: true } buyer)
            return;

        if (!IsOnStartingMap(uid, component))
        {
            DisableRefund(uid, component);
            UpdateUserInterface(buyer, uid, component);
        }

        if (!component.RefundAllowed || component.BoughtEntities.Count == 0)
            return;

        _admin.Add(LogType.StoreRefund, LogImpact.Low, $"{ToPrettyString(buyer):player} has refunded their purchases from {ToPrettyString(uid):store}");

        for (var i = component.BoughtEntities.Count - 1; i >= 0; i--)
        {
            var purchase = component.BoughtEntities[i];

            if (!Exists(purchase))
                continue;

            component.BoughtEntities.RemoveAt(i);

            _actionContainer.RemoveAction(purchase, logMissing: false);

            Del(purchase);
        }

        component.BoughtEntities.Clear();

        foreach (var (currency, value) in component.BalanceSpent)
        {
            component.Balance[currency] += value;
        }

        // Reset store back to its original state
        // Starlight-start
        RefreshAllListings((uid, component));
        // Starlight-end
        component.BalanceSpent = new();
        UpdateUserInterface(buyer, uid, component);
    }

    private void HandleRefundComp(EntityUid uid, StoreComponent component, EntityUid purchase)
    {
        component.BoughtEntities.Add(purchase);
        var refundComp = EnsureComp<StoreRefundComponent>(purchase);
        refundComp.StoreEntity = uid;
        refundComp.BoughtTime = _timing.CurTime;
    }

    private bool IsOnStartingMap(EntityUid store, StoreComponent component)
    {
        var xform = Transform(store);
        return component.StartingMap == xform.MapUid;
    }

    /// <summary>
    ///     Disables refunds for this store
    /// </summary>
    public void DisableRefund(EntityUid store, StoreComponent? component = null)
    {
        if (!Resolve(store, ref component))
            return;

        component.RefundAllowed = false;
    }
}

/// <summary>
/// Event of successfully finishing purchase in store (<see cref="StoreSystem"/>.
/// </summary>
/// <param name="StoreUid">EntityUid on which store is placed.</param>
/// <param name="PurchasedItem">ListingItem that was purchased.</param>
[ByRefEvent]
public readonly record struct StoreBuyFinishedEvent(
    EntityUid StoreUid,
    ListingDataWithCostModifiers PurchasedItem
);
