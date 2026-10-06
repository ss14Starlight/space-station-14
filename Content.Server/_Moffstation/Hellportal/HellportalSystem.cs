using System.Linq;
using Content.Server._Moffstation.Hellportal.Components;
using Content.Shared.EntityTable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Moffstation.Hellportal;

public sealed partial class HellportalSystem : EntitySystem
{
    [Dependency] private EntityTableSystem _entityTable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _time = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HellportalComponent, TransformComponent>();
        var totalCount = EntityQuery<HellportalMobComponent>().Count();

        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (_time.CurTime < comp.NextSpawn)
                continue;

            comp.NextSpawn = _time.CurTime + comp.SpawnCooldown;

            if (totalCount >= comp.MaxSpawns)
                continue;

            _audio.PlayPvs(comp.Sound, xform.Coordinates);
            foreach (var proto in _entityTable.GetSpawns(comp.BasicSpawnTable))
                Spawn(proto, xform.Coordinates);
        }
    }

    [SubscribeLocalEvent]
    private void OnAnchorChange(Entity<HellportalComponent> entity, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
        {
            QueueDel(entity);
        }
    }
}
