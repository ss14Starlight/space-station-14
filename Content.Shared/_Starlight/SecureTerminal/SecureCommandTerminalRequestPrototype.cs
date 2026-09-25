using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.SecureTerminal;

/// <summary>
/// Defines one requestable action in the Secure Command Terminal.
/// Added to all stations via the station prototype; all values are data-driven.
/// </summary>
[Prototype("secureTerminalRequest")]
public sealed partial class SecureCommandTerminalRequestPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Localization key for the display name shown in the request list.</summary>
    [DataField(required: true)]
    public string Name = string.Empty;

    /// <summary>Localization key for the info panel description.</summary>
    [DataField]
    public string Description = string.Empty;

    /// <summary>If true, will announce the proposal.</summary>
    [DataField]
    public bool ProposalAnnouncement = true;

    /// <summary>
    /// Localization key for the global announcement sent when all signatures are collected
    /// (i.e., when the countdown begins).  Null = no announcement.
    /// </summary>
    [DataField]
    public string? Announcement;

    [DataField]
    public Color AnnouncementColor = Color.Orange;

    /// <summary>
    /// If set, this request is a sub-item and will appear indented under the named parent request in the UI.
    /// </summary>
    [DataField]
    public string? ParentId;

    /// <summary>
    /// Seconds to wait after all authorizations are collected before executing the action.
    /// This is the "ETA" window that prevents ghost roles from arriving immediately.
    /// </summary>
    [DataField]
    public int ActivationDelaySecs = 600;

    /// <summary>
    /// Credits charged from the requester once it is fully approved and the countdown begins.
    /// </summary>
    [DataField]
    public int Fee = 5000;

    /// <summary>
    /// Additional salary changes for salary sources after this request activates.
    /// Positive values increase the affected source's salary; negative values reduce it.
    /// </summary>
    [DataField]
    public List<SecureTerminalSalaryModifier> SalaryModifiers = new();

    // ── Action ───────────────────────────────────────────────────────────────

    /// <summary>What type of action to perform when the timer expires.</summary>
    [DataField(required: true)]
    public SecureTerminalActionType ActionType;

    /// <summary>Entity prototype ID of the gamerule to start (ErtShuttle action).</summary>
    [DataField]
    public string? GameruleId;

    /// <summary>Alert level key to force-set (AlertLevel action).</summary>
    [DataField]
    public string? AlertLevel;

    /// <summary>Armory key to dispatch (Armory action).</summary>
    [DataField]
    public string? ArmoryKey;

    /// <summary>Access whitelist for MaintenanceAccess or StationAccess action.</summary>
    [DataField]
    public List<ProtoId<AccessLevelPrototype>>? AllowedAccesses = new();

    /// <summary>Access toggle for MaintenanceAccess or StationAccess action.</summary>
    [DataField]
    public bool AccessEnabled;

    // ── Authorization ─────────────────────────────────────────────────────────

    /// <summary>
    /// Alternative authorization schemes. All groups within one scheme must be satisfied;
    /// satisfying any one scheme is sufficient.
    /// </summary>
    [DataField(required: true)]
    public List<SecureTerminalAuthScheme> AuthSchemes = new();

    /// <summary>Alternative veto schemes that can cancel the request during its activation delay.</summary>
    [DataField]
    public List<SecureTerminalAuthScheme> VetoSchemes = new();

    // ── Conditions ────────────────────────────────────────────────────────────
    /// <summary>If true, the request will require a reason, this reason will be logged and if RequiresAdminApproval, will be fully showed to admins.</summary>
    [DataField]
    public bool RequireReason;

    /// <summary>If true, the request will need to be Authorized by at least ONE Admin.</summary>
    [DataField]
    public bool RequiresAdminApproval;

    /// <summary>If true, will bypass RequiresAdminApproval if no active admins.</summary>
    [DataField]
    public bool BypassIfNoAdmin = true;

    /// <summary>If true, the request button is hidden/disabled unless War Ops are active.</summary>
    [DataField]
    public bool RequiresWarDeclared;

    /// <summary>If true, the request button is hidden/disabled when War Ops is active.</summary>
    [DataField]
    public bool RequiresWarNotDeclared;

    /// <summary>If set, requires this alert level to be currently active on the station.</summary>
    [DataField]
    public string? RequiresAlertLevel;

    /// <summary>
    /// If > 0, the alert level specified in RequiresAlertLevel must have been active for at least this many minutes.
    /// </summary>
    [DataField]
    public int RequiresAlertActiveMinutes = 0;

    /// <summary>Minimum seconds from full authorization before the armory recall becomes available. 0 = no delay.</summary>
    [DataField]
    public int RecallMinDelaySecs = 0;

    /// <summary>Seconds before this request can be started again after completion.</summary>
    [DataField]
    public int CooldownSecs = 1800;

    /// <summary>
    /// Display order in the request list. Lower numbers appear first.
    /// Defaults to 100 so unset entries sort to the end.
    /// </summary>
    [DataField]
    public int SortOrder = 100;

    /// <summary>
    /// If true, this request can only be activated once per round.
    /// After use or recall it shows "USED" and cannot be re-requested.
    /// </summary>
    [DataField]
    public bool OneTimeUse;
}

[DataDefinition]
public sealed partial class SecureTerminalAuthScheme
{
    /// <summary>Technical identifier used to distinguish this scheme from other alternatives.</summary>
    [DataField(required: true)]
    public string Id = string.Empty;

    /// <summary>Optional localization key or display name for this scheme.</summary>
    [DataField]
    public string? Name;

    /// <summary>Optional localization key or description for this scheme.</summary>
    [DataField]
    public string? Description;

    /// <summary>
    /// Authorization groups within this scheme. Any one access tag satisfies a group;
    /// all groups must be satisfied by distinct individuals.
    /// </summary>
    [DataField(required: true)]
    public List<List<ProtoId<AccessLevelPrototype>>> Groups = new();
}

[DataDefinition]
public sealed partial class SecureTerminalSalaryModifier
{
    /// <summary>Salary source to modify. Use `Everyone` to affect every salary payout.</summary>
    [DataField(required: true)]
    public string Source = string.Empty;

    [DataField(required: true)]
    public float Change;
}

public enum SecureTerminalActionType
{
    GameRule,
    AlertLevel,
    Armory,
    NukeCodes,
    AirlockAccess,
    /// <summary>Performs no mechanical change — the request exists purely for its announcement.</summary>
    Announcement,
    EscapePods
}
