using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Interaction;
using Content.Shared.Prying.Systems;

namespace Content.Shared._Starlight.Door;

/// <summary>
/// Handles firelock clicks from <see cref="FirelockOpenerComponent"/> users, which
/// <see cref="SharedDoorSystem"/> ignores for non-complex users.
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
        if (args.Handled || args.Complex || !HasComp<FirelockOpenerComponent>(args.User))
            return;

        if (!TryComp<DoorComponent>(ent, out var door) || !door.ClickOpen)
            return;

        // Warning lights block the toggle; the hand-pry only works unpowered.
        if (!_door.TryToggleDoor(ent, door, args.User, predicted: true))
            _prying.TryPry(ent, args.User, out _);

        args.Handled = true;
    }
}
