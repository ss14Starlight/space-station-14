using Content.Shared._Starlight.Door;
using Content.Shared.Doors;
using Content.Server.Doors.Systems;
using Content.Shared.Doors.Components;

namespace Content.Server._Starlight.Door;

public sealed class TimedDoorClosingSystem : EntitySystem
{
    [Dependency] private DoorSystem _door = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TimedDoorClosingComponent, DoorStateChangedEvent>(OnDoorStateChanged);
    }

    private void OnDoorStateChanged(Entity<TimedDoorClosingComponent> ent, ref DoorStateChangedEvent args)
    {
        if (args.State != DoorState.Open)
            return;

        _door.SetNextStateChange(ent, ent.Comp.AutoCloseDelay);
    }
}
