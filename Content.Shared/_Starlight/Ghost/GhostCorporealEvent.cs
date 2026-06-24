using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Ghost;

[Serializable, NetSerializable]
public sealed class GhostCorporealChatUpdateEvent(bool isCorporeal) : EntityEventArgs
{
    public bool IsCorporeal { get; init; } = isCorporeal;
}

[Serializable, NetSerializable]
public sealed class GhostCorporealEvent(bool isCorporeal, NetEntity uid) : EntityEventArgs
{
    public bool IsCorporeal { get; init; } = isCorporeal;
    public NetEntity Uid { get; init; } = uid;
}
