using System.Numerics;
using Content.Server._Starlight.Cargo.TamperSeal.Components;
using Content.Server.Cargo.Components;
using Content.Shared._Starlight.Cargo.TamperSeal.Components;
using Content.Shared.Atmos;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Labels.Components;
using Content.Shared.Paper;
using Content.Shared.Station.Components;
using Content.Shared.Tools;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem
{
    private float _tamperSealRewardMultiplier = 0.1f;
    private float _tamperSealPenaltyMultiplier = 0.1f;
    private float _tamperSealRefundMultiplier = 0.5f;

    private EntityUid? TryFulfillGasOrder(
        Entity<StationDataComponent> stationData,
        ProtoId<CargoAccountPrototype> account,
        CargoOrderData order,
        StationCargoOrderDatabaseComponent orderDatabase)
    {
        if (order.GasType is not { } gasType)
            return null;

        _listEnts.Clear();
        GetTradeStations(stationData, ref _listEnts);
        var gas = Enum.Parse<Gas>(gasType.Id);
        var orderSlipPrinted = new HashSet<EntityUid>();

        foreach (var trade in _listEnts)
        {
            var gasTanks = GetCargoGasPallets(trade, BuySellType.Buy);
            _random.Shuffle(gasTanks);
            var freeTanks = GetFreeCargoGasPallets(
                gasTanks,
                gas,
                order.GasMoles,
                order.GasTemperature,
                order.OrderQuantity);

            if (freeTanks.Count < order.OrderQuantity)
                continue;

            foreach (var gasTank in freeTanks)
            {
                if (order.NumDispatched >= order.OrderQuantity)
                    break;

                // A tank can appear multiple times when it has room for more than one order.
                var mixture = new GasMixture(gasTank.Component.Air.Volume)
                {
                    Temperature = order.GasTemperature,
                };
                mixture.SetMoles(gas, order.GasMoles);
                _atmosphereSystem.Merge(gasTank.Component.Air, mixture);

                // Cargo only needs one signed slip per pallet, even when multiple orders fit in it.
                if (orderSlipPrinted.Add(gasTank.Entity))
                {
                    CreateOrderSlip(
                        order,
                        account,
                        new EntityCoordinates(gasTank.Entity, Vector2.Zero),
                        orderDatabase.PrinterOutput,
                        null,
                        _protoMan.Index(order.Product).Name);
                }

                order.NumDispatched++;
            }

            return trade;
        }

        return null;
    }

    private void CreateOrderSlip(
        CargoOrderData order,
        ProtoId<CargoAccountPrototype> account,
        EntityCoordinates spawn,
        string? paperProto,
        EntityUid? item,
        string name)
    {
        var printed = Spawn(paperProto, spawn);
        if (!TryComp<PaperComponent>(printed, out var paper))
            return;

        var title = Loc.GetString("cargo-console-paper-print-name", ("orderNumber", order.OrderId));
        _metaSystem.SetEntityName(printed, title);

        var accountProto = _protoMan.Index(account);
        _paperSystem.SetContent((printed, paper),
            Loc.GetString(
                "cargo-console-paper-print-text",
                ("orderNumber", order.OrderId),
                ("itemName", name),
                ("orderQuantity", order.OrderQuantity),
                ("requester", order.Requester),
                ("reason", string.IsNullOrWhiteSpace(order.Reason)
                    ? Loc.GetString("cargo-console-paper-reason-default")
                    : order.Reason),
                ("account", Loc.GetString(accountProto.Name)),
                ("accountcode", Loc.GetString(accountProto.Code)),
                ("approver", string.IsNullOrWhiteSpace(order.Approver)
                    ? Loc.GetString("cargo-console-paper-approver-default")
                    : order.Approver)));

        if (item is { } itemUid && TryComp<PaperLabelComponent>(itemUid, out var label))
            _slots.TryInsert(itemUid, label.LabelSlot, printed, null);
    }

    private void ApplyCargoTamperSeal(
        EntityUid item,
        CargoOrderData order,
        ProtoId<CargoAccountPrototype> account)
    {
        if (!TryComp<TamperSealableComponent>(item, out var tamperSealable))
            return;

        var recipient = _protoMan.Index(account);
        var seal = EnsureComp<TamperSealComponent>(item);
        seal.Recipient = account;
        seal.RecipientName = recipient.TamperSealName;
        seal.RecipientExamineColor = recipient.Color;
        seal.Color = recipient.TamperSealColor;
        seal.Accesses = new List<TamperSealAccessPattern>(recipient.TamperSealAccesses);
        seal.DestroyToolQualities = new HashSet<ProtoId<ToolQualityPrototype>>(tamperSealable.DestroyToolQualities);

        var value = EnsureComp<TamperSealValueComponent>(item);
        value.StationId = GetEntity(order.StationId);
        value.Value = order.Price;
        value.Reward = (int) Math.Floor(_tamperSealRewardMultiplier * order.Price);
        value.Penalty = (int) Math.Ceiling(_tamperSealPenaltyMultiplier * order.Price);
        value.Refund = (int) Math.Ceiling(_tamperSealRefundMultiplier * order.Price);

        var integrity = EnsureComp<TamperSealIntegrityBeaconComponent>(item);
        integrity.StationId = GetEntity(order.StationId);

        DirtyEntity(item);
    }
}
