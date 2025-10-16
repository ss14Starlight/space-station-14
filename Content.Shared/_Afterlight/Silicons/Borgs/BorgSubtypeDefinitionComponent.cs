using System.Numerics;
using Content.Shared.Inventory;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Afterlight.Silicons.Borgs;

/// <summary>
/// Information relating to a borg's subtype. Should be mostly cosmetic.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[EntityCategory("BorgSubtype")]
public sealed partial class BorgSubtypeDefinitionComponent : Component
{
    [ValidatePrototypeId<SoundCollectionPrototype>]
    private static readonly ProtoId<SoundCollectionPrototype> _defaultFootsteps = new("FootstepBorg");

    /// <summary>
    /// <inheritdoc cref="BorgTypePrototype.InventoryTemplateId"/>
    /// </summary>
    [DataField, AutoNetworkedField] public ProtoId<InventoryTemplatePrototype> InventoryTemplateId = "borgShort";

    /// <summary>
    /// The parent borg type of this subtype.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public string ParentType;

    /// <summary>
    /// <inheritdoc cref="BorgTypePrototype.AddComponents"/>
    /// </summary>
    [DataField] public ComponentRegistry? AddComponents;

    /// <summary>
    /// Sprite path that the prototype's layer data will reference.
    /// </summary>
    [DataField, AutoNetworkedField] public ResPath? SpritePath;

    /// <summary>
    /// The visual layer data for the subtype.
    /// At the minimum should have definitions for each value of <see cref="BorgVisualLayers"/>.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public PrototypeLayerData[] LayerData;

    [DataField]
    public string SpriteHasMindState { get; set; } = "borg_e";

    [DataField]
    public string SpriteNoMindState { get; set; } = "borg_e_r";

    [DataField]
    public string? SpriteBodyState;

    [DataField]
    public string SpriteToggleLightState { get; set; } = "borg_l";

    [DataField, AutoNetworkedField] public Vector2? Offset;

    [DataField, AutoNetworkedField] public string PetSuccessString = "petting-success-generic-cyborg";
    [DataField, AutoNetworkedField] public string PetFailureString = "petting-failure-generic-cyborg";

    /// <summary>
    /// Sound specifier for footstep sounds created by this subtype.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier FootstepCollection { get; set; } = new SoundCollectionSpecifier(_defaultFootsteps);

    /// <summary>
    /// <inheritdoc cref="BorgTypePrototype.SpriteBodyMovementState"/>
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? SpriteBodyMovementState { get; set; }

    [DataField]
    public int? Price { get; set; }
}