using Content.Shared.Inventory;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.RetractableItemAction;

/// <summary>
/// Used for storing an unremovable item within an action and summoning it into your hand on use.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(RetractableItemActionSystem))]
public sealed partial class RetractableItemActionComponent : Component
{
    /// <summary>
    /// The item that will appear be spawned by the action.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId SpawnedPrototype;

    /// <summary>
    /// Sound collection to play when the item is summoned.
    /// </summary>
    [DataField]
    public SoundCollectionSpecifier? SummonSounds;

    /// <summary>
    /// Sound collection to play when the summoned item is retracted back into the action.
    /// </summary>
    [DataField]
    public SoundCollectionSpecifier? RetractSounds;

    /// <summary>
    /// The item managed by the action. Will be summoned and hidden as the action is used.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ActionItemUid;

    /// <summary>
    /// The container ID used to store the item.
    /// </summary>
    public const string ContainerId = "item-action-item-container";

    #region Starlight
    /// <summary>
    /// Does the item get summoned into your hand?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SpawnInHand = true;

    /// <summary>
    /// Which slot does the item get retracted from?
    /// </summary>
    [DataField, AutoNetworkedField]
    public SlotFlags RequiredSlots = SlotFlags.NONE;

    /// <summary>
    /// Which slot does the item get summoned into?
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Slot = "none";

    #endregion Starlight
}
