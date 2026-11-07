using Content.Shared.DeviceLinking;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Atmos.Components;

[RegisterComponent]
public sealed partial class IgniterComponent : Component
{
    [DataField]
    public bool Enabled;

    [DataField]
    public ProtoId<SinkPortPrototype> TogglePort = "Toggle";

    [DataField]
    public ProtoId<SinkPortPrototype> OnPort = "On";

    [DataField]
    public ProtoId<SinkPortPrototype> OffPort = "Off";

    [DataField]
    public SoundSpecifier? ToggleSound = new SoundCollectionSpecifier("WelderOn");
}

[Serializable, NetSerializable]
public enum IgniterVisuals : byte
{
    Active,
}
