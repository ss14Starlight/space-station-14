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
        //var totalCount = EntityQuery<HellportalMobComponent>().Count(); // Starlight
        var countedMobs = false;  // Starlight

        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (_time.CurTime < comp.NextSpawn)
                continue;

            comp.NextSpawn = _time.CurTime + comp.SpawnCooldown;

            #region Starlight
            // Count once when a wave is due, rather than scanning all mobs every frame.
            if (!countedMobs)
            {
                CountSpawnedMobs();
                countedMobs = true;
            }

            var count = _mobCounts.GetValueOrDefault(uid);
            var maxSpawns = comp.MobsPerPlayer is { } ratio
                ? (int)Math.Ceiling(_playerManager.PlayerCount * Math.Max(0f, ratio))
                : comp.MaxSpawns;

            if (count >= maxSpawns)
                continue;
            #endregion

            _audio.PlayPvs(comp.Sound, xform.Coordinates);
            foreach (var proto in _entityTable.GetSpawns(comp.BasicSpawnTable))
            #region Starlight
            {
                if (count >= maxSpawns)
                    break;

                var spawned = Spawn(proto, xform.Coordinates);
                EnsureComp<HellportalMobComponent>(spawned).SourcePortal = uid;
                count++;
            }

            _mobCounts[uid] = count;
            #endregion
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
