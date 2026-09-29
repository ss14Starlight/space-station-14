using Content.Server._Funkystation.ReagentFires.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._Funkystation.ReagentFires.Systems;

public sealed partial class ReagentFireSystem : EntitySystem
{
    private readonly HashSet<EntityUid> _burningFires = [];
    private readonly List<EntityUid> _dueFires = [];
    private float _updateAccumulator;

    private void OnFireStartup(EntityUid uid, ReagentPuddleFireComponent component, ref ComponentStartup args)
    {
        if (component.OnFire)
            _burningFires.Add(uid);
    }

    private bool HasAdjacentBurningPuddle(EntityUid gridUid, MapGridComponent grid, Vector2i tilePos)
    {
        foreach (var offset in _cardinalOffsets)
        {
            var anchored = _map.GetAnchoredEntities(gridUid, grid, tilePos + offset);
            while (anchored.MoveNext(out var ent))
            {
                if (_fireQuery.TryComp(ent, out var fire) && fire.OnFire)
                    return true;
            }
        }

        return false;
    }

    private void SpreadToAdjacentPuddles(EntityUid gridUid, Vector2i tilePos)
    {
        if (!_gridQuery.TryComp(gridUid, out var grid))
            return;

        _spreadPuddles.Clear();

        foreach (var offset in _cardinalOffsets)
            CollectIgnitablePuddles(gridUid, grid, tilePos + offset, null, _spreadPuddles);

        foreach (var adjPuddle in _spreadPuddles)
            Ignite(adjPuddle, adjPuddle.Comp);
    }
}
