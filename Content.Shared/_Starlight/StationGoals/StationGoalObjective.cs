using Content.Shared.Whitelist;

namespace Content.Shared._Starlight.StationGoals;

[ImplicitDataDefinitionForInheritors]
public abstract partial class StationGoalObjective
{
    [DataField(required: true)]
    public LocId Description;

    /// <summary>
    /// Value progress must reach for this objective to count as done.
    /// </summary>
    public abstract float GetTarget();

    /// <summary>
    /// Localized objective string.
    /// </summary>
    public virtual string GetText() => Loc.GetString(Description, ("target", GetTarget()));
}

/// <summary>
/// Adds up every value reported under its key.
/// </summary>
public sealed partial class CounterGoalObjective : StationGoalObjective
{
    [DataField(required: true)]
    public string Key = string.Empty;

    [DataField]
    public float Target = 1;

    public override float GetTarget() => Target;
}

/// <summary>
/// Keeps the single highest value reported under its key.
/// </summary>
public sealed partial class RecordGoalObjective : StationGoalObjective
{
    [DataField(required: true)]
    public string Key = string.Empty;

    [DataField]
    public float Target = 1;

    public override float GetTarget() => Target;
}

public sealed partial class SellGoalObjective : StationGoalObjective
{
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// Units needed of each kind.
    /// </summary>
    [DataField]
    public int Amount = 1;

    /// <summary>
    /// How many different prototypes must each reach the amount.
    /// </summary>
    [DataField]
    public int Kinds = 1;

    public override float GetTarget() => Kinds > 1 ? Kinds : Amount;

    public override string GetText() => Loc.GetString(Description, ("amount", Amount), ("kinds", Kinds));
}
