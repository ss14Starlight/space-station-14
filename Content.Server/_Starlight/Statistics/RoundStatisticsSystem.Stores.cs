using Content.Shared.GameTicking;
using Content.Shared.Store.Components;

namespace Content.Server._Starlight.Statistics;

public sealed partial class RoundStatisticsSystem
{
    private readonly Dictionary<StorePurchaseKey, int> _storePurchases = [];

    private void InitializeStoreStatistics()
        => RegisterStatisticsDomain(EmitStoreStatistics, ClearStoreStatistics);

    /// <summary>
    /// Records a completed store purchase without retaining purchaser information.
    /// </summary>
    public void RecordStorePurchase(string storeId, string itemId, bool discounted)
    {
        if (!EnsureRound())
            return;

        Increment(_storePurchases, new StorePurchaseKey(storeId, itemId, discounted));
    }

    /// <summary>
    /// Emits what each store was given, spent and never got round to spending. Walking the stores
    /// at round end rather than on purchase is what keeps a refund from still counting as spent.
    /// </summary>
    private void EmitStoreStatistics(RoundEndMessageEvent args)
    {
        var roundId = args.RoundId;

        foreach (var (key, count) in _storePurchases)
        {
            EmitRoundRecord(
                roundId,
                "kind=store_purchase store_id={StoreId} item_id={ItemId} discounted={Discounted} count={Count}",
                key.Store,
                key.Item,
                key.Discounted,
                count);
        }

        var remaining = new Dictionary<StoreCurrencyKey, double>();
        var spent = new Dictionary<StoreCurrencyKey, double>();
        var stores = new Dictionary<string, int>();

        var query = EntityQueryEnumerator<StoreComponent>();
        while (query.MoveNext(out _, out var store))
        {
            var storeId = store.Name.Id;
            Increment(stores, storeId);

            foreach (var (currency, amount) in store.Balance)
            {
                Add(remaining, new StoreCurrencyKey(storeId, currency.Id), amount.Double());
            }

            foreach (var (currency, amount) in store.BalanceSpent)
            {
                Add(spent, new StoreCurrencyKey(storeId, currency.Id), amount.Double());
            }
        }

        foreach (var key in remaining.Keys)
        {
            spent.TryAdd(key, 0d);
        }

        foreach (var (key, amount) in spent)
        {
            EmitRoundRecord(
                roundId,
                "kind=store_currency store_id={StoreId} currency_id={CurrencyId} spent={Spent} " +
                "remaining={Remaining} stores={Stores}",
                key.Store,
                key.Currency,
                Number(amount),
                Number(remaining.GetValueOrDefault(key)),
                stores.GetValueOrDefault(key.Store));
        }
    }

    private void ClearStoreStatistics()
        => _storePurchases.Clear();

    private readonly record struct StorePurchaseKey(string Store, string Item, bool Discounted);
    private readonly record struct StoreCurrencyKey(string Store, string Currency);
}
