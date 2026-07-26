using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared.Doors.Components;
using Content.Server.Doors.Systems;
using Content.Shared.Doors;

namespace Content.Server._Starlight.CosmicCult;

public sealed class CosmicDoorSystem : EntitySystem
{
    [Dependency] private DoorSystem _door = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CosmicDoorComponent, DoorStateChangedEvent>(OnDoorStateChanged);
    }

    private void OnDoorStateChanged(Entity<CosmicDoorComponent> ent, ref DoorStateChangedEvent args)
    {
        if (args.State != DoorState.Open)
            return;

        _door.SetNextStateChange(ent,ent.Comp.AutoCloseDelay);
    }
}
