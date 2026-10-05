// ReSharper disable once CheckNamespace

using Robust.Shared.Serialization;

namespace Content.Shared.SubFloor;

[Flags, FlagsFor(typeof(VisibilityMask))]
public enum SubFloorVisibilityMask : int
{
    None = 0,
    Pipes = 1 << 0,
    LV = 1 << 1,
    MV = 1 << 2,
    HV = 1 << 3,
    Disposal = 1 << 4,
    Other = 1 << 5,
    All = Pipes | LV | MV | HV | Disposal | Other,
}
