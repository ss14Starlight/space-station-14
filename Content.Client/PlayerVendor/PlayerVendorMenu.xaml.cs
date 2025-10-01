using System.Numerics;
using System.Linq;
using Content.Shared.PlayerVendor;
using Content.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StyleNano;
using Content.Client.Administration.UI.CustomControls;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Player;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.PlayerVendor;

public sealed partial class PlayerVendorMenu : FancyWindow
{
    private BoxContainer _list = default!;
    private LineEdit _priceEdit = default!; 
    private Button _setPriceButton = default!;
    private Button _purchaseButton = default!;
    private Button _withdrawButton = default!; 
    private Button _lockButton = default!; 
    private Button _claimButton = default!;
    private Button _refundButton = default!;
    private Label _ownerLabel = default!; 
    private Label _balanceLabel = default!;
    private Label _depositLabel = default!;
    private BoxContainer _priceBox = default!;

    private string? _selectedEntry;
    private string? _currentUserId;

    public event Action<string>? OnPurchase;
    public event Action<string, int>? OnSetPrice;
    public event Action? OnWithdraw;
    public event Action? OnToggleLock;
    public event Action? OnClaim;
    public event Action? OnRefund;

    public PlayerVendorMenu()
    {
        MinSize = new Vector2(600, 450);
        SetSize = new Vector2(700, 550);
        RobustXamlLoader.Load(this);
        MapControls();
        ConfigureVisuals();
        Title = "🏪 " + Loc.GetString("player-vendor-ui-title");
        WireEvents();
    }

    public void SetCurrentUserId(string? userId)
    {
        _currentUserId = userId;
    }

    private VSeparator _vSeparator = default!;

    private void MapControls()
    {
        _list = FindControl<BoxContainer>("ItemListContainer");
        _priceEdit = FindControl<LineEdit>("PriceEdit");
        _setPriceButton = FindControl<Button>("SetPriceButton");
        _purchaseButton = FindControl<Button>("PurchaseButton");
        _withdrawButton = FindControl<Button>("WithdrawButton");
        _lockButton = FindControl<Button>("LockButton");
        _claimButton = FindControl<Button>("ClaimButton");
        _refundButton = FindControl<Button>("RefundButton");
        _ownerLabel = FindControl<Label>("OwnerLabel");
        _balanceLabel = FindControl<Label>("BalanceLabel");
        _depositLabel = FindControl<Label>("DepositLabel");
        _priceBox = FindControl<BoxContainer>("PriceBox");
        _vSeparator = FindControl<VSeparator>("VendorSeparator");
    }

    private void ConfigureVisuals()
    {
        if (_vSeparator != null)
        {
            _vSeparator.Color = NanoGold;
        }
    }

    private void WireEvents()
    {
        _refundButton.OnPressed += _ => OnRefund?.Invoke();
        _setPriceButton.OnPressed += _ =>
        {
            if (_selectedEntry == null)
                return;
            if (int.TryParse(_priceEdit.Text, out var price))
                OnSetPrice?.Invoke(_selectedEntry, price);
        };
        _withdrawButton.OnPressed += _ => OnWithdraw?.Invoke();
        _lockButton.OnPressed += _ => OnToggleLock?.Invoke();
        _claimButton.OnPressed += _ => OnClaim?.Invoke();
        _purchaseButton.OnPressed += _ =>
        {
            if (_selectedEntry != null)
                OnPurchase?.Invoke(_selectedEntry);
        };
    }

    public void Populate(PlayerVendorUiState state)
    {
        _list.DisposeAllChildren();

        // Populate entries
        if (!state.Entries.Any())
        {
            _list.AddChild(new Label
            {
                Text = Loc.GetString("player-vendor-ui-empty"),
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(0, 16),
                FontColorOverride = Color.Gray
            });
        }
        else
        {
            foreach (var entry in state.Entries)
            {
                var amount = state.Amounts.GetValueOrDefault(entry, 0);
                var price = state.Prices.GetValueOrDefault(entry, state.DefaultPrice);
                var representative = state.Representatives.GetValueOrDefault(entry, null);

                var rowButton = new Button
                {
                    HorizontalExpand = true,
                    Margin = new Thickness(2),
                    ClipText = false,
                    Name = entry
                };
                var hBox = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 6,
                    HorizontalExpand = true
                };
                rowButton.AddChild(hBox);

                if (representative != null)
                {
                    var spriteView = new SpriteView
                    {
                        SetSize = new Vector2(32, 32),
                        OverrideDirection = Direction.South
                    };
                    spriteView.SetEntity(representative.Value);
                    hBox.AddChild(spriteView);
                }
                else
                {
                    hBox.AddChild(new PanelContainer
                    {
                        SetSize = new Vector2(32, 32),
                        ModulateSelfOverride = Color.DarkGray
                    });
                }

                var textBox = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    HorizontalExpand = true
                };
                hBox.AddChild(textBox);

                textBox.AddChild(new Label
                {
                    Text = entry,
                    HorizontalExpand = true,
                    FontColorOverride = Color.White
                });
                textBox.AddChild(new Label
                {
                    Text = amount > 0 ? Loc.GetString("player-vendor-ui-entry-info", ("amount", amount), ("price", price)) : Loc.GetString("player-vendor-ui-entry-info-empty", ("price", price)),
                    FontColorOverride = amount > 0 ? Color.FromHex("#9fe29f") : Color.FromHex("#e29f9f")
                });

                rowButton.ModulateSelfOverride = Color.Gray;
                if (amount <= 0)
                    rowButton.Disabled = true;
                rowButton.ToolTip = Loc.GetString("player-vendor-ui-entry-tooltip", ("entry", entry), ("price", price));
                rowButton.OnPressed += _ =>
                {
                    _selectedEntry = entry;
                    HighlightSelection();
                };
                _list.AddChild(rowButton);
            }
        }

        // Ownership label
        if (state.OwnerEntityNet == null)
        {
            _ownerLabel.Text = Loc.GetString("player-vendor-ui-owner-none");
            _ownerLabel.FontColorOverride = Color.Orange;
        }
        else
        {
            var display = state.OwnerName ?? "?";
            _ownerLabel.Text = Loc.GetString("player-vendor-ui-owner", ("owner", display));
            _ownerLabel.FontColorOverride = Color.LightBlue;
        }

        var isOwner = state.IsOwner;
        if (state.OwnerEntityNet != null)
        {
            var playerManager = IoCManager.Resolve<IPlayerManager>();
            var entityManager = IoCManager.Resolve<IEntityManager>();
            var localSession = playerManager.LocalSession;
            if (localSession?.AttachedEntity != null)
            {
                var localEntityNet = entityManager.GetNetEntity(localSession.AttachedEntity.Value);
                isOwner = localEntityNet == state.OwnerEntityNet;
            }
        }
        var isDepositor = state.IsActiveDepositor || (state.ActiveDepositorUserId != null && _currentUserId == state.ActiveDepositorUserId);

        if (isOwner)
        {
            _balanceLabel.Text = "💰 " + Loc.GetString("player-vendor-ui-balance", ("amount", state.Balance));
            _balanceLabel.FontColorOverride = state.Balance > 0 ? Color.LightGreen : Color.Gray;
            _balanceLabel.Visible = true;
        }
        else
        {
            _balanceLabel.Visible = false;
        }

        var deposit = state.ActiveDeposit;
        _depositLabel.Text = Loc.GetString("player-vendor-ui-deposit", ("amount", deposit));
        if (isDepositor && deposit > 0)
        {
            _depositLabel.FontColorOverride = Color.LightGreen;
            _refundButton.ToolTip = Loc.GetString("player-vendor-ui-refund-tooltip") + " (" + deposit + "₡)";
        }
        else
        {
            _depositLabel.FontColorOverride = Color.Gray;
            _refundButton.ToolTip = Loc.GetString("player-vendor-ui-refund-tooltip");
        }
        _depositLabel.Visible = true;

        var lockIcon = state.Locked ? "🔒" : "🔓";
        _lockButton.Text = $"{lockIcon} {(state.Locked ? Loc.GetString("player-vendor-ui-locked") : Loc.GetString("player-vendor-ui-unlocked"))}";
        _lockButton.ModulateSelfOverride = state.Locked ? Color.FromHex("#ff6b6b") : Color.FromHex("#51cf66");

        var canClaim = state.OwnerEntityNet == null;
        _setPriceButton.Visible = isOwner;
        _priceBox.Visible = isOwner;
        _priceEdit.Editable = isOwner;
        _withdrawButton.Visible = isOwner;
        _lockButton.Visible = isOwner;
        _claimButton.Visible = canClaim;
        _refundButton.Visible = isDepositor && state.ActiveDeposit > 0;

        HighlightSelection();
        if (_selectedEntry == null)
            _purchaseButton.Text = Loc.GetString("player-vendor-ui-purchase-button");
    }

    private void HighlightSelection()
    {
        foreach (var child in _list.Children)
        {
            if (child is not Button btn)
                continue;

            var entryName = btn.Name;
            var isSelected = _selectedEntry != null && entryName == _selectedEntry;

            if (isSelected)
                btn.Modulate = Color.FromHex("#5c8dd6");
            else
                btn.Modulate = Color.FromHex("#a5aaa7");
        }

        _purchaseButton.Disabled = _selectedEntry == null;

        if (_selectedEntry != null)
            _purchaseButton.Text = Loc.GetString("player-vendor-ui-purchase-item-button", ("item", _selectedEntry));
        else
            _purchaseButton.Text = Loc.GetString("player-vendor-ui-purchase-button");
    }
}

