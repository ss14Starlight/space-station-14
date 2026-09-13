using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Zones;

public sealed partial class SharedZoneTrackerSystem : EntitySystem
{

    [SubscribeLocalEvent]
    private void OnHandleState(Entity<ZoneTrackerComponent> ent, ref AfterAutoHandleStateEvent _)
        => RaiseIfChanged(ent);

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<ZoneTrackerComponent> ent, ref ComponentShutdown _)
    {
        if (ent.Comp.Raised == null)
            return;

        var ev = new ZoneChangedEvent(ent.Comp.Raised, null);
        ent.Comp.Raised = null;
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    /// <summary>
    /// Sets the zone for the given entity and updates its position and revision. If the zone has changed, it marks the component as dirty and raises a ZoneChangedEvent.
    /// </summary>
    public void SetZone(
        Entity<ZoneTrackerComponent> ent,
        ProtoId<ZonePrototype>? zone,
        (EntityUid Grid, Vector2i Tile) position,
        int revision = 0)
    {
        ent.Comp.LastPosition = position;
        ent.Comp.LastRevision = revision;

        if (ent.Comp.Zone != zone)
        {
            ent.Comp.Zone = zone;
            Dirty(ent);
        }

        RaiseIfChanged(ent);
    }

    /// <summary>
    /// Raises a ZoneChangedEvent if the zone for the given entity has changed since the last raised event. This method checks if the current zone is different from the last raised zone and raises the event accordingly.
    /// </summary>
    public void RaiseIfChanged(Entity<ZoneTrackerComponent> ent)
    {
        if (ent.Comp.Raised == ent.Comp.Zone)
            return;

        var ev = new ZoneChangedEvent(ent.Comp.Raised, ent.Comp.Zone);
        ent.Comp.Raised = ent.Comp.Zone;
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    /// <summary>
    /// Gets the current zone for the given entity. If the entity is not in a zone, it returns null.
    /// </summary>
    public ProtoId<ZonePrototype>? GetZone(Entity<ZoneTrackerComponent?> ent)
        => Resolve(ent.Owner, ref ent.Comp, false) ? ent.Comp.Zone : null;

    /// <summary>
    /// Checks if the given entity is currently in the specified zone. Returns true if the entity's current zone matches the provided zone ID, otherwise returns false.
    /// </summary>
    public bool IsInZone(Entity<ZoneTrackerComponent?> ent, ProtoId<ZonePrototype> zone)
        => GetZone(ent) == zone;
}
