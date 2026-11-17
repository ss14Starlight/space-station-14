using Content.Client.Clothing;
using Content.Client.Items.Systems;
using Content.Shared._Funkystation.Stains.Components;
using Content.Shared._Funkystation.Stains.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Funkystation.Stains;

public sealed partial class StainSystem : SharedStainSystem
{
    [Dependency] private IPrototypeManager _prototypeManager = null!;
    [Dependency] private SharedSolutionContainerSystem _solution = null!;
    [Dependency] private SpriteSystem _sprite = null!;

    [SubscribeLocalEvent]
    private void OnAppearanceChanged(Entity<StainableComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var spriteEnt = new Entity<SpriteComponent?>(ent.Owner, args.Sprite);

        // Starlight begin - layering fixes
        foreach (var key in ent.Comp.RevealedLayers) _sprite.RemoveLayer(spriteEnt, key, false);

        ent.Comp.RevealedLayers.Clear();

        foreach (var (key, layerData) in BuildVisuals(ent, ent.Comp.IconVisuals, "icon"))
        {
            var index = _sprite.AddLayer(spriteEnt, layerData, null);
            _sprite.LayerMapSet(spriteEnt, key, index);
            ent.Comp.RevealedLayers.Add(key);
        }
        // Starlight end
    }

    [SubscribeLocalEvent(after: [typeof(ClientClothingSystem)])]
    private void OnEquipmentVisuals(Entity<StainableComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        // Pulled-down masks may have no equipped sprite, so their stains must not float on the face.
        if (args.Slot == "mask" && TryComp<MaskComponent>(ent.Owner, out var mask) && mask.IsToggled) // Starlight
            return; // Starlight

        if (ent.Comp.ClothingVisuals.TryGetValue(args.Slot, out var layers))
            args.Layers.AddRange(BuildVisuals(ent, layers, args.Slot));
    }

    [SubscribeLocalEvent(after: [typeof(ItemSystem)])]
    private void OnInhandVisuals(Entity<StainableComponent> ent, ref GetInhandVisualsEvent args)
    {
        if (ent.Comp.ItemVisuals.TryGetValue(args.Location.ToString(), out var layers))
            args.Layers.AddRange(BuildVisuals(ent, layers, args.Location.ToString()));
    }

    private IEnumerable<(string, PrototypeLayerData)> BuildVisuals(Entity<StainableComponent> ent, List<PrototypeLayerData> templates, string prefix)
    {
        if (!_showClothingStains) // Starlight
            yield break; // Starlight

        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out _, out var sol) || sol.Volume <= FixedPoint2.Zero)
            yield break;

        var color = sol.GetColor(_prototypeManager);
        for (var i = 0; i < templates.Count; i++)
        {
            var layer = templates[i];
            layer.Color = color;
            yield return ($"stain-{prefix}-{i}", layer);
        }
    }
}
