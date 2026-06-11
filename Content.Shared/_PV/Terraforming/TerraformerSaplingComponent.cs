namespace Content.Shared._PV.Terraforming;

[RegisterComponent]
public sealed partial class TerraformerSaplingComponent : Component
{
    /// <summary>
    /// Fallback tree entity spawned inside the terraformer radius after this sapling is planted into a terraformer.
    /// </summary>
    [DataField]
    public string TreePrototype = string.Empty;

    /// <summary>
    /// If set, one of these tree prototypes is randomly picked when the sapling grows.
    /// Use this for visual variants such as tree01/tree02.
    /// </summary>
    [DataField]
    public List<string> TreePrototypes = new();

    /// <summary>
    /// Delay before the tree is spawned after inserting the sapling into the terraformer.
    /// </summary>
    [DataField]
    public float SpawnDelay = 8f;
}
