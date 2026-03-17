using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.ServerTransfer;

[Serializable, NetSerializable]
public sealed class ServerTransferEvent : EntityEventArgs
{
    public string Address { get; set; } = string.Empty;
}
