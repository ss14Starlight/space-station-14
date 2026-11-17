// ReSharper disable CheckNamespace
using Content.Shared.Store;
using Robust.Client.UserInterface;

namespace Content.Client.Store.Ui;

public sealed partial class StoreBoundUserInterface
{
    /// <summary>
    /// Stores the latest UI state received before the store menu is initialized.
    /// </summary>
    private StoreUpdateState? _pendingState;

    protected override void Open()
    {
        base.Open();

        var menu = this.CreateWindow<StoreMenu>();
        _menu = menu;

        if (_storeSystem.TryGetStore(Owner, out var store))
            menu.Title = Loc.GetString(store.Value.Comp.Name);

        menu.OnListingButtonPressed += (_, listing) => SendMessage(new StoreBuyListingMessage(listing.ID, EntMan.GetNetEntity(Owner)));

        menu.OnCategoryButtonPressed += (_, category) =>
        {
            menu.CurrentCategory = category;
            menu.UpdateListing();
        };

        menu.OnWithdrawAttempt += (_, type, amount) => SendMessage(new StoreRequestWithdrawMessage(type, amount));

        menu.SearchTextUpdated += (_, search) =>
        {
            _search = search.Trim().ToLowerInvariant();
            UpdateListingsWithSearchFilter();
        };

        menu.OnRefundAttempt += (_) => SendMessage(new StoreRequestRefundMessage());

        // Diona's pollen shop uses the shared Store UI. Its initial StoreUpdateState
        // can arrive before Open() creates StoreMenu, so cache it until initialization.
        if (_pendingState is { } pending)
        {
            _pendingState = null;
            ApplyStoreUpdateState(pending);
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not StoreUpdateState msg)
            return;

        // The server can send an update before Open() creates the menu.
        if (_menu == null)
        {
            _pendingState = msg;
            return;
        }

        ApplyStoreUpdateState(msg);
    }

    private void ApplyStoreUpdateState(StoreUpdateState msg)
    {
        if (_menu is not { } menu)
            return;

        _listings = msg.Listings;

        menu.UpdateBalance(msg.Balance);
        menu.GridMode = msg.GridMode;

        UpdateListingsWithSearchFilter();

        menu.SetFooterVisibility(msg.ShowFooter);
        menu.UpdateRefund(msg.AllowRefund);
    }
}
