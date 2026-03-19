using Content.Shared._Starlight.ServerTransfer;
using Robust.Client;

namespace Content.Client._Starlight.ServerTransfer;

public sealed class ServerTransferSystem : EntitySystem
{
    [Dependency] private readonly IGameController _gameController = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("server-transfer");
        SubscribeNetworkEvent<ServerTransferEvent>(OnServerTransfer);
    }

    private void OnServerTransfer(ServerTransferEvent ev)
    {
        if (string.IsNullOrEmpty(ev.Address))
            return;

        _sawmill.Info($"Received server transfer request to {ev.Address}");

        try
        {
            _gameController.Redial(ev.Address, "Server transfer at round end.");
        }
        catch (Exception ex)
        {
            _sawmill.Warning($"Failed to redial to {ev.Address}: {ex}");
        }
    }
}
