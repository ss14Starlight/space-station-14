using System.Numerics;
using Content.Shared.Inventory;
using Content.Shared.Random.Helpers;
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

    /// <summary>
    /// A salt value used to generate a new seed for piercing events. This ensures that the random number generation for piercing is consistent and unique for each event, preventing predictable outcomes.
    /// </summary>
    private const int PierceSalt = -7;

    public override void Initialize()
    {
        _reflectQuery = GetEntityQuery<HitscanReflectComponent>();
        base.Initialize();
    }

    [SubscribeLocalEvent]
    private void OnHitscanHit(Entity<HitscanPierceComponent> hitscan, ref HitscanRaycastFiredEvent args)
    {
        var data = args.Data;

        if (data.HitEntity == null
            || !TryPierce(hitscan, data.HitEntity.Value, data.ShotDirection, data.PredictionSeed, out var dir)
            || !_reflectQuery.TryComp(hitscan.Owner, out var reflect))
            return;

        reflect.CurrentReflections++;

        var fromEffect = Transform(data.HitEntity.Value).Coordinates;
        if (Transform(data.HitEntity.Value).MapUid is { } hitMap && data.HitPosition is { } hitPosition)
            fromEffect = new EntityCoordinates(hitMap, hitPosition);

        var hitFiredEvent = new HitscanTraceEvent
        {
            FromCoordinates = fromEffect,
            ToCoordinates = fromEffect.Offset(dir),
            ShotDirection = dir,
            Gun = data.Gun,
            Shooter = data.HitEntity.Value,
            OutputTrace = data.OutputTrace,
            PredictionSeed = GetNextSeed(data.PredictionSeed, reflect.CurrentReflections),
        };

        RaiseLocalEvent(hitscan, ref hitFiredEvent);
    }

    public static int? GetNextSeed(int? seed, int depth)
        => seed is { } value ? SharedRandomExtensions.HashCodeCombine(value, depth, PierceSalt) : null;

    public bool TryPierce(Entity<HitscanPierceComponent> hitscan, EntityUid hitEntity, Vector2 shotDirection, int? seed, out Vector2 direction)
    {
        direction = default;

        if (hitscan.Comp.Chance <= 0)
            return false;

        var random = seed is { } value ? new System.Random(SharedRandomExtensions.HashCodeCombine(value, PierceSalt)) : null;

        if (hitscan.Comp.Chance < 1 && !(random?.Prob(hitscan.Comp.Chance) ?? _rand.Prob(hitscan.Comp.Chance)))
            return false;

        // If we're at our maximum recursion depth, don't try to pierce
        if (!_reflectQuery.TryComp(hitscan.Owner, out var reflect) || reflect.CurrentReflections > reflect.MaxReflections)
            return false;

        var ev = new HitScanPierceAttemptEvent(hitscan.Comp.PierceLevel, true);
        RaiseLocalEvent(hitEntity, ref ev);

        //Check to see if a hand held shield is equipped to block piercing
        if (ev.Pierced) //If the bullet still piercing the entity, check to see if anything in hand will block the bullet from piercing. If armor has already blocked the bullet, no need to check for a shield in hand.
            foreach (var held in _handsSystem.EnumerateHeld(hitEntity)) //check each hand slot
            {
                if (!_tag.HasTag(held, _shieldTag) //Check if the item can be used as a shield, a hand held hardsuit isn't a shield.
                    || !TryComp<PierceableComponent>(held, out var pierceable) || pierceable.Level <= hitscan.Comp.PierceLevel //Check to see if the shield has the stopping power
                    || (TryComp<ItemToggleComponent>(held, out var itemToggle) && !itemToggle.Activated)) //If the shield has a toggle comp, it needs to be toggled on to be of use
                    continue;
                ev.Pierced = false;
                break; //Once we know the bullet is being stopped by something, no need to check other hand slots
            }

        if (!ev.Pierced)
            return false;

        // Give it a little bit of swim
        var swim = random != null
            ? (float) ((random.NextDouble() * 2) - 1) * hitscan.Comp.Deviation
            : _rand.NextFloat(-hitscan.Comp.Deviation, hitscan.Comp.Deviation);

        direction = (shotDirection.ToAngle() + swim).ToVec();
        return true;
    }

    [SubscribeLocalEvent]
    private void OnArmorPierce(Entity<PierceableComponent> ent, ref InventoryRelayedEvent<HitScanPierceAttemptEvent> args)
    {
        if ((byte)ent.Comp.Level > (byte)args.Args.Level)
            args.Args.Pierced = false;
    }

    [SubscribeLocalEvent]
    private void OnPierceablePierce(Entity<PierceableComponent> ent, ref HitScanPierceAttemptEvent args)
    {
        if ((byte)ent.Comp.Level > (byte)args.Level)
            args.Pierced = false;
    }
}
