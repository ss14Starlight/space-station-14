using Content.Server.Store.Systems;
using Content.Server.StoreDiscount.Systems;
using Content.Server.Traitor.Uplink;
using Content.Server._Starlight.Objectives.Components;
using Content.Shared._Starlight.Mind.Events;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Objectives.Systems;

/// <summary>
/// Pays additional TC after receiving objectives that grant additional TC (aka DAGD).
/// </summary>
public sealed partial class ObjectiveBonusSystem : EntitySystem
{
    [Dependency] private StoreSystem _store = default!;

    private readonly Queue<EntityUid> _pending = new();
    private readonly HashSet<EntityUid> _queued = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ObjectiveBonusRecipientComponent, MindObjectivesChangedEvent>(OnObjectivesChanged);
        SubscribeLocalEvent<StoreInitializedEvent>(OnStoreInitialized);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnObjectivesChanged(Entity<ObjectiveBonusRecipientComponent> ent, ref MindObjectivesChangedEvent args)
    {
        if (TryComp<MindComponent>(ent, out var mind))
            QueueRewards((ent.Owner, mind), ent.Comp);
    }

    private void OnStoreInitialized(ref StoreInitializedEvent args)
    {
        if (!TryComp<StoreComponent>(args.Store, out var store) ||
            !store.CurrencyWhitelist.Contains(UplinkSystem.TelecrystalCurrencyPrototype) ||
            store.AccountOwner != args.TargetUser ||
            !TryComp<MindComponent>(args.TargetUser, out var mind))
            return;

        // Remember the account independently of its access device and the mind's current body.
        var recipient = EnsureComp<ObjectiveBonusRecipientComponent>(args.TargetUser);
        recipient.Uplink = args.Store;

        // Clear the previous payment record so assigned objectives can grant their bonuses to the newly initialized balance.
        if (args.BalanceInitialized)
            recipient.RewardedObjectives.Clear();
        QueueRewards((args.TargetUser, mind), recipient);
    }

    private void QueueRewards(Entity<MindComponent> mind, ObjectiveBonusRecipientComponent recipient)
    {
        foreach (var objective in mind.Comp.Objectives)
        {
            if (!TryComp<ObjectiveBonusComponent>(objective, out var reward) ||
                reward.Telecrystals <= 0 || recipient.RewardedObjectives.Contains(objective))
                continue;

            if (_queued.Add(mind.Owner))
                _pending.Enqueue(mind.Owner);
            return;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Defer until assignment finishes, just incase you are invalid for it.
        var count = _pending.Count;
        while (count-- > 0 && _pending.TryDequeue(out var uid))
        {
            _queued.Remove(uid);
            if (!TryComp<MindComponent>(uid, out var mind) ||
                !TryComp<ObjectiveBonusRecipientComponent>(uid, out var recipient) ||
                recipient.Uplink is not { } uplink ||
                !TryComp<StoreComponent>(uplink, out var store) || store.AccountOwner != uid)
                continue;

            var rewards = new List<EntityUid>();
            var seen = new HashSet<EntityUid>();
            var total = FixedPoint2.Zero;
            foreach (var objective in mind.Objectives)
            {
                if (!seen.Add(objective) || !TryComp<ObjectiveBonusComponent>(objective, out var reward) ||
                    reward.Telecrystals <= 0 || recipient.RewardedObjectives.Contains(objective))
                    continue;

                rewards.Add(objective);
                total += reward.Telecrystals;
            }

            // Combine rewards into one balance/update.
            if (rewards.Count == 0 || !_store.TryAddCurrency(
                    new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2>
                    {
                        [UplinkSystem.TelecrystalCurrencyPrototype] = total,
                    }, uplink, store))
                continue;

            recipient.RewardedObjectives.UnionWith(rewards);
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _pending.Clear();
        _queued.Clear();
    }
}
