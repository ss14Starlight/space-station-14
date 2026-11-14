using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Client.Cargo.UI;

public sealed partial class CargoConsoleMenu
{
    public void PopulateBarcodeAccounts()
    {
        if (!_entityManager.TryGetComponent<StationBankAccountComponent>(_station, out var bank))
            return;

        var i = 0;
        BarcodeAccountOptions.Clear();
        ProtoId<CargoAccountPrototype>? ownAccount = null;
        if (_entityManager.TryGetComponent<CargoOrderConsoleComponent>(_owner, out var orderConsole))
        {
            var accountProto = _protoManager.Index(orderConsole.Account);
            ownAccount = accountProto;
        }
        var selectedAccount = 0;
        foreach (var account in bank.Accounts.Keys)
        {
            if (account == bank.PrimaryAccount) continue;
            var accountProto = _protoManager.Index(account);
            BarcodeAccountOptions.AddItem(Loc.GetString(accountProto.Code),
                i);
            BarcodeAccountOptions.SetItemMetadata(i, account);
            if(ownAccount != null && accountProto == ownAccount)
            {
                selectedAccount = i;
            }
            i++;
        }
        BarcodeAccountOptions.SelectId(selectedAccount);

    }
}
