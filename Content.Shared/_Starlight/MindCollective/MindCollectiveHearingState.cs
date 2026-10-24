using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.MindCollective;

[Serializable, NetSerializable]
public readonly record struct MindCollectiveHearingState(NetEntity Body, float Pressure, bool SealedHelmet);
