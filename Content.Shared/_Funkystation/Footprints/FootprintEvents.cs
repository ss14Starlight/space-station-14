using Robust.Shared.Serialization;

namespace Content.Shared._Funkystation.Footprints;

[Serializable, NetSerializable]
public sealed class FootprintStateEvent : EntityEventArgs
{
    public NetEntity NetEntity;
    public FootprintStateEvent(NetEntity netEntity)
    {
        NetEntity = netEntity;
    }
}

public sealed class FootprintCleanEvent : EntityEventArgs
{
}
