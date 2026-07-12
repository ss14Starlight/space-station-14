using Content.Shared._Starlight.Spawners.Components;
using Content.Shared._Starlight.Abstract.Extensions;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Shared._Starlight.Spawners.EntitySystems;

public sealed partial class SharedTimedSpawnerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly List<(EntityUid uid, TimedSpawnerComponent comp)> _toFire = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TimedSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _toFire.Clear();

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<TimedSpawnerComponent>();
        while (query.MoveNext(out var uid, out var timedSpawner))
        {
            if (timedSpawner.NextFire > curTime)
                continue;

            // Advance NextFire to catch up, but add only once
            while (timedSpawner.NextFire <= curTime)
                timedSpawner.NextFire += timedSpawner.IntervalSeconds;

            _toFire.Add((uid, timedSpawner));
        }

        foreach (var (uid, comp) in _toFire)
            OnTimerFired(uid, comp);
    }

    private void OnMapInit(Entity<TimedSpawnerComponent> ent, ref MapInitEvent args)
        => ent.Comp.NextFire = _timing.CurTime + ent.Comp.IntervalSeconds;

    private void OnTimerFired(EntityUid uid, TimedSpawnerComponent component)
    {
        var random = RandomPredicted.GetPredictedRandom(_random, _timing, GetNetEntity(uid).Id);

        if ((component.RequiredState != MobState.Invalid && (!TryComp<MobStateComponent>(uid, out var stateComp) || stateComp.CurrentState != component.RequiredState))
            || !random.Prob(component.Chance))
            return;

        var number = random.Next(component.MinimumEntitiesSpawned, component.MaximumEntitiesSpawned);
        var coordinates = Transform(uid).Coordinates;

        if (component.Prototypes.Count == 0)
            return;

        for (var i = 0; i < number; i++)
        {
            var entity = random.Pick(component.Prototypes);
            PredictedSpawnAtPosition(entity, coordinates);
        }

        if (component.DespawnWhenDone)
            PredictedQueueDel(uid);
    }
}
