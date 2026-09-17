using Content.Server.EUI;
using Content.Shared._Starlight.SecureTerminal;
using Content.Shared.Eui;

namespace Content.Server._Starlight.SecureTerminal;

public sealed class SecureTerminalAdminApprovalEui : BaseEui
{
    private readonly SecureCommandTerminalSystem _terminalSystem;

    public string RequestId { get; }
    public string RequestName { get; }
    public string RequestDescription { get; }
    public string? Reason { get; }
    public IReadOnlyList<string> AuthorizedBy { get; }

    public SecureTerminalAdminApprovalEui(SecureCommandTerminalSystem terminalSystem, string requestId,
        string requestName, string requestDescription, string? reason, IReadOnlyList<string> authorizedBy)
    {
        _terminalSystem = terminalSystem;
        RequestId = requestId;
        RequestName = requestName;
        RequestDescription = requestDescription;
        Reason = reason;
        AuthorizedBy = authorizedBy;
    }

    public override void Opened() => StateDirty();

    public override SecureTerminalAdminApprovalEuiState GetNewState() => new()
    {
        RequestId = RequestId,
        RequestName = RequestName,
        RequestDescription = RequestDescription,
        Reason = Reason,
        AuthorizedBy = new List<string>(AuthorizedBy)
    };

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (msg is SecureTerminalAdminApprovalMessage approval)
        {
            _terminalSystem.HandleAdminApproval(Player, RequestId, approval.Approved);
            Close();
        }
    }

    public override void Closed() => _terminalSystem.OnAdminApprovalEuiClosed(this);
}
