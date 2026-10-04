using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Interaction;
using Content.Shared.Prying.Systems;

namespace Content.Shared._Starlight.Door;

/// <summary>
/// Mirrors <see cref="SharedDoorSystem"/>'s click-to-open for firelocks, for users
/// with <see cref="FirelockOpenerComponent"/> that the door system's own handler
/// skips because they can't do complex interactions.
/// </summary>
public sealed partial class FirelockOpenerSystem : EntitySystem
{
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private PryingSystem _prying = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FirelockComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnActivate(Entity<FirelockComponent> ent, ref ActivateInWorldEvent args)
    {
        // Complex users are already handled by the door system.
        if (args.Handled || args.Complex || !HasComp<FirelockOpenerComponent>(args.User))
            return;

        if (!TryComp<DoorComponent>(ent, out var door) || !door.ClickOpen)
            return;

        // Same as a bare hand: the firelock's own BeforeDoorOpenedEvent refuses
        // while its warning lights are on, and an unpowered firelock falls back
        // to a slow hand-pry.
        if (!_door.TryToggleDoor(ent, door, args.User, predicted: true))
            _prying.TryPry(ent, args.User, out _);

        args.Handled = true;
    }
}
