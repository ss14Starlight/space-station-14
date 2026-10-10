using Content.Server.EUI;
using Content.Shared._Starlight.SecureTerminal;
using Content.Shared.Eui;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server._Starlight.SecureTerminal;

public sealed partial class SecureTerminalAdminApprovalEui : BaseEui
{
    private readonly SecureCommandTerminalSystem _terminalSystem;
    [Dependency] private IEntityManager _entityManager = default!;

    public EntityUid StationUid { get; }
    public string RequestId { get; }
    public string RequestName { get; }
    public string RequestDescription { get; }
    public string? Reason { get; }
    public IReadOnlyList<string> AuthorizedBy { get; }

    public SecureTerminalAdminApprovalEui(SecureCommandTerminalSystem terminalSystem, EntityUid stationUid, string requestId,
        string requestName, string requestDescription, string? reason, IReadOnlyList<string> authorizedBy)
    {
        IoCManager.InjectDependencies(this);
        _terminalSystem = terminalSystem;
        StationUid = stationUid;
        RequestId = requestId;
        RequestName = requestName;
        RequestDescription = requestDescription;
        Reason = reason;
        AuthorizedBy = authorizedBy;
    }

    public override void Opened() => StateDirty();

    public override SecureTerminalAdminApprovalEuiState GetNewState() => new()
    {
        StationUid = _entityManager.GetNetEntity(StationUid),
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
            _terminalSystem.HandleAdminApproval(Player, StationUid, RequestId, approval.Approved);
            Close();
        }
    }

    public override void Closed() => _terminalSystem.OnAdminApprovalEuiClosed(this);
}
