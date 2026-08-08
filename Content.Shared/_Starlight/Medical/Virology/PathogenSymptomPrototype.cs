using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// A single expressible symptom. Strains are built by combining these, so a symptom
/// should be self-contained and say nothing about which strain is carrying it.
/// </summary>
[Prototype]
public sealed partial class PathogenSymptomPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Shown by the analyzer once the strain has been catalogued.
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// The stage the host must reach before this expresses at all.
    /// </summary>
    [DataField]
    public int MinStage = 1;

    /// <summary>
    /// Average time between expressions.
    /// </summary>
    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Fraction of <see cref="Interval"/> to randomly vary each expression by.eak.
    /// </summary>
    [DataField]
    public float IntervalVariance = 0.35f;

    /// <summary>
    /// What actually happens to the host. Reuses the standard entity effect library,
    /// so anything a reagent can do, a symptom can do.
    /// </summary>
    [DataField]
    public List<EntityEffect> Effects = new();
}
