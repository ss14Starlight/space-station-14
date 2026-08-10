using Content.Shared.Hands.Components;

namespace Content.Client.Toggleable;

/// <summary>
/// Component that handles toggling the visuals of an entity, including layers on an entity's sprite,
/// the in-hand visuals, and the clothing/equipment visuals.
/// </summary>
/// <see cref="ToggleableVisualsSystem"/>
[RegisterComponent]
public sealed partial class ToggleableVisualsComponent : Component
{
    /// <summary>
    /// Sprite layer that will have its visibility toggled when this item is toggled.
    /// </summary>
    [DataField(required: true)]
    public string? SpriteLayer;

    /// <summary>
    /// Layers to add to the sprite of the player that is holding this entity (while the component is toggled on).
    /// </summary>
    [DataField]
    public Dictionary<HandLocation, List<PrototypeLayerData>> InhandVisuals = new();

    /// <summary>
    /// Layers to add to the sprite of the player that is wearing this entity (while the component is toggled on).
    /// </summary>
    [DataField]
    public Dictionary<string, List<PrototypeLayerData>> ClothingVisuals = new();

    #region Starlight

    /// <summary>
    /// Additional layers to toggle.
    /// </summary>
    /// <remarks>
    /// Added to avoid needing to alter several dozen prototypes.
    /// </remarks>
    [DataField] public List<string> AdditionalLayers = [];

    /// <summary>
    /// List of layers to ignore when modulating color with appearance data.
    /// </summary>
    [DataField] public List<string> ModulateIgnoreLayers = [];

    /// <summary>
    /// Toggleable visuals for when wielding item.
    /// </summary>
    [DataField] public Dictionary<HandLocation, List<PrototypeLayerData>> WieldingVisuals = [];

    #endregion
}
