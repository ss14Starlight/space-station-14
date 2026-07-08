using Content.Shared.Inventory;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared._Starlight.Combat.Ranged.Pierce;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Content.Shared._Starlight.Weapons.Hitscan.Components;
using Content.Shared._Starlight.Weapons.Hitscan.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Weapons.Hitscan.Systems;

public sealed partial class PierceSystem : EntitySystem
{
    [Dependency] private IRobustRandom _rand = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private TagSystem _tag = default!;

    private EntityQuery<HitscanReflectComponent> _reflectQuery;
    private static readonly ProtoId<TagPrototype> _shieldTag = "Shield";

    public override void Initialize()
    {
        _reflectQuery = GetEntityQuery<HitscanReflectComponent>();

        SubscribeLocalEvent<HitscanPierceComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
        SubscribeLocalEvent<PierceableComponent, HitScanPierceAttemptEvent>(OnPierceablePierce);
        SubscribeLocalEvent<PierceableComponent, InventoryRelayedEvent<HitScanPierceAttemptEvent>>(OnArmorPierce);
        base.Initialize();
    }

    private void OnHitscanHit(Entity<HitscanPierceComponent> hitscan, ref HitscanRaycastFiredEvent args)
    {
        var data = args.Data;

        if (hitscan.Comp.Chance <= 0 || data.HitEntity == null)
            return;

        if (hitscan.Comp.Chance < 1 && !_rand.Prob(hitscan.Comp.Chance))
            return;

        // If we're at our maximum recursion depth, don't try to pierce
        if (!_reflectQuery.TryComp(hitscan.Owner, out var reflect) || reflect.CurrentReflections > reflect.MaxReflections)
            return;

        var ev = new HitScanPierceAttemptEvent(hitscan.Comp.PierceLevel, true);
        RaiseLocalEvent(data.HitEntity.Value, ref ev);

        //Check to see if a hand held shield is equipped to block piercing
        if (ev.Pierced) //If the bullet still piercing the entity, check to see if anything in hand will block the bullet from piercing. If armor has already blocked the bullet, no need to check for a shield in hand.
            foreach (var held in _handsSystem.EnumerateHeld(data.HitEntity.Value)) //check each hand slot
            {
                if (!_tag.HasTag(held, _shieldTag) //Check if the item can be used as a shield, a hand held hardsuit isn't a shield.
                    || !TryComp<PierceableComponent>(held, out var pierceable) || pierceable.Level <= hitscan.Comp.PierceLevel //Check to see if the shield has the stopping power
                    || (TryComp<ItemToggleComponent>(held, out var itemToggle) && !itemToggle.Activated)) //If the shield has a toggle comp, it needs to be toggled on to be of use
                    continue;
                ev.Pierced = false;
                break; //Once we know the bullet is being stopped by something, no need to check other hand slots
            }

        if (!ev.Pierced)
            return;

        reflect.CurrentReflections++;

        var fromEffect = Transform(data.HitEntity.Value).Coordinates;
        if (Transform(data.HitEntity.Value).MapUid is { } hitMap && data.HitPosition is { } hitPosition)
            fromEffect = new EntityCoordinates(hitMap, hitPosition);

        // Give it a little bit of swim
        var random = _rand.NextFloat(-hitscan.Comp.Deviation, hitscan.Comp.Deviation);

        var dir = (data.ShotDirection.ToAngle() + random).ToVec(); // Starlight-edit

        var hitFiredEvent = new HitscanTraceEvent
        {
            FromCoordinates = fromEffect,
            ToCoordinates = fromEffect.Offset(dir), // Starlight-edit
            ShotDirection = dir, // Starlight-edit
            Gun = data.Gun,
            Shooter = data.HitEntity.Value,
            OutputTrace = data.OutputTrace,
        };

        RaiseLocalEvent(hitscan, ref hitFiredEvent);
    }

    private void OnArmorPierce(Entity<PierceableComponent> ent, ref InventoryRelayedEvent<HitScanPierceAttemptEvent> args)
    {
        if ((byte)ent.Comp.Level > (byte)args.Args.Level)
            args.Args.Pierced = false;
    }

    private void OnPierceablePierce(Entity<PierceableComponent> ent, ref HitScanPierceAttemptEvent args)
    {
        if ((byte)ent.Comp.Level > (byte)args.Level)
            args.Pierced = false;
    }
}
