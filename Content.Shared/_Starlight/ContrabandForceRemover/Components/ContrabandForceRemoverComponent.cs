using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Contraband;
using Content.Shared.Roles;

namespace Content.Shared._Starlight.ContrabandForceRemover.Components;

/// <summary>
/// A turnstile-like gate that detects contraband and removes or blocks items based on job permissions and severity.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ContrabandForceRemoverComponent : Component
{
    /// <summary>
    /// The delay between scans.
    /// </summary>
    [DataField("scanDelay"), ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan ScanDelay = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// The next time the gate can perform a scan.
    /// </summary>
    [AutoNetworkedField, AutoPausedField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextScanTime = TimeSpan.Zero;

    /// <summary>
    /// The sound played when scanning.
    /// </summary>
    [DataField("scanSound")]
    public SoundSpecifier ScanSound = new SoundCollectionSpecifier("ScanGateScan", AudioParams.Default.WithVolume(-5f));

    /// <summary>
    /// The sound played when contraband is detected and removed.
    /// </summary>
    [DataField("scanFailSound")]
    public SoundSpecifier ScanFailSound = new SoundPathSpecifier("/Audio/_Starlight/Effects/ScanGate/scan_fail.ogg", AudioParams.Default.WithVolume(-5f));

    /// <summary>
    /// Sprite state to set on successful scan (no contraband).
    /// </summary>
    [DataField("scanSuccessState")]
    public string ScanSuccessState = "success";

    /// <summary>
    /// Sprite state to set on failed scan (contraband detected).
    /// </summary>
    [DataField("scanFailState")]
    public string ScanFailState = "fail";

    /// <summary>
    /// Sprite state to set when idle.
    /// </summary>
    [DataField("idleState")]
    public string IdleState = "idle";

    /// <summary>
    /// Contraband severity levels that NO ONE can pass with (except specific rules).
    /// </summary>
    [DataField("alwaysBlockedSeverities")]
    public HashSet<ProtoId<ContrabandSeverityPrototype>> AlwaysBlockedSeverities = new()
    {
        "Major",
        "Syndicate",
        "Magical",
        "Soviet",
        "AdvancedCyberlimbs"
    };

    /// <summary>
    /// Contraband severity that only Central Command can pass with.
    /// </summary>
    [DataField("centralCommandOnlySeverities")]
    public HashSet<ProtoId<ContrabandSeverityPrototype>> CentralCommandOnlySeverities = new()
    {
        "HighlyIllegal"
    };

    /// <summary>
    /// Contraband severity that Command and Security can pass with.
    /// </summary>
    [DataField("commandSecuritySeverities")]
    public HashSet<ProtoId<ContrabandSeverityPrototype>> CommandSecuritySeverities = new()
    {
        "Minor"
    };

    /// <summary>
    /// Contraband severity that only Command can pass with.
    /// </summary>
    [DataField("commandOnlySeverities")]
    public HashSet<ProtoId<ContrabandSeverityPrototype>> CommandOnlySeverities = new()
    {
        "GrandTheft"
    };

    /// <summary>
    /// Departments that are considered "Command".
    /// </summary>
    [DataField("commandDepartments")]
    public HashSet<ProtoId<DepartmentPrototype>> CommandDepartments = new()
    {
        "Command",
        "CentralCommand"
    };

    /// <summary>
    /// Department that is considered "Security" for minor contraband.
    /// </summary>
    [DataField("securityDepartment")]
    public ProtoId<DepartmentPrototype> SecurityDepartment = "Security";

    /// <summary>
    /// Department that is considered "Central Command" for highly illegal contraband.
    /// </summary>
    [DataField("centralCommandDepartment")]
    public ProtoId<DepartmentPrototype> CentralCommandDepartment = "CentralCommand";

    /// <summary>
    /// Contraband severities that require exact job/department match (like TSF-specific items).
    /// These check the contraband's allowed jobs/departments and only allow if there's an exact match.
    /// </summary>
    [DataField("jobSpecificSeverities")]
    public HashSet<ProtoId<ContrabandSeverityPrototype>> JobSpecificSeverities = new()
    {
        "TSF"
    };

    /// <summary>
    /// Contraband severities that require departmental restriction match.
    /// These check if the person's department is in the contraband's allowed departments.
    /// </summary>
    [DataField("departmentRestrictedSeverities")]
    public HashSet<ProtoId<ContrabandSeverityPrototype>> DepartmentRestrictedSeverities = new()
    {
        "Restricted"
    };

    /// <summary>
    /// The signal to send on successful scan.
    /// </summary>
    [DataField]
    public string SuccessSignal = "ScanGateSuccess";

    /// <summary>
    /// The signal to send on failed scan (contraband detected).
    /// </summary>
    [DataField]
    public string FailSignal = "ScanGateFail";

    /// <summary>
    /// Maintained hashset of entities currently passing through.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> PassingThrough = new();
}
