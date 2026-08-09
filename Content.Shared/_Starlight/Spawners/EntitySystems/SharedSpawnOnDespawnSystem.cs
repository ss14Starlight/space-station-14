using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using SpawnOnDespawnComponent = Content.Shared._Starlight.Spawners.Components.SpawnOnDespawnComponent;

namespace Content.Shared._Starlight.Spawners.EntitySystems;

public sealed partial class SharedSpawnOnDespawnSystem : EntitySystem // Starlight edit
{
    [Dependency] private SharedTransformSystem _xform = default!; // Starlight
    private readonly Queue<(EntProtoId Prototype, EntityCoordinates Coordinates, ComponentRegistry? overrides)> _queuedSpawns = new(); // Starlight

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnOnDespawnComponent, TimedDespawnEvent>(OnDespawn);
    }

    // Starlight Start
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Spawn queued entities after all deletions are processed
        while (_queuedSpawns.Count > 0)
        {
            var (prototype, coordinates, overrides) = _queuedSpawns.Dequeue();
            PredictedSpawnAtPosition(prototype, coordinates, overrides);
        }
    }
    // Starlight End

    private void OnDespawn(EntityUid uid, SpawnOnDespawnComponent comp, ref TimedDespawnEvent args)
    {
        if (!TryComp(uid, out TransformComponent? xform))
            return;

        _queuedSpawns.Enqueue((comp.Prototype, xform.Coordinates, comp.Overrides)); // Starlight Edit: Queue the spawn to occur after the entity is fully deleted
    }

    public void SetPrototype(Entity<SpawnOnDespawnComponent> entity, EntProtoId prototype)
    {
        entity.Comp.Prototype = prototype;
    }

    #region Starlight

    public void SetOverrides(Entity<SpawnOnDespawnComponent> entity, ComponentRegistry? overrides) =>
        entity.Comp.Overrides = overrides;

    #endregion
}
