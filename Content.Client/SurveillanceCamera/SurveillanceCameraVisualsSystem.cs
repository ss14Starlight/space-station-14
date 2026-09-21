using Content.Shared.SurveillanceCamera;
using Robust.Client.GameObjects;

namespace Content.Client.SurveillanceCamera;

public sealed partial class SurveillanceCameraVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurveillanceCameraVisualsComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(EntityUid uid, SurveillanceCameraVisualsComponent component,
        ref AppearanceChangeEvent args)
    {
        if (!args.AppearanceData.TryGetValue(SurveillanceCameraVisualsKey.Key, out var data)
            || data is not SurveillanceCameraVisuals key
            || args.Sprite == null
            || !_sprite.LayerMapTryGet((uid, args.Sprite), SurveillanceCameraVisualsKey.Layer, out var layer, false)
            || !component.CameraSprites.TryGetValue(key, out var state))
        {
            return;
        }

        // Starlight start
        if (key == SurveillanceCameraVisuals.Disabled)
        {
            // only do animations when there is actually an animation to be done
            // avoids an exception down the line when the engine inevitably tries to draw the off camera on an index higher than 0
            _sprite.LayerSetRsiState((uid, args.Sprite), layer, state);
        }
        else
        {
            SetStatePreserveTime((uid, args.Sprite), layer, state);
        }
        // Starlight end
    }
}
