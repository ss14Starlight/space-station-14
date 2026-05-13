using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Tablets;

[Serializable, NetSerializable]
public enum CommandTabletLayers : byte
{
    Frame,
    Powered
}
