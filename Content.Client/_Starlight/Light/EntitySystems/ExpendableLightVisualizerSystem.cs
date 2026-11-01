using Content.Client.Light.Components;
using Content.Client.Light.EntitySystems;
using Content.Shared._Starlight.Light.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Light.EntitySystems;

public sealed partial class ExpendableLightVisualizerSystem : VisualizerSystem<ExpendableLightComponent>
{
    [Dependency] private PointLightSystem _pointLightSystem = default!;
    [Dependency] private LightBehaviorSystem _lightBehavior = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void FrameUpdate(float frameTime)
    {
        // Fade an unshaded glow layer with the light. The Glow layer ignores world lighting, so toggling its
        // visibility alone reads as a hard on/off; instead drive its alpha from the point light's animated
        // energy so the glow eases in and out in lockstep with the LightBehaviour fade.
        var query = EntityQueryEnumerator<ExpendableLightComponent, PointLightComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var comp, out var light, out var sprite))
        {
            if (!comp.FadeGlowWithEnergy)
                continue;

            if (comp.CurrentState is not (ExpendableLightState.Fading))
                continue;

            if (!SpriteSystem.TryGetLayer((uid, sprite), ExpendableLightVisualLayers.Glow, out var layer, false))
                continue;

            var alpha = comp.GlowReferenceEnergy > 0f
                ? Math.Clamp(light.Energy / comp.GlowReferenceEnergy, 0f, 1f)
                : 1f;

            // Preserve whatever RGB the layer already has (GlowColorLit, or e.g. the breadcrumb's per-crumb
            // tint) and only drive the alpha.
            SpriteSystem.LayerSetColor(layer, layer.Color.WithAlpha(alpha));
        }
    }

    protected override void OnAppearanceChange(EntityUid uid, ExpendableLightComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData<string>(uid, ExpendableLightVisuals.Behavior, out var lightBehaviourId, args.Component)
            && TryComp<LightBehaviourComponent>(uid, out var lightBehaviour))
        {
            _lightBehavior.StopLightBehaviour((uid, lightBehaviour));

            if (!string.IsNullOrEmpty(lightBehaviourId))
            {
                // Resume the fade at however far into the current state we already are, so a light that just
                // entered PVS (or spawned mid-life) picks up at the right point instead of replaying the fade.
                var elapsed = (float)(_timing.CurTime - comp.StateStartTime).TotalSeconds;
                _lightBehavior.StartLightBehaviour((uid, lightBehaviour), lightBehaviourId, elapsed);
            }
            else if (TryComp<PointLightComponent>(uid, out var light))
            {
                _pointLightSystem.SetEnabled(uid, false, light);
            }
        }

        if (!AppearanceSystem.TryGetData<ExpendableLightState>(uid, ExpendableLightVisuals.State, out var state, args.Component))
            return;

        switch (state)
        {
            case ExpendableLightState.Lit:
                if (SpriteSystem.LayerMapTryGet((uid, args.Sprite), ExpendableLightVisualLayers.Overlay, out var layerIdx, true))
                {
                    if (!string.IsNullOrWhiteSpace(comp.IconStateLit))
                        SpriteSystem.LayerSetRsiState((uid, args.Sprite), layerIdx, comp.IconStateLit);
                    if (!string.IsNullOrWhiteSpace(comp.SpriteShaderLit))
                        args.Sprite.LayerSetShader(layerIdx, comp.SpriteShaderLit);
                    else
                        args.Sprite.LayerSetShader(layerIdx, null, null);
                    if (comp.GlowColorLit.HasValue)
                        SpriteSystem.LayerSetColor((uid, args.Sprite), layerIdx, comp.GlowColorLit.Value);
                    SpriteSystem.LayerSetVisible((uid, args.Sprite), layerIdx, true);
                }

                if (comp.GlowColorLit.HasValue)
                    SpriteSystem.LayerSetColor((uid, args.Sprite), ExpendableLightVisualLayers.Glow, comp.GlowColorLit.Value);
                SpriteSystem.LayerSetVisible((uid, args.Sprite), ExpendableLightVisualLayers.Glow, true);

                break;
            case ExpendableLightState.Dead:
                if (SpriteSystem.LayerMapTryGet((uid, args.Sprite), ExpendableLightVisualLayers.Overlay, out layerIdx, true))
                {
                    if (!string.IsNullOrWhiteSpace(comp.IconStateSpent))
                        SpriteSystem.LayerSetRsiState((uid, args.Sprite), layerIdx, comp.IconStateSpent);
                    if (!string.IsNullOrWhiteSpace(comp.SpriteShaderSpent))
                        args.Sprite.LayerSetShader(layerIdx, comp.SpriteShaderSpent);
                    else
                        args.Sprite.LayerSetShader(layerIdx, null, null);
                }

                SpriteSystem.LayerSetVisible((uid, args.Sprite), ExpendableLightVisualLayers.Glow, false);
                break;
        }
    }
}
