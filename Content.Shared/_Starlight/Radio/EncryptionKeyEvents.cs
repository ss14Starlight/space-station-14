using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Radio;

[Serializable, NetSerializable]
public sealed class EncryptionKeyToggleMessage(ProtoId<RadioChannelPrototype> protoId) : BoundUserInterfaceMessage
{
    public ProtoId<RadioChannelPrototype> ProtoId = protoId;
}

[Serializable, NetSerializable]
public enum EncryptionKeyHolderUiKey : byte
{
    Key
}
