using System.Linq;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared._Starlight.NullSpace.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server._Starlight.CosmicCult.Abilities;

public sealed partial class CosmicTransmuteSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private EntityWhitelistSystem _entityWhitelist = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicGlyphTransmuteComponent, TryActivateGlyphEvent>(OnTransmuteGlyph);
    }

    private void OnTransmuteGlyph(Entity<CosmicGlyphTransmuteComponent> uid, ref TryActivateGlyphEvent args)
    {
        foreach (var entity in _lookup.GetEntitiesIntersecting(Transform(uid).Coordinates))
            if (HasComp<NullSpaceBlockerComponent>(entity))
            {
                _popup.PopupEntity(Loc.GetString("cosmicability-generic-fail"), uid, args.User);
                args.Cancel();
                return;
            }

        var tgtpos = Transform(uid).Coordinates;
        GatherEntities(uid, args.User, out var held, out var loose);
        if (held.Count == 0 && loose.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-glyph-conditions-not-met"), uid, args.User);
            args.Cancel();
            return;
        }

        // Prioritize items held by the player activating the glyph over items on the glyph itself.
        var pool = held.Count > 0 ? held : loose;
        var target = pool[_random.Next(pool.Count)];

        string? targetHand = null;
        if (held.Count > 0)
            _hands.IsHolding((args.User, null), target, out targetHand);

        var spawned = Spawn(_random.Pick(uid.Comp.Transmutations), tgtpos);
        if (targetHand != null)
            _hands.TryForcePickup((args.User, null), spawned, targetHand, checkActionBlocker: false);
        QueueDel(target);
    }

    /// <summary>
    ///     Gets all valid transmutation targets near a glyph. Only items held
    ///     by the player triggering the glyph or on the glyph itself are considered.
    /// </summary>
    private void GatherEntities(Entity<CosmicGlyphTransmuteComponent> ent, EntityUid user, out List<EntityUid> held, out List<EntityUid> loose)
    {
        held = [];
        loose = [];

        var entities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.TransmuteRange, entities);
        foreach (var entity in entities)
        {
            if (!IsValidTarget(ent, entity, user))
                continue;

            if (_container.IsEntityInContainer(entity))
                held.Add(entity);
            else
                loose.Add(entity);
        }

        if (TryComp<HandsComponent>(user, out var handsComp))
        {
            foreach (var hand in _hands.EnumerateHands((user, handsComp)))
            {
                if (_hands.TryGetHeldItem((user, handsComp), hand, out var heldItem)
                    && IsValidTarget(ent, heldItem.Value, user)
                    && !held.Contains(heldItem.Value))
                    held.Add(heldItem.Value);
            }
        }
    }

    private bool IsValidTarget(Entity<CosmicGlyphTransmuteComponent> ent, EntityUid target, EntityUid user)
    {
        if (!_entityWhitelist.IsValid(ent.Comp.Whitelist, target))
            return false;

        // Cult gear shouldn't block conversion, virtual hands shouldn't be converted.
        if (HasComp<CosmicEquipmentComponent>(target) || HasComp<VirtualItemComponent>(target))
            return false;

        if (!_container.IsEntityInContainer(target))
            return true;

        return _container.TryGetContainingContainer(target, out var container)
            && container.Owner == user
            && _hands.IsHolding((container.Owner, null), target);
    }
}
