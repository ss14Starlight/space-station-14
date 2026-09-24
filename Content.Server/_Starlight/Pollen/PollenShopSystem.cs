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
using Content.Shared.Trigger.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Popups;

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
    [Dependency] private TriggerSystem _trigger = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly EntProtoId _sporeCloudEmitter = "PollenSporeCloudEmitter";
    private static readonly EntProtoId _hardenStatusEffect = "PollenTreeBarkT2PassiveHardenEffect";
    private static readonly EntProtoId _pollenShopAction = "ActionOpenPollenShop";
    private static readonly EntProtoId _woodPlankStack10 = "MaterialWoodPlank10";
    private const string HardenListingId = "PollenTreeBarkT2Harden";
    private static readonly ProtoId<ReagentPrototype> _phytovitalin = "Phytovitalin";

    /// <summary>
    /// Listings that grant a periodic reagent drip when bought, keyed by
    /// listing id. Add an entry here for each new "drip" perk (e.g. Mushroom
    /// T3 Ultravasculine) instead of creating a new component/event per perk.
    /// </summary>
    private static readonly Dictionary<string, (ProtoId<ReagentPrototype> Reagent, float Amount, TimeSpan Interval)> _periodicReagentPerks = new()
    {
        ["PollenTreeBarkT3SapSerum"] = (_phytovitalin, 1f, TimeSpan.FromSeconds(30)),
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PollenCollectorComponent, PollenCollectorInitializedEvent>(OnCollectorInitialized);
        SubscribeLocalEvent<StorePurchaseCompletedEvent>(OnPurchaseCompleted);
        SubscribeLocalEvent<PollenCollectorComponent, MakeWoodEvent>(OnMakeWood);
        SubscribeLocalEvent<PollenCollectorComponent, PollenSporeCloudEvent>(OnSporeCloud);
        SubscribeLocalEvent<PollenCollectorComponent, PollenInjectPhytovitalinEvent>(OnInjectPhytovitalin);
        SubscribeLocalEvent<PollenCollectorComponent, PollenInjectPhytovitalinDoAfterEvent>(OnInjectPhytovitalinDoAfter);
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

#region Bark

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
#endregion Bark

#region Mushroom
    // T1
    private void OnInjectPhytovitalin(Entity<PollenCollectorComponent> ent, ref PollenInjectPhytovitalinEvent args)
    {
        if (args.Handled)
            return;

        var netAction = GetNetEntity(args.Action);

        var doAfterArgs = new DoAfterArgs(EntityManager, ent.Owner, TimeSpan.FromSeconds(10),
            new PollenInjectPhytovitalinDoAfterEvent(netAction), ent.Owner, target: args.Target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    private void OnInjectPhytovitalinDoAfter(Entity<PollenCollectorComponent> ent, ref PollenInjectPhytovitalinDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var actionUid = GetEntity(args.Action);

        if (args.Cancelled || args.Args.Target is not { } target)
        {
            _actions.ClearCooldown(actionUid);
            return;
        }

        var solution = new Solution();
        solution.AddReagent(_phytovitalin, 5f);

        var injected = false;

        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            injected = _bloodstream.TryAddToBloodstream((target, bloodstream), solution);
        }
        else if (_solutionContainer.TryGetInjectableSolution(target, out var injectable, out _))
        {
            _solutionContainer.Inject(target, injectable.Value, solution);
            injected = true;
        }
        else if (_solutionContainer.TryGetRefillableSolution(target, out var refillable, out _))
        {
            _solutionContainer.Refill(target, refillable.Value, solution);
            injected = true;
        }

        if (!injected)
        {
            _popup.PopupEntity(Loc.GetString("pollen-inject-invalid-target"), ent.Owner, ent.Owner);
            _actions.ClearCooldown(actionUid);
            return;
        }

        var selfDamage = new DamageSpecifier();
        selfDamage.DamageDict.Add("Cellular", 5);
        selfDamage.DamageDict.Add("Bloodloss", 20);
        _damageable.TryChangeDamage(ent.Owner, selfDamage, ignoreResistances: true);
    }
    // T2

    // T3
    private void OnSporeCloud(Entity<PollenCollectorComponent> ent, ref PollenSporeCloudEvent args)
    {
        if (args.Handled)
            return;

        var emitter = Spawn(_sporeCloudEmitter, Transform(ent.Owner).Coordinates);
        _trigger.Trigger(emitter, ent.Owner);

        args.Handled = true;
    }
#endregion Mushroom
}
