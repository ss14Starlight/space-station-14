using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
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
    public List<ExtractReaction> ExtractReactions = new();
    
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
    
    /// <summary>
    /// Whether the current slime extract is paused. Needed as AutoGenerateComponentPause is not possible on this component.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool CurrentlyPaused = false;
}

/// <summary>
/// A set of requirements and the associated effects.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class ExtractReaction
{
    /// <summary>
    /// The minimum reagent requirements.
    /// </summary>
    [DataField("requirements", required: true)]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Requirements = default!;

    /// <summary>
    /// The effects caused when there is enough of the required reagents.
    /// </summary>
    [DataField("effects", required: true)]
    public List<ScaledEntityEffect> Effects = default!;
    
    /// <summary>
    /// Whether the extract should be deleted upon this reaction occuring.
    /// </summary>
    [DataField("shouldDelete", required: true)]
    public bool ShouldDelete = default!;

    /// <summary>
    /// If nonzero, how long until the effect actually occurs.
    /// </summary>
    [DataField("delay")]
    public TimeSpan Delay = TimeSpan.Zero;
    
    /// <summary>
    /// The moment at which the effect actually occurs.
    /// </summary>
    [ViewVariables]
    public TimeSpan? ActivationMoment;
}

/// <summary>
/// An entity effect combined with a scaling factor.
/// </summary>
/// <remarks>
/// While this could be used anywhere, this is meant for xenobiology.
/// As such, I'll document the formula here for the sake of people writing slime extracts.
/// First, a minimized scaling factor is found among the reagents.
/// Specifically, for each reagent, amountInContainer/amountRequired is calculated.
/// The minimum found across all the reagents is taken and used as the minimizedScalingFactor.
/// The final factor is then minimizedScalingFactor * scalingFactor + scalingOffset.
/// Great fans of y = mx + b should find themselves right at home here.
/// </remarks>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class ScaledEntityEffect
{
    /// <summary>
    /// The effect.
    /// </summary>
    [DataField("effect", required: true)]
    public EntityEffect Effect = default!;
    
    /// <summary>
    /// Increases the scale in proportion to how much reagent was provided.
    /// </summary>
    [DataField("scalingFactor")]
    public FixedPoint2 ScalingFactor = 0;

    /// <summary>
    /// A flat modifier added at the end of calculating the scale.
    /// </summary>
    [DataField("scalingOffset")]
    public FixedPoint2 ScalingOffset = 1;
}