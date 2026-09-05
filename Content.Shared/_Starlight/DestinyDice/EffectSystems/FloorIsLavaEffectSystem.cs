using System.Linq;
using Content.Shared._Starlight.DestinyDice.Effects;
using Content.Shared.EntityEffects;
using Content.Shared.Maps;
using Robust.Shared.Map.Components;

namespace Content.Shared._Starlight.DestinyDice.EffectSystems;

public sealed partial class FloorIsLavaEffectSystem : EntityEffectSystem<DestinyDiceComponent, FloorIsLavaEffect>
{
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedMapSystem _map = default!;

    private const string LavaPrototypeId = "FloorLavaEntity";

    protected override void Effect(Entity<DestinyDiceComponent> entity, ref EntityEffectEvent<FloorIsLavaEffect> args)
    {
        var uid = entity.Comp.ActiveGrid;
        if (uid is null) return;

        if (!TryComp<MapGridComponent>(uid, out var grid))
            return;

        var tiles = _map.GetAllTiles(uid.Value, grid);
        foreach (var coords in tiles.Select(tile => _turf.GetTileCenter(tile)))
            PredictedSpawnAtPosition(LavaPrototypeId, coords);
    }
}
