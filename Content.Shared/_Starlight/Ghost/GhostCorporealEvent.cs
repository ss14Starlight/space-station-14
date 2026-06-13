using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Ghost;

[Serializable, NetSerializable]
public sealed class GhostCorporealEvent(bool isCorporeal) : EntityEventArgs
{
    public bool IsCorporeal { get; init; } = isCorporeal;
}
