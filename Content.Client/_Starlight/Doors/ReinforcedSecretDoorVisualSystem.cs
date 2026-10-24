using Content.Shared.Doors.Components;
using Content.Shared._Starlight.Doors.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Map;

namespace Content.Client._Starlight.Doors;

/// <summary>
/// Shows the exact reinforced-wall sprite while closed and the door's normal
/// opening animation while moving.
/// </summary>
public sealed partial class ReinforcedSecretDoorVisualSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprites = default!;

    /// <summary>Shows smoothed wall art while closed and door art while opening.</summary>
    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<ReinforcedSecretDoorVisualComponent, DoorComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var door, out var sprite, out var xform))
        {
            var animating = door.State is DoorState.Open or DoorState.Opening or DoorState.Closing;
            // Nullspace previews use the directional door art.
            var preview = xform.MapID == MapId.Nullspace;

            var noRotation = !animating && !preview;
            if (sprite.NoRotation != noRotation)
                sprite.NoRotation = noRotation;

            // Layer zero is the door animation; IconSmooth may add wall layers later.
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
