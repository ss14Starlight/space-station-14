using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Holograms;

[Serializable, NetSerializable]
public enum HologramBriefcaseVisuals : byte
{
    State,
    HasBlade
}

[Serializable, NetSerializable]
public enum HologramBriefcaseState : byte
{
    Closed,
    Open,
    Active
}
