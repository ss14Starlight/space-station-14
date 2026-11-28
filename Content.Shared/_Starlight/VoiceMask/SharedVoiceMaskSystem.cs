using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

//ReSharper disable once CheckNamespace
namespace Content.Shared.VoiceMask;

public sealed partial class VoiceMaskBuiState
{
    public ProtoId<JobIconPrototype>? RadioJobIcon;
}

[Serializable, NetSerializable]
public sealed class VoiceMaskChangeRadioIconMessage : BoundUserInterfaceMessage
{
    public readonly ProtoId<JobIconPrototype>? JobIcon;

    public VoiceMaskChangeRadioIconMessage(ProtoId<JobIconPrototype>? jobIcon)
        => JobIcon = jobIcon;
}
