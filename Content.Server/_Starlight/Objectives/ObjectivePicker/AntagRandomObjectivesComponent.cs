using Content.Server.Antag.Components;
using Content.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Objectives.ObjectivePicker;

[RegisterComponent]
public sealed partial class AntagRandomObjectivesComponent : Component
{
    /// <summary>
    /// Each set of objectives to add.
    /// </summary>
    [DataField(required: true)]
    public List<AntagObjectiveSet> Sets = new();

    /// <summary>
    /// Selection time for objectives, set to 0 to have them be instantly picked randomly
    /// </summary>
    [DataField]
    public TimeSpan SelectionDelay = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The preferred number of options; expanded as needed to meet the difficulty budget.
    /// </summary>
    [DataField]
    public int MaxOptions = 8;

    [DataField]
    public int MaxChoices = 5;

    [DataField]
    public int MinChoices = 2;

    /// <summary>
    /// Minimum selected difficulty; the offered pool must provide twice this amount.
    /// </summary>
    [DataField]
    public float MaxDifficulty = 6;

    [DataField("forcedObjectiveGroup")]
    public ProtoId<WeightedRandomPrototype>? ForcedObjectiveGroup;

    [DataField("traitorForcedObjectiveGroup")]
    public ProtoId<WeightedRandomPrototype>? TraitorForcedObjectiveGroup;

    [DataField("storyObjectiveGroup")]
    public ProtoId<WeightedRandomPrototype>? StoryObjectiveGroup;

    [DataField("excludedObjectives")]
    public HashSet<EntProtoId> ExcludedObjectives = new();
}
