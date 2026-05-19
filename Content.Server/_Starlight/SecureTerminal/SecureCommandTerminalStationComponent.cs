using Content.Shared.Starlight.SecureTerminal;

namespace Content.Server.Starlight.SecureTerminal;

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

    /// <summary>
    /// Accumulated salary penalty for this station this round (0–0.8).
    /// Each activated proposal adds its SalaryPenalty value.
    /// </summary>
    [ViewVariables]
    public float SalaryPenalty;

    /// <summary>One-time-use request IDs permanently consumed this round.</summary>
    [ViewVariables]
    public readonly HashSet<string> UsedOnce = new();

    /// <summary>Armory requests that have fired and are deployed. Maps requestId → time of authorization (for recall delay). Removed when recalled.</summary>
    [ViewVariables]
    public readonly Dictionary<string, TimeSpan> DeployedArmories = new();

    /// <summary>When the current alert level was last set (CurTime). Used for RequiresAlertActiveMinutes checks.</summary>
    [ViewVariables]
    public TimeSpan AlertLevelSetAt;
}

/// <summary>Server-only live data for one pending/activating proposal.</summary>
public sealed class SecureTerminalProposalData
{
    public string RequestId = string.Empty;

    /// <summary>The reason of the Request.</summary>
    public string Reason = string.Empty;

    public bool AdminApproved = false;

    /// <summary>
    /// Each entry: PlayerUid, display name, job name, which auth-group index they satisfy.
    /// </summary>
    public readonly List<(EntityUid PlayerUid, string Name, string Job, int GroupIndex)> Authorizers = new();

    public readonly List<EntityUid> UsedTerminals = new();

    /// <summary>CurTime when the action fires. Null while still collecting signatures.</summary>
    public TimeSpan? ActivateAt;

    public TimeSpan? AuthTimer;

    public SecureTerminalProposalStatus Status = SecureTerminalProposalStatus.Pending;
}
