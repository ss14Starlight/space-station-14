using Content.Shared.Silicons.StationAi;
using Content.Shared._Starlight.Wires;
using Robust.Shared.Player;

// ReSharper disable CheckNamespace
namespace Content.Client.Wires.UI;

public sealed partial class WiresBoundUserInterface
{
    [Dependency] private ISharedPlayerManager _playerManager = default!;

    private bool _isStationAiUser =>
        _playerManager.LocalEntity is { } local && EntMan.HasComponent<StationAiHeldComponent>(local);

    private void SendStationAiWireCutHoldMessage(int id, bool started) =>
        SendMessage(new StationAiWireCutHoldMessage(id, started));
}
