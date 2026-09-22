using Content.Server._Starlight.Medical.Body.Systems;
using Content.Server._Starlight.Pollen.Components;
using Content.Server.Actions;
using Content.Shared._Starlight.Pollen;
using Content.Shared._Starlight.Pollen.Components;
using Content.Shared._Starlight.Store.Events;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Pollen.Systems;

/// <summary>
/// Owns the pollen perk shop: grants Dionas the action to open it, and runs
/// the effects of whatever perks they've bought. Collection and currency
/// granting live in PollenCollectorSystem - this system only cares about
/// what happens once points are spent.
/// </summary>
public sealed partial class PollenShopSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;

    private static readonly EntProtoId _pollenShopAction = "ActionOpenPollenShop";

    /// <summary>Must match the `id:` of the serum-drip listing in pollen_catalog.yml.</summary>
    private const string SerumPerkListingId = "PollenPerkSerumDrip";

        public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PollenCollectorComponent, PollenCollectorInitializedEvent>(OnCollectorInitialized);
        SubscribeLocalEvent<StorePurchaseCompletedEvent>(OnPurchaseCompleted);
    }

    private void OnCollectorInitialized(Entity<PollenCollectorComponent> ent, ref PollenCollectorInitializedEvent args)
    {
        _actions.AddAction(ent.Owner, _pollenShopAction);
    }

    private void OnPurchaseCompleted(ref StorePurchaseCompletedEvent args)
    {
        if (args.ListingId != SerumPerkListingId)
            return;

        EnsureComp<PollenSerumPerkComponent>(args.Buyer);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var perkQuery = EntityQueryEnumerator<PollenSerumPerkComponent>();
        while (perkQuery.MoveNext(out var uid, out var perk))
        {
            perk.Accumulator += frameTime;
            if (perk.Accumulator < 1f)
                continue;

            perk.Accumulator -= 1f;
            AddSerum(uid, perk.Reagent, perk.AmountPerSecond);
        }
    }

    private void AddSerum(EntityUid uid, ProtoId<ReagentPrototype> reagent, float amount)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return;

        var solution = new Solution();
        solution.AddReagent(reagent, amount);
        _bloodstream.TryAddToBloodstream((uid, bloodstream), solution);
    }
}