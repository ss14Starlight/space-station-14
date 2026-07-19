using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Virology;

[Serializable, NetSerializable]
public enum PathogenStage : byte
{
    Incubation = 0,
    Symptomatic = 1,
    Critical = 2,
    Recovering = 3,
}
