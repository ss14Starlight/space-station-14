using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._Starlight.DeviceLinking.Components;

[RegisterComponent]
public sealed partial class GasVolumePumpSignalComponent : Component
{
    [DataField]
    public ProtoId<SinkPortPrototype> OnPort = "On";

    [DataField]
    public ProtoId<SinkPortPrototype> OffPort = "Off";

    [DataField]
    public ProtoId<SinkPortPrototype> TogglePort = "Toggle";
}
