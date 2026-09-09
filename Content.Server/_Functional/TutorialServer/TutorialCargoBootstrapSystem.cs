using Content.Server.Cargo.Components;
using Content.Server.Station.Systems;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Attaches a cargo trade station to QM practice grids, seeds an unapproved order,
/// and fulfills Approves without a live telepad network.
/// </summary>
public sealed partial class TutorialCargoBootstrapSystem : EntitySystem // Starlight edit
{
    private static readonly EntProtoId TradeStationProto = "TutorialCargoTradeStation";
    private static readonly ProtoId<TagPrototype> OrdersConsoleTag = "TutorialCargoOrders";
    private static readonly ProtoId<TagPrototype> SellPadTag = "TutorialCargoSell";
    private static readonly ProtoId<TagPrototype> PurchaseTag = "TutorialCargoPurchase";
    private static readonly ProtoId<CargoAccountPrototype> CargoAccount = "Cargo";
    private static readonly ProtoId<CargoProductPrototype> SeedProduct = "JanitorialCleanerGrenades";
    private static readonly EntProtoId FulfilledCrateProto = "CrateJanitorialCleanerGrenades";

    // Starlight begin
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    // Starlight end

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FulfillCargoOrderEvent>(OnFulfillCargoOrder);
    }

    public void TryConfigureOnGrid(EntityUid gridUid, TutorialRolePrototype role)
    {
        if (role.ID != "TutorialQuartermaster")
            return;

        if (_station.GetOwningStation(gridUid) is { } existing &&
            HasComp<TutorialCargoStationComponent>(existing))
        {
            SeedOrder(existing);
            PrepareSellables(gridUid);
            return;
        }

        var tradeStation = Spawn(TradeStationProto, MapCoordinates.Nullspace);
        EnsureComp<TutorialCargoStationComponent>(tradeStation);
        _station.AddGridToStation(tradeStation, gridUid, name: "Tutorial Cargo");
        SeedOrder(tradeStation);
        PrepareSellables(gridUid);
    }

    private void PrepareSellables(EntityUid gridUid)
    {
        EntityCoordinates? sellCoords = null;
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            if (_tags.HasTag(uid, SellPadTag))
                sellCoords = xform.Coordinates;

            var proto = MetaData(uid).EntityPrototype?.ID;
            if (proto is not ("CrateGenericSteel" or "CrateHydroponics" or "CrateJanitorialCleanerGrenades"))
                continue;

            if (xform.Anchored)
                _transform.Unanchor(uid, xform);
        }

        // Snap the practice crate onto the sell pad if we found one.
        if (sellCoords == null)
            return;

        var crateQuery = EntityQueryEnumerator<TransformComponent>();
        while (crateQuery.MoveNext(out var uid, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            if (MetaData(uid).EntityPrototype?.ID != "CrateGenericSteel")
                continue;

            _transform.SetCoordinates(uid, sellCoords.Value);
            if (xform.Anchored)
                _transform.Unanchor(uid, xform);
            break;
        }
    }

    private void SeedOrder(EntityUid station)
    {
        if (!TryComp<StationCargoOrderDatabaseComponent>(station, out var db))
            return;

        if (!db.Orders.TryGetValue(CargoAccount, out var list))
        {
            list = new List<CargoOrderData>();
            db.Orders[CargoAccount] = list;
        }

        if (list.Exists(o => !o.Approved && o.CargoProductId == SeedProduct)) // Starlight edit
            return;


        db.NumOrdersCreated++;
        // Starlight begin
        var product = _proto.Index(SeedProduct);
        var order = new CargoOrderData(db.NumOrdersCreated, product.Product, product.Name, product.Cost, 1, "Tutorial",
            "Tutorial seed order", CargoAccount, GetNetEntity(station), SeedProduct, null, 0, 0);
        // Starlight end
        list.Add(order);
    }

    private void OnFulfillCargoOrder(ref FulfillCargoOrderEvent args)
    {
        // Cargo bay shuttle arenas + QM practice both use TutorialCargoTradeStation.
        // Stub fulfill so Approve works without telepads.
        if (!HasComp<TutorialCargoStationComponent>(args.Station.Owner))
            return;

        if (args.Handled)
            return;

        args.FulfillmentEntity = args.Station.Owner;
        args.Handled = true;

        var spawnCoords = FindBuyPadCoordinates(args.OrderConsole.Owner)
            ?? FindOrdersConsoleCoordinates(args.Station.Owner);

        if (spawnCoords == null)
            return;

        var crate = Spawn(FulfilledCrateProto, spawnCoords.Value);
        var crateXform = Transform(crate);
        if (crateXform.Anchored)
            _transform.Unanchor(crate, crateXform);

        _tags.AddTag(crate, PurchaseTag);
    }

    private EntityCoordinates? FindBuyPadCoordinates(EntityUid consoleUid)
    {
        if (Transform(consoleUid).MapUid is not {} mapUid) return null;

        var query = EntityQueryEnumerator<CargoPalletComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pallet, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            if (pallet.PalletType != BuySellType.Buy)
                continue;

            return xform.Coordinates;
        }

        return null;
    }

    private EntityCoordinates? FindOrdersConsoleCoordinates(EntityUid station)
    {
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var xform))
        {
            if (xform.GridUid == null)
                continue;

            if (_station.GetOwningStation(uid) != station)
                continue;

            if (!_tags.HasTag(uid, OrdersConsoleTag))
                continue;

            return xform.Coordinates;
        }

        return null;
    }
}
