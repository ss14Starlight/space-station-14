using Robust.Shared.GameStates;
using Content.Shared.FixedPoint;

namespace Content.Shared.Zombies;

[RegisterComponent]
public sealed partial class BloodStreamInfectionComponent : Component
{
    //level of infection in the bloodstream, zombification at 100
    [ViewVariables(VVAccess.ReadWrite), DataField("infectionLevel")]
    public float InfectionLevel { get; set; } = 0f;

    //how fast the infection rises, planned to be per infectious bite
    [ViewVariables(VVAccess.ReadWrite), DataField("infectionRate")]
    public float InfectionRate { get; set; } = 1f;

    //how many infectious bites they have received (80% chance for zombie bite to be infectious)
    [ViewVariables(VVAccess.ReadWrite), DataField("infectiousBiteCount")]
    public int InfectiousBiteCount { get; set; } = 0;

    //once per second
    [ViewVariables(VVAccess.ReadOnly), DataField("nextTickTime")]
    public TimeSpan NextTickTime { get; set; } = TimeSpan.Zero;

    //Forced critical infection threshold (chance to go "oh shit, he's about to zombify" for medical staff before they zombify)
    [ViewVariables(VVAccess.ReadOnly), DataField("forcedCriticalStage")]
    public float ForcedCriticalStage { get; set; } = 90f;

    [ViewVariables(VVAccess.ReadWrite), DataField("isInitialInfected")]
    public bool IsInitialInfected { get; set; } = false;

    public float BloodLevel { get; set; } = 1f;

    public float PreviousBloodLevel { get; set; } = 1f;

    public float BloodLossRatio { get; set; } = 1f;

    public float ProcChance { get; set; } = 0f;

    //important for ambuzol rework. makes it so the current is set to max when ambuzol is present in system, reverted to max 100 once ambuzol leaves system
    [ViewVariables(VVAccess.ReadWrite), DataField("MaximumInfectionLevel")]
    public float MaximumInfectionLevel { get; set; } = 100f;

    public bool MaximumSet { get; set; } = false;
    public FixedPoint2 OriginalCriticalThreshold { get; set; } = FixedPoint2.New(0.1);

    //pulled from pendingzombiecomponent start
    /// <summary>
    /// The chance each second that a warning will be shown.
    /// </summary>
    [DataField("infectionWarningChance")]
    public float InfectionWarningChance = 0.0166f;

    /// <summary>
    /// Infection warnings shown as popups
    /// </summary>
    [DataField("infectionWarnings")]
    public List<string> InfectionWarnings = new()
    {
        "zombie-infection-warning",
        "zombie-infection-underway"
    };
    //pulled from pendingzombiecomponent end
}
