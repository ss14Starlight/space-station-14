using Content.Client.Interactable.Components;
using Content.Shared._Starlight.Light;
using Content.Shared.Light;
using Content.Shared.Wires;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Light;

public sealed partial class LightFloorVisualizerSystem : VisualizerSystem<LightFloorComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, LightFloorComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        AppearanceSystem.TryGetData<bool>(uid, WiresVisuals.MaintenancePanelState, out var open, args.Component);

        // No bulb state at all means an always powered floor, which is always lit.
        var lit = !AppearanceSystem.TryGetData<PoweredLightState>(uid, PoweredLightVisuals.BulbState, out var state, args.Component)
                || state == PoweredLightState.On;

        SpriteSystem.LayerSetVisible((uid, args.Sprite), LightFloorLayers.Cover, !open);
        SpriteSystem.LayerSetVisible((uid, args.Sprite), LightFloorLayers.CoverGlow, !open && lit);

        if (TryComp<PointLightComponent>(uid, out var light))
            SpriteSystem.LayerSetColor((uid, args.Sprite), LightFloorLayers.CoverGlow, light.Color);

        if (open)
            EnsureComp<InteractionOutlineComponent>(uid);
        else
            RemComp<InteractionOutlineComponent>(uid);
    }
}
