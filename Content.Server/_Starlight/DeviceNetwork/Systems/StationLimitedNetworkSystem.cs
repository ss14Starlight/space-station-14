// ReSharper disable CheckNamespace
using Content.Shared._Starlight.Maps;
using Robust.Shared.Map;

namespace Content.Server.DeviceNetwork.Systems;

public sealed partial class StationLimitedNetworkSystem
{
    [Dependency] private SharedGridAccessSystem _gridAccess = default!;

    private bool CanAccessSenderGrid(EntityUid receiver, EntityUid sender)
    {
        var receiverGrid = Transform(receiver).GridUid;
        var senderGrid = Transform(sender).GridUid;

        return receiverGrid is { } targetGrid && senderGrid is { } sourceGrid && (_gridAccess.CanAccess((sourceGrid, null), (targetGrid, null)) || _gridAccess.CanAccess((targetGrid, null), (sourceGrid, null)));
    }
}
