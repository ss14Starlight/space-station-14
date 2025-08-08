using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

// Lifted from ItemBorgModuleComponent.cs for separate maintenance and access
namespace Content.Shared._Starlight.Mech.Components;

/// <summary>
/// Used for <see cref="MechEquipmentComponent.cs"/> for providing usable items to a mech via modules
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MechActiveEquipComponent : Component
{
    /// <summary>
    /// The items that are provided.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> Items = new();

    /// <summary>
    /// The entities from <see cref="Items"/> that were spawned.
    /// </summary>
    [DataField("providedItems")]
    public SortedDictionary<string, EntityUid> ProvidedItems = new();

    /// <summary>
    /// A counter that ensures a unique hand for every created item
    /// </summary>
    [DataField("handCounter")]
    public int HandCounter;

    /// <summary>
    /// Whether or not the items have been created and stored in <see cref="ProvidedContainer"/>
    /// </summary>
    public bool ItemsCreated;

    /// <summary>
    /// A container where provided items are stored when not being used.
    /// This is helpful as it means that items retain state.
    /// </summary>
    [ViewVariables]
    public Container ProvidedContainer = default!;

    /// <summary>
    /// An ID for the container where provided items are stored when not used.
    /// </summary>
    [DataField("providedContainerId")]
    public string ProvidedContainerId = "provided_container";
}
