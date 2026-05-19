using Content.Shared._Starlight.Devil.DamnationActions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Devil;

[Prototype]
public sealed partial class DamnationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Name of the damnation
    /// </summary>
    [DataField("name")]
    public string Name = "DAMNATION!!!!";

    /// <summary>
    /// Description of the curse
    /// </summary>
    [DataField("description")]
    public string Description = "THY END IS NOW!!!!";

    /// <summary>
    /// Cost of the curse. Negative are punishments, Positive are benefits.
    /// </summary>
    [DataField("cost")]
    public int Cost = 0;

    /// <summary>
    /// List of components to add to the player
    /// </summary>
    [DataField("components")]
    public ComponentRegistry Components = new();

    /// <summary>
    /// List of components to remove from the player
    /// </summary>
    [DataField("removedComponents")]
    public ComponentRegistry RemovedComponents = new();

    /// <summary>
    /// List of actions to run
    /// </summary>
    [DataField("actions")]
    public List<DamnationAction> Actions = new();

    /// <summary>
    /// Should the added components be removed if the damnation is undone?
    /// </summary>
    [DataField("reverseOnRemove")]
    public bool ReverseOnRemove = true;
}
