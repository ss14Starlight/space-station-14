using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared.EntityTable;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.StationRadio.Systems;

public sealed partial class UniqueDiscFillSystem : EntitySystem
{
    private const int DrawsPerDisc = 20;

    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EntityTableSystem _entityTable = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    [SubscribeLocalEvent]
    private void OnMapInit(EntityUid uid, UniqueDiscFillComponent comp, MapInitEvent args)
    {
        if (!_container.TryGetContainer(uid, comp.Container, out var target))
        {
            Log.Error($"{ToPrettyString(uid)} has no container {comp.Container} to fill with discs");
            return;
        }

        var wanted = _random.Next(comp.Min, comp.Max + 1);
        var table = _proto.Index(comp.Table);
        var picked = new List<EntProtoId>();
        for (var draw = 0; draw < wanted * DrawsPerDisc && picked.Count < wanted; draw++)
        {
            foreach (var disc in _entityTable.GetSpawns(table))
            {
                if (!picked.Contains(disc) && picked.Count < wanted)
                    picked.Add(disc);
            }
        }

        var coords = Transform(uid).Coordinates;
        foreach (var discId in picked)
        {
            var item = Spawn(discId, coords);
            if (comp.Wrapper is { } wrapperId)
            {
                var wrapper = Spawn(wrapperId, coords);
                if (TryComp<DiscCoverComponent>(wrapper, out var cover)
                    && _container.TryGetContainer(wrapper, cover.Slot, out var slot)
                    && _container.Insert(item, slot))
                {
                    item = wrapper;
                }
                else
                {
                    Del(wrapper);
                }
            }

            if (!_container.Insert(item, target))
                Del(item);
        }

        RemCompDeferred<UniqueDiscFillComponent>(uid);
    }
}
