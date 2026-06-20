using Content.Server.Atmos.EntitySystems;
using Content.Shared._Starlight.Weapons.Hitscan.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;

namespace Content.Server._Starlight.Weapons.Hitscan.Systems;

public sealed partial class HitscanIgniteEffectSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private FlammableSystem _flammableSystem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanIgniteEffectComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
    }

    private void OnHitscanHit(Entity<HitscanIgniteEffectComponent> hitscan, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity == null)
            return;

        if (TryComp<FlammableComponent>(args.Data.HitEntity.Value, out var flammable))
            _flammableSystem.SetFireStacks(args.Data.HitEntity.Value, flammable.FireStacks + (flammable.MinIgnitionTemperature / hitscan.Comp.Temperature), flammable, true);

        if (Transform(args.Data.HitEntity.Value) is TransformComponent xform && xform.GridUid is { } hitGridUid)
        {
            var position = _transform.GetGridOrMapTilePosition(args.Data.HitEntity.Value, xform);
            _atmosphere.HotspotExpose(hitGridUid, position, hitscan.Comp.Temperature, 50, args.Data.Shooter ?? args.Data.Gun, true);
        }
    }
}
