using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.EdgeConnection;

[Serializable, NetSerializable]
public enum EdgeConnectionVisuals
{
    ConnectionMask
}

/// <summary>
/// Direction flags for edge connections.
/// </summary>
[Flags]
[Serializable, NetSerializable]
public enum EdgeConnectionFlags : byte
{
    None = 0,
    North = 1,
    South = 2,
    East = 4,
    West = 8
}
