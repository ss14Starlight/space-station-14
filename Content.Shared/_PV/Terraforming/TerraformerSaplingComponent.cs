namespace Content.Shared._PV.Terraforming;

[RegisterComponent]
public sealed partial class TerraformerSaplingComponent : Component
{
    /// <summary>
    /// Tree entity spawned inside the terraformer radius after this sapling is planted into a terraformer.
    /// </summary>
    [DataField(required: true)]
    public string TreePrototype = string.Empty;

    /// <summary>
    /// Delay before the tree is spawned after inserting the sapling into the terraformer.
    /// </summary>
    [DataField]
    public float SpawnDelay = 8f;
}
