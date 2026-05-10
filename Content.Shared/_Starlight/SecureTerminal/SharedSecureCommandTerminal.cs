using Robust.Shared.Serialization;

namespace Content.Shared.Starlight.SecureTerminal;

/// <summary>
/// Marker component added to communications consoles that have the Secure Command Terminal feature.
/// </summary>
[RegisterComponent]
public sealed partial class SecureCommandTerminalConsoleComponent : Component
{
    /// <summary>
    /// When false the terminal UI is completely inert — no proposals can be created or authorized.
    /// Set to true on Command comms consoles, false on Syndicate/Wizard equivalents.
    /// Editable in VV at runtime.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Enabled = true;

    /// <summary>If true, will be able to approve/deny AdminNeededRequest.</summary>
    [DataField]
    public bool Admin = false;

    /// <summary>If true, this terminal will only be able to approve/deny.</summary>
    [DataField]
    public bool AuthTerminal = false;
}

[Serializable, NetSerializable]
public enum SecureCommandTerminalUiKey { Key }

[Serializable, NetSerializable]
public enum SecureTerminalProposalStatus
{
    Pending,    // waiting for all required authorizations
    Activating, // all authorizations received, countdown running
    Completed,  // action has been executed
}

// ── BUI Messages ─────────────────────────────────────────────────────────────

/// <summary>Request / re-propose an action.</summary>
[Serializable, NetSerializable]
public sealed class SecureTerminalRequestMessage : BoundUserInterfaceMessage
{
    public readonly string RequestId;
    public SecureTerminalRequestMessage(string requestId) => RequestId = requestId;
}

/// <summary>Authorize / sign the currently pending proposal for a given request.</summary>
[Serializable, NetSerializable]
public sealed class SecureTerminalAuthorizeMessage : BoundUserInterfaceMessage
{
    public readonly string RequestId;
    public SecureTerminalAuthorizeMessage(string requestId) => RequestId = requestId;
}

/// <summary>Cancel / deny the currently pending proposal for a given request.</summary>
[Serializable, NetSerializable]
public sealed class SecureTerminalDenyMessage : BoundUserInterfaceMessage
{
    public readonly string RequestId;
    public SecureTerminalDenyMessage(string requestId) => RequestId = requestId;
}

/// <summary>Abort an activating armory during its countdown (free, marks it permanently as used).</summary>
[Serializable, NetSerializable]
public sealed class SecureTerminalRecallMessage : BoundUserInterfaceMessage
{
    public readonly string RequestId;
    public SecureTerminalRecallMessage(string requestId) => RequestId = requestId;
}

// ── BUI State ────────────────────────────────────────────────────────────────

/// <summary>
/// Net-serializable snapshot of one active proposal, sent to clients.
/// </summary>
[Serializable, NetSerializable]
public sealed class SecureTerminalProposalState
{
    public string RequestId = string.Empty;

    /// <summary>Display name + job-title of each person who has signed.</summary>
    public List<(string Name, string Job)> AuthorizedBy = new();

    /// <summary>True if the corresponding auth-group has been satisfied.</summary>
    public List<bool> GroupsSatisfied = new();

    /// <summary>Human-readable label per auth-group, e.g. "Captain / HoS".</summary>
    public List<string> GroupLabels = new();

    /// <summary>
    /// When the action will fire (CurTime, server-side).
    /// Null while still gathering authorizations.
    /// </summary>
    public TimeSpan? ActivateAt;

    public TimeSpan? AuthTimer;

    public SecureTerminalProposalStatus Status;
}

[Serializable, NetSerializable]
public sealed class SecureCommandTerminalInterfaceState : BoundUserInterfaceState
{
    public readonly List<SecureTerminalProposalState> Proposals;
    public readonly bool IsWarDeclared;
    /// <summary>RequestId → cooldown end time (CurTime). Entries disappear when cooldown expires.</summary>
    public readonly Dictionary<string, TimeSpan> CoolingDown;
    /// <summary>Current station alert level id (e.g. "gamma"), or null if unknown.</summary>
    public readonly string? CurrentAlertLevel;
    /// <summary>One-time-use request IDs permanently consumed this round.</summary>
    public readonly HashSet<string> UsedOnce;
    /// <summary>When the current alert level was last set (CurTime).</summary>
    public readonly TimeSpan AlertLevelSetAt;
    /// <summary>Armory requests currently deployed (fired but not recalled). Maps requestId → authorization time for delay UI.</summary>
    public readonly Dictionary<string, TimeSpan> DeployedArmories;

    public SecureCommandTerminalInterfaceState(
        List<SecureTerminalProposalState> proposals,
        bool isWarDeclared,
        Dictionary<string, TimeSpan> coolingDown,
        string? currentAlertLevel,
        HashSet<string> usedOnce,
        TimeSpan alertLevelSetAt,
        Dictionary<string, TimeSpan> deployedArmories)
    {
        Proposals = proposals;
        IsWarDeclared = isWarDeclared;
        CoolingDown = coolingDown;
        CurrentAlertLevel = currentAlertLevel;
        UsedOnce = usedOnce;
        AlertLevelSetAt = alertLevelSetAt;
        DeployedArmories = deployedArmories;
    }
}
