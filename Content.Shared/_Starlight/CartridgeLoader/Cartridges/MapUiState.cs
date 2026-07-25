using Robust.Shared.Serialization;

namespace Content.Shared.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class MapUiState : BoundUserInterfaceState
{
    public NetEntity mapUid;
    public NetEntity trackedEntityUid;

    public MapUiState(NetEntity mapUid, NetEntity trackedEntityUid)
    {
        this.mapUid = mapUid;
        this.trackedEntityUid = trackedEntityUid;
    }
}
