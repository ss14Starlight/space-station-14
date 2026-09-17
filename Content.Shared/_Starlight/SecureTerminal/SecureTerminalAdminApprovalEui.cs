using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.SecureTerminal;

[Serializable, NetSerializable]
public sealed class SecureTerminalAdminApprovalEuiState : EuiStateBase
{
    public string RequestId = string.Empty;
    public string RequestName = string.Empty;
    public string RequestDescription = string.Empty;
    public string? Reason;
    public List<string> AuthorizedBy = new();
}

[Serializable, NetSerializable]
public sealed class SecureTerminalAdminApprovalMessage : EuiMessageBase
{
    public readonly bool Approved;

    public SecureTerminalAdminApprovalMessage(bool approved) => Approved = approved;
}
