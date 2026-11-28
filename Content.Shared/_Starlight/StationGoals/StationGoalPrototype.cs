using Content.Shared.Cargo.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.StationGoals;

[Prototype]
public sealed partial class StationGoalPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    /// <summary>
    /// Longer instructions that only go on the printed goal sheet, such as construction steps.
    /// </summary>
    [DataField]
    public LocId? Instructions;

    [DataField]
    public SpriteSpecifier? Icon;

    /// <summary>
    /// Departments that work on this goal.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<DepartmentPrototype>> Departments = [];

    /// <summary>
    /// Everything that must be done to complete the goal.
    /// </summary>
    [DataField(required: true)]
    public List<StationGoalObjective> Objectives = [];

    /// <summary>
    /// Credits added to the cargo account of every involved department.
    /// </summary>
    [DataField]
    public int DepartmentReward = 5000;

    /// <summary>
    /// Credits paid to each crew member who works in an involved department when the goal is completed.
    /// </summary>
    [DataField]
    public int PersonalReward = 300;

    /// <summary>
    /// How long station has after picking the goal.
    /// </summary>
    [DataField]
    public TimeSpan? TimeLimit;

    /// <summary>
    /// Cargo markets opened while the goal is active.
    /// </summary>
    [DataField]
    public List<ProtoId<CargoMarketPrototype>> CargoMarkets = [];

    /// <summary>
    /// Relative chance of this goal being drawn as a card.
    /// </summary>
    [DataField]
    public float Weight = 1f;

    /// <summary>
    /// Whether the goal can be drawn at all.
    /// </summary>
    [DataField]
    public bool Offered = true;
}
