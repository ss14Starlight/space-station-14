using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Xenobiology;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlimeExtractComponent : Component
{
    /// <summary>
    /// What occurs when this extract receives some specific reagent.
    /// Each entry is a reagent reaction, consisting of the requirements and then the response
    /// </summary>
    [DataField("extractReactions"), AutoNetworkedField]
    public List<ProtoId<ExtractReactionPrototype>> ExtractReactions = new();

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


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlimeExtractActiveReactionComponent : Component
{
    /// <summary>
    /// Whether the current slime extract is paused. Needed as AutoGenerateComponentPause is not possible on this component.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool CurrentlyPaused = false;
    
    /// <summary>
    /// The reactions currently active on this reagent, along with the timestamps of their activation.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<ProtoId<ExtractReactionPrototype>, TimeSpan> ActiveReactions = new();
}