using Content.Shared.Doors.Components;
using Content.Shared._Starlight.Doors.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Map;

namespace Content.Client._Starlight.Doors;

/// <summary>
/// Shows the exact reinforced-wall sprite while closed and the door's normal
/// opening animation while moving. Only mounted objects are split into halves.
/// </summary>
public sealed partial class ReinforcedSecretDoorVisualSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprites = default!;

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<ReinforcedSecretDoorVisualComponent, DoorComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var door, out var sprite, out var xform))
        {
            var animating = door.State is DoorState.Open or DoorState.Opening or DoorState.Closing;
            // Spawn-menu icons and placement ghosts live in nullspace. Show the
            // same directional door graphic as SolidSecretDoor there so admins
            // can see which way the reinforced facade will open.
            var preview = xform.MapID == MapId.Nullspace;

            // A placed closed facade must use WallReinforced's upright art.
            var noRotation = !animating && !preview;
            if (sprite.NoRotation != noRotation)
                sprite.NoRotation = noRotation;

            // Layer zero is DoorSystem's existing animation. IconSmooth adds the
            // remaining layers from the same RSI used by WallReinforced. Keep
            // checking them because IconSmooth can add layers after spawn;
            // LayerSetVisible itself skips layers already in the right state.
            _sprites.LayerSetVisible((uid, sprite), 0, animating || preview);
            var index = 0;
            foreach (var _ in sprite.AllLayers)
            {
                if (index > 0)
                    _sprites.LayerSetVisible((uid, sprite), index, !animating && !preview);
                index++;
            }
        }
    }
}
