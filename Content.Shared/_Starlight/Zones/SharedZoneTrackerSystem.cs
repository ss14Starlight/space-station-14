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

    public void SetZone(
        Entity<ZoneTrackerComponent> ent,
        ProtoId<ZonePrototype>? zone,
        (EntityUid Grid, Vector2i Tile) position)
    {
        ent.Comp.LastPosition = position;

        if (ent.Comp.Zone != zone)
        {
            ent.Comp.Zone = zone;
            Dirty(ent);
        }

        RaiseIfChanged(ent);
    }

    public void RaiseIfChanged(Entity<ZoneTrackerComponent> ent)
    {
        if (ent.Comp.Raised == ent.Comp.Zone)
            return;

        var ev = new ZoneChangedEvent(ent.Comp.Raised, ent.Comp.Zone);
        ent.Comp.Raised = ent.Comp.Zone;
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    public ProtoId<ZonePrototype>? GetZone(Entity<ZoneTrackerComponent?> ent)
        => Resolve(ent.Owner, ref ent.Comp, false) ? ent.Comp.Zone : null;

    public bool IsInZone(Entity<ZoneTrackerComponent?> ent, ProtoId<ZonePrototype> zone)
        => GetZone(ent) == zone;
}
