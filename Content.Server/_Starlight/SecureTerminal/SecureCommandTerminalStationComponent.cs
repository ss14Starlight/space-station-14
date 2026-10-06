using Content.Shared._Starlight.SecureTerminal;

namespace Content.Server._Starlight.SecureTerminal;

/// <summary>
/// Tracks all Secure Command Terminal proposals for a station.
/// Automatically added to the station entity the first time any console on it is used.
/// </summary>
[RegisterComponent]
public sealed partial class SecureCommandTerminalStationComponent : Component
{
    /// <summary>Active proposals keyed by RequestId.</summary>
    [ViewVariables]
    public readonly Dictionary<string, SecureTerminalProposalData> ActiveProposals = new();

    /// <summary>Per-request cooldown end times (CurTime).</summary>
    [ViewVariables]
    public readonly Dictionary<string, TimeSpan> Cooldowns = new();

    /// <summary>Active request-specific salary changes keyed by salary source.</summary>
    [ViewVariables]
    public readonly Dictionary<string, float> SalaryModifiers = new(StringComparer.Ordinal);

    /// <summary>One-time-use request IDs permanently consumed this round.</summary>
    [ViewVariables]
    public readonly HashSet<string> UsedOnce = [];

    /// <summary>Armory requests that have fired and are deployed. Maps requestId → time of authorization (for recall delay). Removed when recalled.</summary>
    [ViewVariables]
    public readonly Dictionary<string, TimeSpan> DeployedArmories = new();

    /// <summary>Requester of each deployed armory.</summary>
    [ViewVariables]
    public readonly Dictionary<string, EntityUid> DeployedArmoryRequesters = new();

    /// <summary>When the current alert level was last set (CurTime). Used for RequiresAlertActiveMinutes checks.</summary>
    [ViewVariables]
    public TimeSpan AlertLevelSetAt;
}

/// <summary>Server-only live data for one pending/activating proposal.</summary>
public sealed class SecureTerminalProposalData
{
    public string RequestId = string.Empty;

    /// <summary>The player who created the request.</summary>
    public EntityUid Requester = EntityUid.Invalid;

    /// <summary>The terminal used by the requester while creating the proposal.</summary>
    public EntityUid RequesterTerminal = EntityUid.Invalid;

    /// <summary>The reason of the Request.</summary>
    public string Reason = string.Empty;

    public bool AdminApproved = false;

    /// <summary>
    /// Each entry: PlayerUid, display name, job name, terminal, scheme index, and auth-group index.
    /// </summary>
    public readonly List<(EntityUid PlayerUid, string Name, string Job, EntityUid TerminalUid, int SchemeIndex, int GroupIndex)> Authorizers = new();
    public readonly List<(EntityUid PlayerUid, string Name, string Job, EntityUid TerminalUid, int SchemeIndex, int GroupIndex)> Vetoers = new();

    public readonly List<EntityUid> UsedTerminals = new();
    public readonly List<EntityUid> UsedVetoTerminals = new();

    /// <summary>CurTime when the proposal was created.</summary>
    public TimeSpan CreatedAt;

    /// <summary>CurTime when the action fires. Null while still collecting signatures.</summary>
    public TimeSpan? ActivateAt;

    public SecureTerminalProposalStatus Status = SecureTerminalProposalStatus.Pending;
}
