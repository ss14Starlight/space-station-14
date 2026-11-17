using Content.Shared.Objectives;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Objectives.ObjectivePicker;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class PotentialObjectivesComponent : Component
{
    /// <summary>
    /// The delay until the objectives get automatically selected
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan AutoSelectionDelay = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The in-game time it get selected
    /// </summary>
    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan AutoSelectionTime;

    /// <summary>
    /// The objective options presented to the player
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<NetEntity, ObjectiveInfo> ObjectiveOptions = new();

    [DataField, AutoNetworkedField]
    public int MaxChoices = 3;

    [DataField]
    public int MinChoices = 1;

    public override bool SessionSpecific => true;

    [ViewVariables, AutoNetworkedField]
    public float MinimumDifficulty;

    [ViewVariables, AutoNetworkedField]
    public Dictionary<NetEntity, float> Difficulties = new();

    [ViewVariables, AutoNetworkedField]
    public Dictionary<NetEntity, HashSet<NetEntity>> Conflicts = new();

    [ViewVariables, AutoNetworkedField]
    public HashSet<NetEntity> UnavailableObjectives = new();

    /// <summary>
    /// Anonymous candidate tokens for deferred targets. These never identify a mind or character.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<NetEntity, HashSet<int>> TargetPools = new();

    /// <summary>
    /// Objectives whose target filters forbid sharing the same target.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<NetEntity, HashSet<NetEntity>> TargetConflicts = new();

    [ViewVariables, AutoNetworkedField]
    public bool MulliganUsed;

    [ViewVariables, AutoNetworkedField]
    public NetEntity? RetainedObjective;
}
