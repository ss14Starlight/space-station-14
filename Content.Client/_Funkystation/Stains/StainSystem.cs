using Content.Client.Clothing;
using Content.Client.Items.Systems;
using Content.Shared._Funkystation.Stains.Components;
using Content.Shared._Funkystation.Stains.Systems;
using Content.Shared._Starlight.CCVar;
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

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StainableComponent, AppearanceChangeEvent>(OnAppearanceChanged);
        SubscribeLocalEvent<StainableComponent, GetEquipmentVisualsEvent>(OnEquipmentVisuals, after: [typeof(ClientClothingSystem)]);
        SubscribeLocalEvent<StainableComponent, GetInhandVisualsEvent>(OnInhandVisuals, after: [typeof(ItemSystem)]);
        #region Starlight
        SubscribeLocalEvent<MaskComponent, AfterAutoHandleStateEvent>(OnMaskStateChanged);

        _showClothingStains = _cfg.GetCVar(StarlightCCVars.ShowClothingStains);
        _cfg.OnValueChanged(StarlightCCVars.ShowClothingStains, OnShowClothingStainsChanged);
        #endregion
    }

    private void OnAppearanceChanged(Entity<StainableComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var spriteEnt = new Entity<SpriteComponent?>(ent.Owner, args.Sprite);

        var layers = new List<int>(ent.Comp.RevealedLayers);
        layers.Sort((a, b) => b.CompareTo(a));

        foreach (var layer in layers)
        {
            _sprite.RemoveLayer(spriteEnt, layer);
        }

        ent.Comp.RevealedLayers.Clear();

        foreach (var (_, layerData) in BuildVisuals(ent, ent.Comp.IconVisuals, "icon"))
        {
            ent.Comp.RevealedLayers.Add(_sprite.AddLayer(spriteEnt, layerData, null));
        }
    }

    private void OnEquipmentVisuals(Entity<StainableComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        // Pulled-down masks may have no equipped sprite, so their stains must not float on the face.
        if (args.Slot == "mask" && TryComp<MaskComponent>(ent.Owner, out var mask) && mask.IsToggled) // Starlight
            return; // Starlight

        if (ent.Comp.ClothingVisuals.TryGetValue(args.Slot, out var layers))
            args.Layers.AddRange(BuildVisuals(ent, layers, args.Slot));
    }

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
