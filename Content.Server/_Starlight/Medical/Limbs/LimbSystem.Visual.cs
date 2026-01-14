using System.Linq;
using Content.Shared._Starlight.Medical.Limbs;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Starlight;

namespace Content.Server._Starlight.Medical.Limbs;
public sealed partial class LimbSystem : SharedLimbSystem
{
    public void AddLimbVisual(Entity<HumanoidAppearanceComponent> body, Entity<BodyPartComponent> limb)
    {
        var layers = new List<HumanoidVisualLayers>();
        foreach (var partLimbId in _body.GetBodyPartAdjacentParts(limb, limb.Comp).Concat([limb]))
        {
            if (!TryComp(partLimbId, out BodyPartComponent? partLimb)) continue;
            var layer = partLimb.ToHumanoidLayers();

            if (layer is null) continue;
            layers.Add(layer.Value);

            if (TryComp<BaseLayerIdComponent>(partLimbId, out var baseLayerStorage)
                && (baseLayerStorage.Layers.TryGetValue(body.Comp.Species, out var baseLayer) 
                || baseLayerStorage.Layers.TryGetValue("Default", out baseLayer))
                && baseLayer.HasValue)
            {
                _humanoidAppearanceSystem.SetBaseLayerId(body, layer.Value, baseLayer, true, body.Comp);
                var @base = _prototype.Index(baseLayer.Value);
                _humanoidAppearanceSystem.SetBaseLayerColor(body, layer.Value, @base.MatchSkin ? body.Comp.SkinColor : Color.White, true, body.Comp);
            }
        }
        _humanoidAppearanceSystem.SetLayersVisibility(body!, layers, true);
    }
    private void RemoveLimbVisual(Entity<TransformComponent, HumanoidAppearanceComponent, BodyComponent> body, Entity<TransformComponent, MetaDataComponent, BodyPartComponent> limb)
    {
        if (!TryComp<HumanoidAppearanceComponent>(body, out var humanoid))
            return;

        var layers = new List<HumanoidVisualLayers>();
        foreach (var partLimbId in _body.GetBodyPartAdjacentParts(limb, limb).Concat([limb]))
        {
            if (!TryComp<BaseLayerIdComponent>(partLimbId, out var baseLayerStorage)
                || !TryComp(partLimbId, out BodyPartComponent? partLimb))
                continue;

            var layer = partLimb.ToHumanoidLayers();
            if (layer is null) continue;
            layers.Add(layer.Value);

            if (humanoid.CustomBaseLayers.TryGetValue(layer.Value, out var customBaseLayer))
                if (baseLayerStorage.Layers.ContainsKey(humanoid.Species))
                    baseLayerStorage.Layers[humanoid.Species] = customBaseLayer.Id;
                else if (baseLayerStorage.Layers.ContainsKey("Default"))
                    baseLayerStorage.Layers["Default"] = customBaseLayer.Id;
                else
                    baseLayerStorage.Layers.Add("Default", customBaseLayer.Id); // This should never happen
            else
            {
                var speciesProto = _prototype.Index(humanoid.Species);
                var baseSprites = _prototype.Index<HumanoidSpeciesBaseSpritesPrototype>(speciesProto.SpriteSet);
                if (baseSprites.Sprites.TryGetValue(layer.Value, out var baseLayer))
                    if (baseLayerStorage.Layers.ContainsKey(humanoid.Species))
                        baseLayerStorage.Layers[humanoid.Species] = baseLayer;
                    else if (baseLayerStorage.Layers.ContainsKey("Default"))
                        baseLayerStorage.Layers["Default"] = baseLayer;
                    else
                        baseLayerStorage.Layers.Add("Default", baseLayer); // This should never happen
            }
        }

        _humanoidAppearanceSystem.SetLayersVisibility(new Entity<HumanoidAppearanceComponent>(body.Owner, body.Comp2)!, layers, false);
    }
    public void ToggleLimbVisual(Entity<HumanoidAppearanceComponent> body, Entity<BaseLayerIdComponent, BaseLayerIdToggledComponent, BodyPartComponent> limb, bool toggled)
    {
        var layer = limb.Comp3.ToHumanoidLayers();
        if (layer is null) return;

        _humanoidAppearanceSystem.SetBaseLayerId(body, layer.Value, toggled ? 
        !limb.Comp2.Layers.TryGetValue(body.Comp.Species, out var baseLayerToggled)? // Get layer value by species
            !limb.Comp2.Layers.TryGetValue("Default", out baseLayerToggled)? null : baseLayerToggled : baseLayerToggled : // Fall back to default, if it exists and species is undefined
        !limb.Comp1.Layers.TryGetValue(body.Comp.Species, out var baseLayer)? 
            !limb.Comp1.Layers.TryGetValue("Default", out baseLayer)? null : baseLayer : baseLayer 
        , true, body.Comp);
    }
}
