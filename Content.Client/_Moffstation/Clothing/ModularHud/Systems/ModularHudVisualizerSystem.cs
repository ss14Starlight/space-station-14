using System.Linq;
using Content.Shared._Moffstation.Clothing.ModularHud.Components;
using Content.Shared.Clothing;
using Content.Shared.Foldable;
using Content.Shared.Hands;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.Reflection;

namespace Content.Client._Moffstation.Clothing.ModularHud.Systems;

/// This system updates the sprites of entities with <see cref="ModularHudComponent"/> based off the appearance data
/// keyed by <see cref="ModularHudVisualKeys"/>. This is what causes modular HUDs to have dynamic visuals.
/// This system also handles updating the sprite to account for folding, which is used to flip the orientation of eye
/// patch HUDs.
public sealed class ModularHudVisualizerSystem : VisualizerSystem<ModularHudVisualsComponent>
{
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly IReflectionManager _reflect = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ModularHudVisualsComponent, GetInhandVisualsEvent>(OnGetInhandVisuals);
        SubscribeLocalEvent<ModularHudVisualsComponent, GetEquipmentVisualsEvent>(OnGetClothingVisuals);
    }

    /// Updates the icon sprites when receiving an <see cref="AppearanceChangeEvent"/>, and triggers in-hand and
    /// clothing sprites to update as well.
    protected override void OnAppearanceChange(
        EntityUid uid,
        ModularHudVisualsComponent component,
        ref AppearanceChangeEvent args
    )
    {
        if (args.Sprite is null)
            return;

        EnsureFoldedSpriteStates((uid, component, args.Sprite, args.Component));
        UpdateModularHudLayers((uid, args.Sprite, args.Component));

        // Update clothing & in-hand visuals.
        _item.VisualsChanged(uid);
    }

    private void EnsureFoldedSpriteStates(
        Entity<ModularHudVisualsComponent, SpriteComponent, AppearanceComponent> entity
    )
    {
        // Only worry about folded visuals if we have a suffix. A lack of a suffix implies this HUD doesn't
        // support folding.
        if (entity.Comp1.FoldedLayerSuffix is not { } foldedSuffix)
            return;

        var folded = IsFolded((entity, entity.Comp3));
        foreach (var layer in entity.Comp2.AllLayers.OfType<SpriteComponent.Layer>())
        {
            if (layer.State is { IsValid: true, Name: { } state })
            {
                switch (folded)
                {
                    // If we're folded and the state isn't folded presently, make it folded.
                    case true when !state.EndsWith(foldedSuffix):
                        SpriteSystem.LayerSetRsiState(layer, state + entity.Comp1.FoldedLayerSuffix);
                        break;
                    // If we're not folded and the state is folded presently, make it not folded.
                    case false when state.EndsWith(foldedSuffix):
                        SpriteSystem.LayerSetRsiState(layer, state.Replace(entity.Comp1.FoldedLayerSuffix, ""));
                        break;
                }
            }
        }
    }

    private void UpdateModularHudLayers(Entity<SpriteComponent, AppearanceComponent> entity)
    {
        foreach (var layerKey in Enum.GetValues<ModularHudVisualKeys>())
        {
            // If there's no data for this layer, skip it, or if the layer doesn't exist, don't try to modify it.
            if (!AppearanceSystem.TryGetData<ModularHudVisualData>(entity, layerKey, out var data, entity.Comp2) ||
                !SpriteSystem.TryGetLayer(entity.AsNullable(), layerKey, out var layer, logMissing: true))
                continue;

            SpriteSystem.LayerSetColor(layer, data.Color);
            SpriteSystem.LayerSetVisible(layer, data.Visible);
        }
    }

    /// Updates the in-hand sprites.
    private void OnGetInhandVisuals(Entity<ModularHudVisualsComponent> entity, ref GetInhandVisualsEvent args)
    {
        // Not all layers exist on all in-hand sprites. Don't try to modulate layers which aren't present.
        var excludedLayers = entity.Comp.InhandExcludedLayers.GetExcludedLayersOrDefaultForSpecies(
            CompOrNull<InventoryComponent>(args.User)?.SpeciesId
        );
        var location = args.Location.ToString().ToLowerInvariant();
        OnGetGenericVisuals(
            entity,
            ref args.Layers,
            $"hand-{args.Location.ToString().ToLowerInvariant()}",
            // Return null if this layer should be excluded.
            key =>
            {
                if (excludedLayers.Contains(key))
                    return null;
                if (entity.Comp.LayerMap.GetValueOrDefault(key) is { } state)
                    return $"inhand-{location}-{state}";
                return null;
            });
    }

    /// Updates clothing sprites.
    private void OnGetClothingVisuals(Entity<ModularHudVisualsComponent> entity, ref GetEquipmentVisualsEvent args)
    {
        // Some species use different states, so make sure we consider those here.
        var speciesId = CompOrNull<InventoryComponent>(args.Equipee)?.SpeciesId;
        var excludedLayers = entity.Comp.EquippedExcludedLayers.GetExcludedLayersOrDefaultForSpecies(speciesId);
        var id = speciesId is not null && entity.Comp.SpeciesWithDifferentClothing.Contains(speciesId)
            ? speciesId
            : null;
        var speciesSuffix = id != null ? $"-{id.ToLowerInvariant()}" : "";
        var foldedSuffix = IsFolded(entity.Owner) ? entity.Comp.FoldedLayerSuffix : "";
        OnGetGenericVisuals(
            entity,
            ref args.Layers,
            $"equipped-{args.Slot.ToUpperInvariant()}-",
            // Return null if this layer should be excluded.
            key =>
            {
                if (excludedLayers.Contains(key))
                    return null;

                if (entity.Comp.LayerMap.GetValueOrDefault(key) is not { } state)
                    return null;

                return $"equipped-EYES-{state}{speciesSuffix}{foldedSuffix}";
            });
    }

    /// Generically handles in-hand and clothing sprite layer setting.
    /// <param name="entity">The modular HUD whose appearance will be used</param>
    /// <param name="layers">The list of layers to add to</param>
    /// <param name="visualKeyPrefix">The prefix applied to all states added to <paramref name="layers"/>, eg. inhand-left, equipped-SUITSTORAGE</param>
    /// <param name="visualsLayerToRsiState">A lookup function which turns <see cref="ModularHudVisualKeys"/> elements into RSI state names</param>
    private void OnGetGenericVisuals(
        Entity<ModularHudVisualsComponent> entity,
        ref List<(string, PrototypeLayerData)> layers,
        string visualKeyPrefix,
        Func<ModularHudVisualKeys, string?> visualsLayerToRsiState
    )
    {
        if (!TryComp<AppearanceComponent>(entity, out var appearance))
            return;

        foreach (var key in Enum.GetValues<ModularHudVisualKeys>())
        {
            if (visualsLayerToRsiState(key) is not { } state)
                continue;

            var hasAppearance =
                AppearanceSystem.TryGetData<ModularHudVisualData>(entity, key, out var data, appearance);
            layers.Add((
                $"{visualKeyPrefix}-{_reflect.GetEnumReference(key)}",
                new PrototypeLayerData
                {
                    State = state,
                    Visible = data.Visible,
                    Color = data.Color,
                }
            ));
        }
    }

    private bool IsFolded(Entity<AppearanceComponent?> entity) => AppearanceSystem.TryGetData<bool>(
        entity,
        FoldableSystem.FoldedVisuals.State,
        out var folded,
        entity
    ) && folded;
}
