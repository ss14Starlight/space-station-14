using System.Linq;
using System.Numerics;
using Content.Client._Starlight.UserInterface.Controls;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared.IdentityManagement;
using Content.Shared.VendingMachines;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.VendingMachines.UI;

public sealed class VendingWindow : FancyWindow
{
    private const int TabAll = 0;
    private const int TabRegular = 1;
    private const int TabExtra = 2;

    private readonly IEntityManager _entities;
    private readonly IPrototypeManager _prototypes;

    private readonly Label _balance = new() { VerticalAlignment = VAlignment.Center, Visible = false };
    private readonly LineEdit _search = new() { HorizontalExpand = true };
    private readonly SegmentedButtons _tabs = new() { Visible = false };
    private readonly WrapContainer _items = new() { SeparationOverride = 6, CrossSeparationOverride = 6, Margin = new Thickness(4) };
    private readonly Label _empty = new()
    {
        Text = Loc.GetString("vending-machine-component-try-eject-out-of-stock"),
        StyleClasses = { StyleClass.LabelSubText },
        HorizontalAlignment = HAlignment.Center,
        VerticalAlignment = VAlignment.Center,
        VerticalExpand = true,
        Visible = false,
    };

    private readonly Dictionary<string, EntityUid> _dummies = new(); // Previews
    private readonly List<(int Index, VendingMachineInventoryEntry Entry, CatalogCard Card)> _cards = new();
    private readonly List<(string Id, InventoryType Type)> _stock = new(); // Every entry, including ones without a card
    private int _tab = TabAll;

    /// <summary>
    /// Raised with the index of the pressed item in the inventory last given to <see cref="Populate"/>.
    /// </summary>
    public event Action<int>? OnItemSelected;

    public VendingWindow()
    {
        IoCManager.Resolve(ref _entities);
        IoCManager.Resolve(ref _prototypes);

        MinSize = new Vector2(300, 260);
        SetSize = new Vector2(500, 460);

        _search.PlaceHolder = Loc.GetString("vending-machine-component-search-filter");
        _search.OnTextChanged += _ => Filter();

        _tabs.AddItem(Loc.GetString("vending-window-tab-all"), TabAll);
        _tabs.AddItem(Loc.GetString("vending-window-tab-regular"), TabRegular);
        _tabs.AddItem(Loc.GetString("vending-window-tab-extra"), TabExtra);
        _tabs.SelectId(TabAll);
        _tabs.OnItemSelected += args =>
        {
            _tab = args.Id;
            Filter();
        };

        ContentsContainer.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(4),
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 8,
                    Children = { _balance, _search },
                },
                _tabs,
                new PanelContainer
                {
                    StyleClasses = { "InsetPanel" },
                    VerticalExpand = true,
                    Children =
                    {
                        new ScrollContainer
                        {
                            HScrollEnabled = false,
                            VerticalExpand = true,
                            ReserveScrollbarSpace = true,
                            Children = { _items },
                        },
                        _empty,
                    },
                },
            },
        });
    }

    public void UpdateBalance(int balance)
        => _balance.Text = Loc.GetString("vending-machine-balance", ("balance", balance));

    public void ToggleBalance(bool? enable = null)
        => _balance.Visible = enable ?? !_balance.Visible;

    /// <summary>
    /// Rebuilds the cards for the machine's stock.
    /// </summary>
    public void Populate(List<VendingMachineInventoryEntry> inventory, bool enabled, bool showPrices)
    {
        _items.RemoveAllChildren();
        _cards.Clear();
        _stock.Clear();
        foreach (var entry in inventory)
            _stock.Add((entry.ID, entry.Type));

        for (var i = 0; i < inventory.Count; i++)
        {
            var entry = inventory[i];
            if (!_prototypes.TryIndex<EntityPrototype>(entry.ID, out var prototype))
                continue;

            if (!_dummies.TryGetValue(entry.ID, out var dummy))
            {
                dummy = _entities.Spawn(entry.ID);
                _dummies.Add(entry.ID, dummy);
            }

            var card = new CatalogCard(Identity.Name(dummy, _entities), new Vector2(104, 128)) { SearchText = prototype.Description };
            card.SetPreview(prototype.ID);
            if (!string.IsNullOrWhiteSpace(prototype.Description))
                card.ToolTip = $"{card.Title}\n{prototype.Description}";
            else
                card.ToolTip = card.Title;

            var index = i;
            card.OnPressed += _ => OnItemSelected?.Invoke(index);

            _cards.Add((i, entry, card));
            _items.AddChild(card);
        }

        UpdateCards(inventory, enabled, showPrices);
        _tabs.Visible = inventory.Any(e => e.Type != InventoryType.Regular);
        if (!_tabs.Visible && _tab != TabAll)
        {
            _tab = TabAll;
            _tabs.SelectId(TabAll);
        }

        Filter();
    }

    /// <summary>
    /// Updates amounts and prices in place. Falls back to a rebuild when the stock itself changed.
    /// </summary>
    public void UpdateAmounts(List<VendingMachineInventoryEntry> inventory, bool enabled, bool showPrices)
    {
        if (inventory.Count != _stock.Count
            || inventory.Where((entry, i) => entry.ID != _stock[i].Id || entry.Type != _stock[i].Type).Any())
        {
            Populate(inventory, enabled, showPrices);
            return;
        }

        UpdateCards(inventory, enabled, showPrices);
        Filter();
    }

    private void UpdateCards(List<VendingMachineInventoryEntry> inventory, bool enabled, bool showPrices)
    {
        for (var i = 0; i < _cards.Count; i++)
        {
            var (index, _, card) = _cards[i];
            var entry = inventory[index];
            _cards[i] = (index, entry, card);

            card.SetTopRight($"×{entry.Amount}");
            if (showPrices && entry.Price > 0)
                card.SetDetails((Loc.GetString("vending-window-price", ("price", entry.Price)), null));
            else
                card.SetDetails();

            card.SetLocked(entry.Amount == 0, showLock: false);
            card.Disabled = !enabled || entry.Amount == 0;
        }
    }

    private void Filter()
    {
        var search = _search.Text.Trim();
        foreach (var (_, entry, card) in _cards)
        {
            var inTab = _tab switch
            {
                TabRegular => entry.Type == InventoryType.Regular,
                TabExtra => entry.Type != InventoryType.Regular,
                _ => true,
            };
            card.Visible = inTab && card.Matches(search);
        }

        _empty.Visible = _cards.Count == 0;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        foreach (var dummy in _dummies.Values)
            _entities.QueueDeleteEntity(dummy);
        _dummies.Clear();
    }
}
