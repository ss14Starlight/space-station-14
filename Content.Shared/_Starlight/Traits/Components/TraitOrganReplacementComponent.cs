using Robust.Shared.Prototypes;

namespace Content.Shared.Starlight.Traits.Components;

/// <summary>
/// Component that replaces an organ when added via trait system
/// </summary>
[RegisterComponent]
public sealed partial class TraitOrganReplacementComponent : Component
{
    /// <summary>
    /// The slot ID of the organ to replace (e.g., "lungs", "heart", "liver")
    /// </summary>
    [DataField(required: true)]
    public string Slot = string.Empty;

    /// <summary>
    /// The prototype ID of the organ to spawn and insert
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Organ = string.Empty;

    /// <summary>
    /// Optional equipment to give when this organ is installed.
    /// Key is the inventory slot (e.g., "mask"), value is the item prototype ID.
    /// </summary>
    [DataField]
    public Dictionary<string, EntProtoId> Equipment = new();

    /// <summary>
    /// Optional item to spawn in hands when this organ is installed.
    /// </summary>
    [DataField]
    public EntProtoId? HandItem = null;

    /// <summary>
    /// If true, removes PassiveDamage component when this organ is installed.
    /// Useful for removing poison regen when switching breathing types.
    /// </summary>
    [DataField]
    public bool RemovePoisonRegen = false;
}

