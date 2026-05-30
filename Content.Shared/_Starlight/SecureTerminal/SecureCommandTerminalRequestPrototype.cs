using Content.Shared.Access;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Starlight.SecureTerminal;

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
    /// Credits charged from EACH authorizer when the countdown begins.
    /// They pay whether they originally requested OR co-signed.
    /// </summary>
    [DataField]
    public int Fee = 5000;

    /// <summary>
    /// Fractional salary penalty applied to the entire station when this request activates
    /// (stacks, capped at 80%).  0.05 = 5 % reduction.
    /// </summary>
    [DataField]
    public float SalaryPenalty = 0.05f;

    /// <summary>
    /// Seconds timer to collect all authorizations before getting auto-cancelled.
    /// If 0, no Timer will be applied.
    /// </summary>
    [DataField]
    public int AuthTimer = 0;

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
    /// List of authorization groups.  Each inner list is a set of access-level prototype IDs;
    /// ANY single tag in the list satisfies that group.
    /// ALL groups must be satisfied by DISTINCT individuals before the countdown begins.
    ///
    /// Example – "Captain or HoS" PLUS "NTR":
    ///   authGroups:
    ///     - [ Captain, HeadOfSecurity ]
    ///     - [ Ntrep ]
    /// </summary>
    [DataField(required: true)]
    public List<List<string>> AuthGroups = new();

    /// <summary>
    /// Human-readable label for each group shown in the Authorization panel.
    /// Must match the length of AuthGroups; falling back to the raw tag names if missing.
    /// </summary>
    [DataField]
    public List<string> AuthGroupLabels = new();

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

public enum SecureTerminalActionType
{
    GameRule,
    AlertLevel,
    Armory,
    NukeCodes,
    AirlockAccess,
}
