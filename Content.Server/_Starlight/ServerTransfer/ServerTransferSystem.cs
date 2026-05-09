using Content.Server.GameTicking;
using Content.Shared._Starlight.ServerTransfer;
using Robust.Shared.Player;

namespace Content.Server._Starlight.ServerTransfer;

public sealed class ServerTransferSystem : EntitySystem
{
    private string? _targetAddress;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("server-transfer");
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    public void SetTargetAddress(string address) => _targetAddress = address;

    public void ClearTargetAddress() => _targetAddress = null;

    public string? GetTargetAddress() => _targetAddress;

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.PreRoundLobby || ev.Old != GameRunLevel.PostRound)
            return;

        if (string.IsNullOrEmpty(_targetAddress))
            return;

        _sawmill.Info($"Round ended. Redirecting all players to {_targetAddress}");

        var msg = new ServerTransferEvent { Address = _targetAddress };

        RaiseNetworkEvent(msg, Filter.Broadcast());
    }
}
