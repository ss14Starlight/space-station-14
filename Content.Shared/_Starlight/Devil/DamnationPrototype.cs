using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Devil;

[Prototype("damnation")]
public sealed partial class DamnationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Name of the damnation
    /// </summary>
    [DataField("name")]
    public string Name => Loc.GetString($"damnation-{ID}-name");

    /// <summary>
    /// Description of the curse
    /// </summary>
    [DataField("description")]
    public string Description => Loc.GetString($"damnation-{ID}-description");

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
    /// Should these component changes be reversed if the damnation is removed?
    /// </summary>
    [DataField("reverseOnRemove")]
    public bool ReverseOnRemove = true;
}