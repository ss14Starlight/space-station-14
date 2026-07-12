using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
// Based on the RMC14.
// https://github.com/RMC-14/RMC-14
namespace Content.Shared._Starlight.Medical.Surgery.Components;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
[AutoGenerateComponentPause]
public sealed partial class IncisionOpenComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
}
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class SkinRetractedComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class BleedersClampedComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryStepOrganExtractComponent : Component
{
    [DataField]
    public ComponentRegistry? Organ;

    [DataField]
    public string? Slot;
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryStepOrganInsertComponent : Component
{
    [DataField(required: true)]
    public string Slot;
}
