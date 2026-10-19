using Robust.Shared.Network.Messages;

// ReSharper disable CheckNamespace
namespace Content.Server.Sandbox;

public sealed partial class SandboxSystem
{
    private bool CanPlace(MsgPlacement placement)
    {
        if (IsSandboxEnabled) return true;

        var channel = placement.MsgChannel;
        var player = _playerManager.GetSessionByChannel(channel);

        return _conGroupController.CanAdminPlace(player);
    }
}
