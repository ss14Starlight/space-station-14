using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Xenobiology;

/// <summary>
/// A set of requirements and the associated effects.
/// </summary>
[Prototype]
public sealed partial class ExtractReactionPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

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
[DataDefinition]
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
