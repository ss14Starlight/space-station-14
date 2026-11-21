using Content.Client._Starlight.VendingMachines.UI;
using Content.Shared.VendingMachines;
using Robust.Client.UserInterface;
using System.Linq;

namespace Content.Client.VendingMachines
{
    public sealed class VendingMachineBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private VendingWindow? _menu; // Starlight-edit

        [ViewVariables]
        private List<VendingMachineInventoryEntry> _cachedInventory = new();
        private bool _showPrices = true; // Starlight-edit

        public VendingMachineBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _menu = this.CreateWindowCenteredLeft<VendingWindow>(); // Starlight-edit
            _menu.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
            _menu.OnItemSelected += OnItemSelected;

            // Starlight-start
            Refresh();

            if (_showPrices)
                RequestBalance(); // Client ask too, server also pushes on open now
            // Starlight-end
        }

        protected override void ReceiveMessage(BoundUserInterfaceMessage message)
        {
            if (message is VendingMachineBalanceUpdateMessage balanceMessage)
            {
                _menu?.UpdateBalance(balanceMessage.Balance);
            }
        }

        /// <summary>
        /// Requests current balance from server
        /// </summary>
        private void RequestBalance()
            => SendMessage(new VendingMachineRequestBalanceMessage()); // Starlight-edit

        public void Refresh()
        {
            var enabled = EntMan.TryGetComponent(Owner, out VendingMachineComponent? bendy) && !bendy.Ejecting;
            _showPrices = bendy?.ShowPrices ?? true;

            var system = EntMan.System<VendingMachineSystem>();
            _cachedInventory = system.GetAllInventory(Owner);

            _menu?.Populate(_cachedInventory, enabled, _showPrices);

            // Starlight-start
            if (_menu != null)
            {
                if (_showPrices)
                {
                    _menu.ToggleBalance(true);
                    RequestBalance();
                }
                else
                {
                    _menu.ToggleBalance(false);
                }
            }
            // Starlight-end
        }

        public void UpdateAmounts()
        {
            var enabled = EntMan.TryGetComponent(Owner, out VendingMachineComponent? bendy) && !bendy.Ejecting;
            _showPrices = bendy?.ShowPrices ?? true; // Starlight-edit

            var system = EntMan.System<VendingMachineSystem>();
            _cachedInventory = system.GetAllInventory(Owner);
            _menu?.UpdateAmounts(_cachedInventory, enabled, _showPrices); // Starlight-edit

            // Starlight-start
            if (_menu != null)
            {
                if (_showPrices)
                {
                    _menu.ToggleBalance(true);
                    RequestBalance();
                }
                else
                {
                    _menu.ToggleBalance(false);
                }
            }
            // Starlight-end
        }

        private void OnItemSelected(int itemIndex) // Starlight-edit
        {
            if (_cachedInventory.Count == 0)
                return;

            var selectedItem = _cachedInventory.ElementAtOrDefault(itemIndex);

            if (selectedItem == null)
                return;

            SendMessage(new VendingMachineEjectMessage(selectedItem.Type, selectedItem.ID)); // Starlight-edit: Use non-predicted message so that the server processes the vend
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            if (_menu == null)
                return;

            _menu.OnItemSelected -= OnItemSelected;
            _menu.OnClose -= Close;
            _menu.Dispose();
        }
    }
}
