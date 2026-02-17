using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Xenobiology;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class GeneticSlimeExtractComponent : Component
{
    
    
    /// <summary>
    /// The name of the container that holds the solution.
    /// Needed so that the slime extract can communicate with the container itself.
    /// </summary>
    [DataField("containerName", required: true), AutoNetworkedField]
    public string ContainerName = string.Empty;
    
    /// <summary>
    /// How many times this extract can be used before being deleted or exhausted.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int RemainingUses = 1;
}