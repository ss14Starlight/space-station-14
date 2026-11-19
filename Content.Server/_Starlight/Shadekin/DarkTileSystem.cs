using Content.Server.Spreader;
using Content.Shared._Starlight.Shadekin;
using Content.Shared._Starlight.Shadekin.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Shadekin;

public sealed partial class DarkTileSystem : EntitySystem
{
    [Dependency] private ShadekinSystem _shadekin = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;

    [SubscribeLocalEvent]
    private void OnSpread(Entity<DarkTileComponent> ent, ref SpreadNeighborsEvent args)
    {
        if (Prototype(ent) is not { } prototype)
        {
            RemCompDeferred<ActiveEdgeSpreaderComponent>(ent);
            return;
        }

        if (args.NeighborFreeTiles.Count == 0)
            return;

        // DarkTiles never spread in the DarkMap.
        if (_shadekin.AreWeInTheDark(ent))
        {
            RemCompDeferred<ActiveEdgeSpreaderComponent>(ent);
            return;
        }

        // Its gona die anyway so we need to stop the spread!
        if (_shadekin.GetLightExposure(ent, ent.Comp.Threshold) >= ent.Comp.Threshold)
        {
            RemCompDeferred<ActiveEdgeSpreaderComponent>(ent);
            QueueDel(ent);
            return;
        }

        foreach (var neighbor in args.NeighborFreeTiles)
        {
            var coordinates = _map.GridTileToLocal(neighbor.Tile.GridUid, neighbor.Grid, neighbor.Tile.GridIndices);

            if (_shadekin.GetLightExposure(_transform.ToMapCoordinates(coordinates), ent.Comp.Threshold) >= ent.Comp.Threshold)
                continue;

            SpawnAtPosition(prototype.ID, coordinates);
            args.Updates--;

            if (args.Updates <= 0)
                return;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        var query = EntityQueryEnumerator<DarkTileComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (curTime < component.NextUpdate)
                continue;

            if (component.NextUpdate == TimeSpan.Zero)
            {
                component.NextUpdate = curTime + (component.UpdateCooldown * _random.NextDouble());
                continue;
            }

            component.NextUpdate = curTime + component.UpdateCooldown;

            if (!HasComp<ActiveEdgeSpreaderComponent>(uid) || _shadekin.AreWeInTheDark(uid))
                continue;

            if (_shadekin.GetLightExposure(uid, component.Threshold) >= component.Threshold)
                QueueDel(uid);
        }
    }
}
