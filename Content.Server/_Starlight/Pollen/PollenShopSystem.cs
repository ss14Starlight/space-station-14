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
using Robust.Shared.Timing;
using Content.Server.Stack;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using System.Linq;
using Content.Shared.StatusEffectNew;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Starlight.Pollen.System;

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
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private static readonly EntProtoId _hardenStatusEffect = "PollenTreeBarkT2PassiveHardenEffect";

    private static readonly EntProtoId _pollenShopAction = "ActionOpenPollenShop";

    private static readonly EntProtoId _woodPlankStack10 = "MaterialWoodPlank10";
    private const string HardenListingId = "PollenTreeBarkT2Harden";

    /// <summary>
    /// Listings that grant a periodic reagent drip when bought, keyed by
    /// listing id. Add an entry here for each new "drip" perk (e.g. Mushroom
    /// T3 Ultravasculine) instead of creating a new component/event per perk.
    /// </summary>
    private static readonly Dictionary<string, (ProtoId<ReagentPrototype> Reagent, float Amount, TimeSpan Interval)> _periodicReagentPerks = new()
    {
        ["PollenTreeBarkT3SapSerum"] = ("RobustHarvest", 1f, TimeSpan.FromSeconds(30)),
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PollenCollectorComponent, PollenCollectorInitializedEvent>(OnCollectorInitialized);
        SubscribeLocalEvent<StorePurchaseCompletedEvent>(OnPurchaseCompleted);
        SubscribeLocalEvent<PollenCollectorComponent, MakeWoodEvent>(OnMakeWood);
    }

    private void OnCollectorInitialized(Entity<PollenCollectorComponent> ent, ref PollenCollectorInitializedEvent args)
        => _actions.AddAction(ent.Owner, _pollenShopAction);

    private void OnPurchaseCompleted(ref StorePurchaseCompletedEvent args)
    {
        if (_periodicReagentPerks.TryGetValue(args.ListingId, out var config))
            {
                var perk = EnsureComp<PollenSerumPerkComponent>(args.Buyer);
                perk.Reagent = config.Reagent;
                perk.Amount = config.Amount;
                perk.Interval = config.Interval;
                perk.NextTick = _timing.CurTime + config.Interval;
                return;
            }

        if (args.ListingId == HardenListingId)
        {
            GrantHardenArmor(args.Buyer);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var perkQuery = EntityQueryEnumerator<PollenSerumPerkComponent>();
        while (perkQuery.MoveNext(out var uid, out var perk))
        {
            if (now < perk.NextTick)
                continue;

            perk.NextTick = now + perk.Interval;
            AddSerum(uid, perk.Reagent, perk.Amount);
        }
    }

    // Bark

    private void OnMakeWood(Entity<PollenCollectorComponent> ent, ref MakeWoodEvent args)
    {
        if (args.Handled)
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Slash", 25);
        _damageable.TryChangeDamage(ent.Owner, damage, ignoreResistances: true);

        var wood = Spawn(_woodPlankStack10, Transform(ent.Owner).Coordinates);
        _hands.PickupOrDrop(ent.Owner, wood);

        args.Handled = true;
    }

    private void GrantHardenArmor(EntityUid buyer)
        => _statusEffects.TrySetStatusEffectDuration(buyer, _hardenStatusEffect);

    private void AddSerum(EntityUid uid, ProtoId<ReagentPrototype> reagent, float amount)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream) ||
        _mobState.IsDead(uid))
        return;

        var solution = new Solution();
        solution.AddReagent(reagent, amount);
        _bloodstream.TryAddToBloodstream((uid, bloodstream), solution);
    }
}
