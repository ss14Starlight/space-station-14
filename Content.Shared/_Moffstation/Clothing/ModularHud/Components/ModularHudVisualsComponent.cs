using Content.Shared._Moffstation.Clothing.ModularHud.Systems;
using Content.Shared.Foldable;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.Clothing.ModularHud.Components;

/// Where <see cref="ModularHudComponent"/> conveys functionality, this component confers non-functional dynamic
/// visuals. Basically, allows entities with <see cref="ModularHudComponent"/> to have their sprites modified by the
/// <see cref="ModularHudModuleComponent"/> they contain.
/// <seealso cref="SharedModularHudSystem.SyncVisuals"/>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedModularHudSystem))]
public sealed partial class ModularHudVisualsComponent : Component
{
    /// The colors this HUD use for layers when no modules modify that layer.
    [DataField(required: true)]
    public Dictionary<ModularHudVisuals, Color> DefaultVisuals = default!;

    /// <see cref="ModularHudModuleComponent.ModuleColor"/>s included in this entity's visuals regardless of the modules
    /// in it. This is  used, eg. to give sunglasses an innate tint.
    [DataField]
    public Dictionary<ModularHudVisuals, ModularHudModuleComponent.ModuleColor> InnateVisuals = new();

    /// A map of <see cref="ModularHudVisualKeys"/> to layer names, used to determine which layers on the sprite
    /// correspond to which conceptual layers.
    [DataField]
    public Dictionary<ModularHudVisualKeys, string> LayerMap = [];

    /// If an entity with an <see cref="InventoryComponent.SpeciesId"/> in this list wears this HUD, all of its states
    /// will include that species ID. This enables the sort of thing like <c>equipped-EYES</c> versus
    /// <c>equipped-EYES-vox</c>.
    [DataField, Access(Other = AccessPermissions.ReadExecute)]
    public List<string> SpeciesWithDifferentClothing = [];

    /// A <see cref="ModularHudVisualsExcludedLayers"/> which describes which layers for which species should not
    /// attempt to be rendered when held.
    [DataField, Access(Other = AccessPermissions.ReadExecute)]
    public ModularHudVisualsExcludedLayers InhandExcludedLayers;

    /// A <see cref="ModularHudVisualsExcludedLayers"/> which describes which layers for which species should not
    /// attempt to be rendered when worn.
    // If modular huds ever need to be worn anywhere other than eyes, we'll need to add one of these for each inventory slot.
    [DataField, Access(Other = AccessPermissions.ReadExecute)]
    public ModularHudVisualsExcludedLayers EquippedExcludedLayers;

    /// Suffix applied to sprite layer states if this entity is <see cref="FoldableComponent.IsFolded"/>. If null,
    /// clients will skip all folding visuals logic.
    [DataField]
    public string? FoldedLayerSuffix;

    /// If true, the ModularHudVisualizerSystem will handle rendering the frame layer. This is useful for when, eg.,
    /// the strap "frame" layer of eye patches need to be dynamically flipped.
    [DataField]
    public bool FrameIsDynamic = false;
}

/// Data used to describe which layers the visualizer ought not attempt to include in a sprite based on the wearer's
/// species. Some species' sprites are just too small to include all layers, so they are omitted sometimes.
/// <param name="Default">The set of layers excluded from sprites when no species can be determined, or when <see cref="Species"/> does not include information for the relevant species.</param>
/// <param name="Species">Sets of layers excluded from sprites, keyed by species ID.</param>
[DataRecord, Serializable, NetSerializable]
public readonly partial record struct ModularHudVisualsExcludedLayers(
    HashSet<ModularHudVisualKeys>? Default = null,
    Dictionary<string, HashSet<ModularHudVisualKeys>>? Species = null
)
{
    public HashSet<ModularHudVisualKeys> GetExcludedLayersOrDefaultForSpecies(string? speciesId)
    {
        var defaultLayers = Default ?? [];
        if (speciesId == null || Species is not { } ss)
            return defaultLayers;

        return ss.GetValueOrDefault(speciesId, defaultLayers);
    }
}

/// Colorable parts of modular HUDs.
[Serializable, NetSerializable]
public enum ModularHudVisuals : byte
{
    Accent,
    Lens,
    Specular,
}

/// This event, when raised on an entity with a HUD component, will refresh the HUD's information. This just triggers
/// <see cref="Content.Client.Overlays.EquipmentHudSystem.RefreshOverlay"/>.
[ByRefEvent]
public record struct EquipmentHudNeedsRefreshEvent;

/// Enum keys for modular HUD's <see cref="AppearanceComponent"/> visuals.
[Serializable, NetSerializable]
public enum ModularHudVisualKeys : byte
{
    Frame,
    Accent,
    Lens,
    Specular,
    LensAccentMinor,
    LensAccentMajor,
}

[Serializable, NetSerializable]
public readonly record struct ModularHudVisualData(Color Color, bool Visible = true)
{
    public static readonly ModularHudVisualData Invisible = new(Color.White, false);
}
