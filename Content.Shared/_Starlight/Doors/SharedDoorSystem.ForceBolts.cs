using Content.Shared.Doors.Components;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Doors.Systems;

public abstract partial class SharedDoorSystem
{
    /// <summary>
    /// Raises or drops the bolts even when the door has no power. <see cref="TrySetBoltDown"/> refuses without power,
    /// which is right for players but not allows systems to unbolt doors.
    /// </summary>
    public void ForceSetBoltsDown(Entity<DoorBoltComponent> ent, bool value)
    {
        if (ent.Comp.BoltsDown == value)
            return;

        ent.Comp.BoltsDown = value;
        Dirty(ent, ent.Comp);
        UpdateBoltLightStatus(ent);
        RaiseLocalEvent(ent.Owner, new DoorBoltsChangedEvent(value));
    }
}
