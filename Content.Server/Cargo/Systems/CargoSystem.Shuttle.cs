using System.Linq;
using Content.Server.Atmos.EntitySystems; //Starlight
using Content.Server.Cargo.Components;
using Content.Shared.Cargo;
using Content.Shared.Cargo.BUI;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Events;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem
{
    /*
     * Handles cargo shuttle / trade mechanics.
     */

    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!; //Starlight

    private static readonly SoundPathSpecifier ApproveSound = new("/Audio/Effects/Cargo/ping.ogg");
    private bool _lockboxCutEnabled;

    private void InitializeShuttle()
    {
        SubscribeLocalEvent<TradeStationComponent, GridSplitEvent>(OnTradeSplit);

        SubscribeLocalEvent<CargoPalletConsoleComponent, CargoPalletSellMessage>(OnPalletSale);
        SubscribeLocalEvent<CargoPalletConsoleComponent, CargoPalletAppraiseMessage>(OnPalletAppraise);
        SubscribeLocalEvent<CargoPalletConsoleComponent, BoundUIOpenedEvent>(OnPalletUIOpen);

        _cfg.OnValueChanged(CCVars.LockboxCutEnabled, (enabled) => { _lockboxCutEnabled = enabled; }, true);
    }

    #region Console
    private void UpdatePalletConsoleInterface(EntityUid uid)
    {
        if (Transform(uid).GridUid is not { } gridUid)
        {
            _uiSystem.SetUiState(uid,
                CargoPalletConsoleUiKey.Sale,
                new CargoPalletConsoleInterfaceState(0, 0, false));
            return;
        }
        GetPalletGoods(gridUid, out var toSell, out var goods);
        GetGasPalletStocks(gridUid, out var gasPallets, out var gasValues); //Starlight
        var totalAmount = goods.Sum(t => t.Item3) + gasValues.Sum(t => t.Item2); //Starlight-edit - add gasValues
        _uiSystem.SetUiState(uid,
            CargoPalletConsoleUiKey.Sale,
            new CargoPalletConsoleInterfaceState((int) totalAmount, toSell.Count, true));
    }

    private void OnPalletUIOpen(EntityUid uid, CargoPalletConsoleComponent component, BoundUIOpenedEvent args)
    {
        UpdatePalletConsoleInterface(uid);
    }

    /// <summary>
    /// Ok so this is just the same thing as opening the UI, its a refresh button.
    /// I know this would probably feel better if it were like predicted and dynamic as pallet contents change
    /// However.
    /// I dont want it to explode if cargo uses a conveyor to move 8000 pineapple slices or whatever, they are
    /// known for their entity spam i wouldnt put it past them
    /// </summary>

    private void OnPalletAppraise(EntityUid uid, CargoPalletConsoleComponent component, CargoPalletAppraiseMessage args)
    {
        UpdatePalletConsoleInterface(uid);
    }

    #endregion

    private void OnTradeSplit(EntityUid uid, TradeStationComponent component, ref GridSplitEvent args)
    {
        // If the trade station gets bombed it's still a trade station.
        foreach (var gridUid in args.NewGrids)
        {
            EnsureComp<TradeStationComponent>(gridUid);
        }
    }

    #region Shuttle
    /// GetCargoPallets(gridUid, BuySellType.Sell) to return only Sell pads
    /// GetCargoPallets(gridUid, BuySellType.Buy) to return only Buy pads
    private List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent PalletXform)> GetCargoPallets(EntityUid gridUid, BuySellType requestType = BuySellType.All)
    {
        _pads.Clear();

        var query = AllEntityQuery<CargoPalletComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var comp, out var compXform))
        {
            if (compXform.ParentUid != gridUid ||
                !compXform.Anchored)
            {
                continue;
            }

            if ((requestType & comp.PalletType) == 0)
            {
                continue;
            }

            _pads.Add((uid, comp, compXform));

        }

        return _pads;
    }

    private List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent Transform)>
        GetFreeCargoPallets(EntityUid gridUid,
            List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent Transform)> pallets)
    {
        _setEnts.Clear();

        List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent Transform)> outList = new();

        foreach (var pallet in pallets)
        {
            var aabb = _lookup.GetAABBNoContainer(pallet.Entity, pallet.Transform.LocalPosition, pallet.Transform.LocalRotation);

            if (_lookup.AnyLocalEntitiesIntersecting(gridUid, aabb, LookupFlags.Dynamic))
                continue;

            outList.Add(pallet);
        }

        return outList;
    }

    /// <summary>
    /// Starlight
    /// Get the list of Gas Pallets (name pending) for selling gas.
    /// </summary>
    /// <remarks>
    /// Unlike Cargo Pallets, Cargo Gas Pallets only support selling for now, so no second parameter like GetCargoPallets
    /// </remarks>
    private List<(EntityUid Entity, CargoGasPalletComponent Component, TransformComponent PalletXform)> GetCargoGasPallets(EntityUid gridUid)
    {
        _gasPads.Clear();

        var query = AllEntityQuery<CargoGasPalletComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var comp, out var compXform))
        {
            if (compXform.ParentUid != gridUid ||
                !compXform.Anchored)
            {
                continue;
            }

            _gasPads.Add((uid, comp, compXform));

        }

        return _gasPads;
    }

    #endregion

    #region Station

    private bool SellPallets(EntityUid gridUid, EntityUid station, out HashSet<(EntityUid, OverrideSellComponent?, double)> goods, out HashSet<(EntityUid, double)> gasValues) //Starlight-edit - add gasValues
    {
        GetPalletGoods(gridUid, out var toSell, out goods);
        GetGasPalletStocks(gridUid, out var gasPallets, out gasValues); //Starlight

        if (toSell.Count == 0 && gasValues.Sum(t => t.Item2) < 1) //Starlight-edit - add gasValues
            return false;

        var ev = new EntitySoldEvent(toSell, station);
        RaiseLocalEvent(ref ev);

        foreach (var ent in toSell)
        {
            Del(ent);
        }

        // Starlight - start
        var gasSaleEvent = new EntitySellContentsEvent(gasPallets, station);
        RaiseLocalEvent(ref gasSaleEvent);
        // Starlight - end

        return true;
    }

    private void GetPalletGoods(EntityUid gridUid, out HashSet<EntityUid> toSell,  out HashSet<(EntityUid, OverrideSellComponent?, double)> goods)
    {
        goods = new HashSet<(EntityUid, OverrideSellComponent?, double)>();
        toSell = new HashSet<EntityUid>();

        foreach (var (palletUid, _, _) in GetCargoPallets(gridUid, BuySellType.Sell))
        {
            // Containers should already get the sell price of their children so can skip those.
            _setEnts.Clear();

            _lookup.GetEntitiesIntersecting(
                palletUid,
                _setEnts,
                LookupFlags.Dynamic | LookupFlags.Sundries);

            foreach (var ent in _setEnts)
            {
                // Dont sell:
                // - anything already being sold
                // - anything anchored (e.g. light fixtures)
                // - anything blacklisted (e.g. players).
                if (toSell.Contains(ent) ||
                    _xformQuery.TryGetComponent(ent, out var xform) &&
                    (xform.Anchored || !CanSell(ent, xform)))
                {
                    continue;
                }

                if (_blacklistQuery.HasComponent(ent))
                    continue;

                var price = _pricing.GetPrice(ent);
                if (price == 0)
                    continue;
                toSell.Add(ent);
                goods.Add((ent, CompOrNull<OverrideSellComponent>(ent), price));
            }
        }
    }

    /// <summary>
    /// Starlight
    /// Get the value of gasses held in the gas pallets on this grid.
    /// </summary>
    private void GetGasPalletStocks(EntityUid gridUid, out HashSet<CargoGasPalletComponent> gasPallets, out HashSet<(EntityUid, double)> gasValues)
    {
            gasPallets = new HashSet<CargoGasPalletComponent>();
            gasValues = new HashSet<(EntityUid, double)>();
            foreach (var (gasPalletUid, gasPalletComponent, _) in GetCargoGasPallets(gridUid))
            {
                    gasPallets.Add(gasPalletComponent);
                    gasValues.Add((gasPalletUid, _atmosphereSystem.GetPrice(gasPalletComponent.Air)));
            }
    }

    private bool CanSell(EntityUid uid, TransformComponent xform)
    {
        if (_mobQuery.HasComponent(uid))
        {
            return false;
        }

        var complete = IsBountyComplete(uid, out var bountyEntities);

        // Recursively check for mobs at any point.
        var children = xform.ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (complete && bountyEntities.Contains(child))
                continue;

            if (!CanSell(child, _xformQuery.GetComponent(child)))
                return false;
        }

        return true;
    }

    private void OnPalletSale(EntityUid uid, CargoPalletConsoleComponent component, CargoPalletSellMessage args)
    {
        var xform = Transform(uid);

        if (_station.GetOwningStation(uid) is not { } station ||
            !TryComp<StationBankAccountComponent>(station, out var bankAccount))
        {
            return;
        }

        if (xform.GridUid is not { } gridUid)
        {
            _uiSystem.SetUiState(uid,
                CargoPalletConsoleUiKey.Sale,
                new CargoPalletConsoleInterfaceState(0, 0, false));
            return;
        }

        if (!SellPallets(gridUid, station, out var goods, out var gasses)) //Starlight-edit - add gasses
            return;

        var baseDistribution = CreateAccountDistribution((station, bankAccount));
        foreach (var (_, sellComponent, value) in goods)
        {
            Dictionary<ProtoId<CargoAccountPrototype>, double> distribution;
            if (sellComponent != null)
            {
                var cut = _lockboxCutEnabled ? bankAccount.LockboxCut : bankAccount.PrimaryCut;
                distribution = new Dictionary<ProtoId<CargoAccountPrototype>, double>
                {
                    { sellComponent.OverrideAccount, cut },
                    { bankAccount.PrimaryAccount, 1.0 - cut },
                };
            }
            else
            {
                distribution = baseDistribution;
            }

            UpdateBankAccount((station, bankAccount), (int) Math.Round(value), distribution, false);
        }

        // Starlight - start
        foreach (var (_, value) in gasses)
        {
                // TODO: Consider if we want raw gasses to count as lockbox cut to engineering if lockbox cut is enabled
                UpdateBankAccount((station, bankAccount), (int) Math.Round(value), baseDistribution, false);
        }
        // Starlight - end

        Dirty(station, bankAccount);
        _audio.PlayPvs(ApproveSound, uid);
        UpdatePalletConsoleInterface(uid);
    }

    #endregion
}

/// <summary>
/// Event broadcast raised by-ref before it is sold and
/// deleted but after the price has been calculated.
/// </summary>
[ByRefEvent]
public readonly record struct EntitySoldEvent(HashSet<EntityUid> Sold, EntityUid Station);
/// <summary>
/// Starlight
/// Event broadcast raised by-ref before its contents are sold
/// but after the price has been calculated.
/// </summary>
[ByRefEvent]
public readonly record struct EntitySellContentsEvent(HashSet<CargoGasPalletComponent> Containers, EntityUid Station);
