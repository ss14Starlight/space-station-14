using Content.Shared._Starlight.CCVar;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Sprite;

/// <summary>
/// Supplies client-selectable sprite variants for an entity.
/// </summary>
/// <remarks>
/// When this is paired with a <see cref="SpriteComponent"/>, <see cref="SpriteQualitySystem"/>
/// swaps the configured layer in place. Other client renderers can use
/// <see cref="SpriteQualitySystem.GetSprite"/> to select a variant
/// without requiring a <see cref="SpriteComponent"/>.
/// </remarks>
[RegisterComponent]
public sealed partial class SpriteQualityComponent : Component
{
    /// <summary>
    /// Sprite layer data to use at each supported quality level.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<SpriteQualityLevel, Variant> Variants = default!;

    /// <summary>
    /// The primary <see cref="SpriteComponent"/> layer to replace, if one is present.
    /// </summary>
    [DataField]
    public int Layer;

    /// <summary>
    /// All sprite data for one quality level.
    /// </summary>
    [DataDefinition]
    public sealed partial class Variant
    {
        /// <summary>
        /// Data applied to <see cref="SpriteQualityComponent.Layer"/>. Renderers which do not
        /// use a <see cref="SpriteComponent"/> also use this as their selected sprite.
        /// </summary>
        [DataField("base", required: true)]
        public PrototypeLayerData BaseLayer = default!;

        /// <summary>
        /// Optional additional sprite layers to update at the same time.
        /// </summary>
        [DataField]
        public Dictionary<int, PrototypeLayerData> Layers = new();
    }
}
