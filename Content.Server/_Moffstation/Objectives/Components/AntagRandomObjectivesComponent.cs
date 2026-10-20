using Content.Server.Antag.Components;
using Robust.Shared.Player;

namespace Content.Server._Moffstation.Objectives.Components;

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
    /// The amount of options to present to the player
    /// </summary>
    [DataField]
    public int MaxOptions = 8;

    [DataField]
    public int MaxChoices = 5;

    [DataField]
    public int MinChoices = 2;

    /// <summary>
    /// Kept for compatibility with the old version
    /// </summary>
    [DataField]
    public float MaxDifficulty = float.MaxValue;
}
