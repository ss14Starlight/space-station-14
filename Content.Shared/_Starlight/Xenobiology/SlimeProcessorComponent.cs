using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Xenobiology;

/// <summary>
/// The base component all slime processor possess.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class SlimeProcessorComponent : Component
{
    /// <summary>
    /// The amount of time it takes to process slime corpses.
    /// </summary>
    [DataField("processingTime", required: true), AutoNetworkedField]
    public TimeSpan ProcessingTime = default!;

    /// <summary>
    /// How many extracts are obtained per slime corpse.
    /// </summary>
    [DataField("yieldMultiplier", required: true), AutoNetworkedField]
    public int YieldMultiplier = default;

    /// <summary>
    /// How long between each slime acquire.
    /// </summary>
    [DataField("slimeAcquireCooldown", required: true), AutoNetworkedField]
    public TimeSpan SlimeAcquireCooldown = default!;
    
    /// <summary>
    /// The name of the container the slime corpses are stored in.
    /// </summary>
    public const string SlimeContainerName = "slimes";
    
    /// <summary>
    /// Container for dead slimes inserted in the processor.
    /// </summary>
    [ViewVariables]
    public Container SlimeContainer = default!;
}

/// <summary>
/// The component for slime processors which are toggled on.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ActiveSlimeProcessorComponent : Component
{
    /// <summary>
    /// The moment in time when processing will be done.
    /// </summary>
    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan? ProcessingFinishedMoment = default!;
}

/// <summary>
/// The component for slime processors which are toggled off and which are collecting slimes.
/// All slime processors get this component on initialization, so it doesn't need to be added in the yml.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CollectingSlimeProcessorComponent : Component
{
    /// <summary>
    /// The moment in time when another slime will be sucked up.
    /// </summary>
    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan? SlimeAcquireMoment = default!;
}