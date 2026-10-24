using Content.Shared.SurveillanceCamera;
using Robust.Client.GameObjects;

namespace Content.Client.SurveillanceCamera;

// When the camera sprite changes between the observed and unobserved sprites, it resets the animation.
// This is intended to make it so that instead of resetting, it smoothly switches between sprites without resetting the animation.

public sealed partial class SurveillanceCameraVisualsSystem
{
    private void SetStatePreserveTime(Entity<SpriteComponent?> ent, int layer, string state)
    {
        int oldFrame = 0;
        float oldTime = 0f;
        float oldTimeLeft = 0f;
        bool hasOld = false;
        if (_sprite.TryGetLayer(ent, layer, out var oldLayer, false) && oldLayer != null)
        {
            oldFrame = oldLayer.AnimationFrame;
            oldTime = oldLayer.AnimationTime;
            oldTimeLeft = oldLayer.AnimationTimeLeft;
            hasOld = true;
        }

        _sprite.LayerSetRsiState(ent, layer, state);

        if (hasOld && _sprite.TryGetLayer(ent, layer, out var newLayer, false) && newLayer != null)
        {
            newLayer.AnimationFrame = oldFrame;
            newLayer.AnimationTime = oldTime;
            newLayer.AnimationTimeLeft = oldTimeLeft;
        }
    }
}
