using Content.Shared._TP.Kitchen.Components;
using Robust.Client.GameObjects;

namespace Content.Client._TP14.Kitchen;

public sealed class DeepFryerVisualizerSystem : VisualizerSystem<Shared._TP.Kitchen.Components.SharedDeepFryerComponent>
{
    protected override void OnAppearanceChange(EntityUid uid,
        SharedDeepFryerComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        AppearanceSystem.TryGetData<bool>(uid, DeepFryerVisuals.Active, out var active, args.Component);
        if (SpriteSystem.LayerMapTryGet((uid, args.Sprite), DeepFryerVisuals.Active, out var layer, false))
        {
            SpriteSystem.LayerSetVisible((uid, args.Sprite), layer, active);
        }
    }
}
