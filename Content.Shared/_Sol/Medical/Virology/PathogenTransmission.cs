using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Virology;

[Flags, Serializable, NetSerializable]
public enum PathogenTransmission : byte
{
    None = 0,
    Contact = 1 << 0,
    Ingestion = 1 << 1,
    Airborne = 1 << 2,
    Fluid = 1 << 3,
    Surgery = 1 << 4,
}
