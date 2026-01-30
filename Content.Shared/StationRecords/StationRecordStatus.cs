using Robust.Shared.Serialization;

namespace Content.Shared.StationRecords;

[Serializable, NetSerializable]
public enum StationRecordStatus : byte
{
    OnStation = 0,
    Cryo = 1,
    Unknown = 2,
}

