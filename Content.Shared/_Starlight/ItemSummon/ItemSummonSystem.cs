using Content.Shared.Actions.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._Starlight.ItemSummon;

public sealed partial class ItemSummonSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly SharedPvsOverrideSystem _pvs = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedProjectileSystem _projectile = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemSummonComponent, OnItemSummonActionEvent>(OnItemSummon);
        SubscribeLocalEvent<ItemSummonComponent, ComponentShutdown>(OnShutdown);
    }

    public void OnItemSummon(Entity<ItemSummonComponent> ent, ref OnItemSummonActionEvent args)
    {
        if (ent.Comp.SummonedItem is EntityUid itemUid && Exists(itemUid))
        {
            var coords = Transform(itemUid).Coordinates;
            _popup.PopupPredictedCoordinates(Loc.GetString("item-summon-action-vanish", ("item", Name(itemUid))), coords, args.Performer, PopupType.Small);

            if(TryComp<EmbeddableProjectileComponent>(itemUid, out var projectileComp))
                _projectile.EmbedDetach(itemUid, projectileComp, args.Performer);

            _hands.TryForcePickupAnyHand(args.Performer, itemUid);

            _popup.PopupPredicted(Loc.GetString("item-summon-action-recall", ("item", Name(itemUid))), args.Performer, args.Performer, PopupType.Small);
        }

        if(ent.Comp.SummonedItem == null || !Exists(ent.Comp.SummonedItem))
        {
            var uid = PredictedSpawnAtPosition(ent.Comp.SummonableItem, Transform(args.Performer).Coordinates);
            _hands.TryForcePickupAnyHand(args.Performer, uid);

            ent.Comp.SummonedItem = uid;
            Dirty(ent);

            _popup.PopupPredicted(Loc.GetString("item-summon-action-recall", ("item", Name(uid))), args.Performer, args.Performer, PopupType.Medium);
        }

        args.Handled = true;
    }

    private void OnShutdown(Entity<ItemSummonComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.SummonedItem is EntityUid uid)
        {
            if (TryComp<ActionComponent>(ent.Owner, out var actionComp)
            && actionComp.AttachedEntity is EntityUid actionOwner
            && _player.TryGetSessionByEntity(actionOwner, out var session))
            {
                _pvs.RemoveSessionOverride(uid, session);
            }

            _popup.PopupEntity(Loc.GetString("item-summon-action-recall", ("item", Name(uid))), uid);
            QueueDel(ent.Comp.SummonedItem);
        }
    }
}
