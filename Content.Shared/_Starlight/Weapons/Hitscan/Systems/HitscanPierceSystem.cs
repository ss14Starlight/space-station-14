using Content.Shared.Actions;
using Content.Shared.Armor;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Content.Shared.Starlight.Antags.Abductor;
using Content.Shared.Starlight.Medical.Surgery;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared._Starlight.Combat.Ranged.Pierce;
using Content.Shared._Starlight.Weapon;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Combat.Ranged;

public sealed partial class PierceSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _rand = default!;

    private EntityQuery<HitscanReflectComponent> _reflectQuery;

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

        if (!ev.Pierced)
            return;

        reflect.CurrentReflections++;

        var fromEffect = Transform(data.HitEntity.Value).Coordinates;

        // Give it a little bit of swim
        var random = _rand.NextFloat(-hitscan.Comp.Deviation, hitscan.Comp.Deviation);

        var hitFiredEvent = new HitscanTraceEvent
        {
            FromCoordinates = fromEffect,
            ShotDirection = (data.ShotDirection.ToAngle() + random).ToVec(),
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
