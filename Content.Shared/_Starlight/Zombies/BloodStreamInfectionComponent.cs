using Robust.Shared.GameStates;
using Content.Shared.FixedPoint;

namespace Content.Shared.Zombies;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BloodStreamInfectionComponent : Component
{
    /// <summary>
    /// The current level of infection in the bloodstream. Zombification occurs at 100.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("infectionLevel")]
    public float InfectionLevel { get; set; } = 0f;

    /// <summary>
    /// The rate at which the infection level rises, typically per infectious bite.
    /// </summary>
    //how fast the infection rises, planned to be per infectious bite
    [ViewVariables(VVAccess.ReadWrite), DataField("infectionRate")]
    public float InfectionRate { get; set; } = 1f;

    /// <summary>
    /// The number of infectious bites the entity has received.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("infectiousBiteCount")]
    public int InfectiousBiteCount { get; set; } = 0;

    /// <summary>
    /// The next time the infection system will tick for this entity.
    /// </summary>
    //once per second
    [ViewVariables(VVAccess.ReadOnly), DataField("nextTickTime")]
    public TimeSpan NextTickTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Marks Initial Infected
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("isInitialInfected")]
    public bool IsInitialInfected { get; set; } = false;

    public float BloodLevel { get; set; } = 1f;

    public float PreviousBloodLevel { get; set; } = 1f;

    public float BloodLossRatio { get; set; } = 1f;

    public float ProcChance { get; set; } = 0f;

    /// <summary>
    /// The maximum infection level for this entity. Important for ambuzol rework; makes it so the current infection level is set to the maximum when ambuzol is present in the system, and reverted to the default maximum (100) once ambuzol leaves the system.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("MaximumInfectionLevel")]
    public float MaximumInfectionLevel { get; set; } = 100f;

    public bool MaximumSet { get; set; } = false;

    public FixedPoint2 OriginalCriticalThreshold { get; set; } = FixedPoint2.New(0.1);

    /// <summary>
    ///
    /// </summary>
    [DataField("hasthebriefbeenshown"), AutoNetworkedField]
    public bool HasBeenBriefed { get; set; } = false;

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
